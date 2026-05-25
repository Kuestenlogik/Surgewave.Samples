using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Spectre.Console;

namespace Kuestenlogik.Surgewave.Samples.MultiProtocol;

/// <summary>
/// Demonstrates producing and consuming messages using Surgewave's native protocol.
/// Optimized for maximum performance with minimal overhead.
/// </summary>
public sealed class NativeProtocolDemo
{
    private readonly string _bootstrapServers;
    private readonly string _topic;

    public NativeProtocolDemo(string bootstrapServers, string topic)
    {
        _bootstrapServers = bootstrapServers;
        _topic = topic;
    }

    public async Task<List<StockQuote>> ProduceAsync(string[] symbols, int count)
    {
        var quotes = new List<StockQuote>();

        await using var client = await SurgewaveClient.Create(_bootstrapServers)
            .UseSurgewaveProtocol()
            .BuildAsync();

        await using var producer = client.CreateProducer<string, string>();

        for (int i = 0; i < count; i++)
        {
            var symbol = symbols[i % symbols.Length];
            var quote = StockQuote.Generate(symbol, "Native");
            quotes.Add(quote);

            await producer.ProduceAsync(_topic, symbol, quote.ToJson());
        }

        return quotes;
    }

    public async Task<List<StockQuote>> ConsumeAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var quotes = new List<StockQuote>();

        await using var client = await SurgewaveClient.Create(_bootstrapServers)
            .UseSurgewaveProtocol()
            .BuildAsync();

        await using var consumer = client.CreateConsumer<string, string>(options =>
        {
            options.GroupId = $"multi-protocol-native-{Guid.NewGuid():N}";
            options.AutoOffsetReset = AutoOffsetReset.Earliest;
            options.EnableAutoCommit = false;
        });

        consumer.Subscribe(_topic);

        var consumed = 0;
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(5);

        while (consumed < maxMessages && DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var result = await consumer.ConsumeAsync(
                    timeout: TimeSpan.FromMilliseconds(500),
                    cancellationToken: cancellationToken);

                if (result?.Value == null) continue;

                var quote = StockQuote.FromJson(result.Value);
                if (quote != null)
                {
                    quotes.Add(quote);
                    consumed++;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return quotes;
    }

    public async Task<(int produced, int consumed, TimeSpan roundtripTime)> RunRoundtripAsync(
        string[] symbols,
        int messageCount)
    {
        var startTime = DateTime.UtcNow;

        // Produce messages
        var produced = await ProduceAsync(symbols, messageCount);

        // Brief delay to ensure messages are available
        await Task.Delay(100);

        // Consume messages
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consumed = await ConsumeAsync(messageCount, cts.Token);

        var roundtripTime = DateTime.UtcNow - startTime;

        return (produced.Count, consumed.Count, roundtripTime);
    }

    public void DisplayInfo()
    {
        AnsiConsole.MarkupLine("[green]Native Surgewave Protocol:[/]");
        AnsiConsole.MarkupLine("  • Optimized binary protocol for maximum performance");
        AnsiConsole.MarkupLine("  • Zero-copy message handling where possible");
        AnsiConsole.MarkupLine("  • Lowest latency option for .NET applications");
        AnsiConsole.MarkupLine("  • Best for: New .NET projects, high-performance scenarios");
        AnsiConsole.WriteLine();
    }
}
