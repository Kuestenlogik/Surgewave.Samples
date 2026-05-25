using System.Text;
using Kuestenlogik.Surgewave.Api.Grpc.Client;
using Spectre.Console;

namespace Kuestenlogik.Surgewave.Samples.MultiProtocol;

/// <summary>
/// Demonstrates producing and consuming messages using gRPC protocol.
/// Language-agnostic and ideal for polyglot environments.
/// </summary>
public sealed class GrpcProtocolDemo
{
    private readonly string _grpcAddress;
    private readonly string _topic;

    public GrpcProtocolDemo(string grpcAddress, string topic)
    {
        _grpcAddress = grpcAddress;
        _topic = topic;
    }

    public async Task<List<StockQuote>> ProduceAsync(string[] symbols, int count)
    {
        var quotes = new List<StockQuote>();

        await using var producer = new GrpcProducer(_grpcAddress);

        for (int i = 0; i < count; i++)
        {
            var symbol = symbols[i % symbols.Length];
            var quote = StockQuote.Generate(symbol, "gRPC");
            quotes.Add(quote);

            var key = Encoding.UTF8.GetBytes(symbol);
            var value = Encoding.UTF8.GetBytes(quote.ToJson());

            await producer.SendAsync(_topic, value, key);
        }

        return quotes;
    }

    public async Task<List<StockQuote>> ConsumeAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var quotes = new List<StockQuote>();

        await using var consumer = new GrpcConsumer(_grpcAddress);

        var consumed = 0;

        try
        {
            // Use streaming consume
            await foreach (var response in consumer.ConsumeAsync(
                _topic,
                partition: 0,
                offset: 0,
                maxRecords: maxMessages,
                maxWaitMs: 5000,
                cancellationToken: cancellationToken))
            {
                if (response.Records == null) continue;

                foreach (var record in response.Records)
                {
                    if (record.Value == null || record.Value.IsEmpty) continue;

                    var json = Encoding.UTF8.GetString(record.Value.Span);
                    var quote = StockQuote.FromJson(json);
                    if (quote != null)
                    {
                        quotes.Add(quote);
                        consumed++;

                        if (consumed >= maxMessages)
                            return quotes;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unavailable)
        {
            // gRPC server not available
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
        AnsiConsole.MarkupLine("[yellow]gRPC Protocol:[/]");
        AnsiConsole.MarkupLine("  • Language-agnostic via Protocol Buffers");
        AnsiConsole.MarkupLine("  • Supports streaming (unary, server, client, bidirectional)");
        AnsiConsole.MarkupLine("  • Built-in flow control and cancellation");
        AnsiConsole.MarkupLine("  • Best for: Polyglot environments, microservices");
        AnsiConsole.WriteLine();
    }
}
