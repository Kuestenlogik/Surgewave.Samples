using Confluent.Kafka;
using Spectre.Console;

namespace Kuestenlogik.Surgewave.Samples.MultiProtocol;

/// <summary>
/// Demonstrates producing and consuming messages using the Kafka protocol.
/// Surgewave is wire-compatible with Kafka, so standard Kafka clients work seamlessly.
/// </summary>
public sealed class KafkaProtocolDemo
{
    private readonly string _bootstrapServers;
    private readonly string _topic;

    public KafkaProtocolDemo(string bootstrapServers, string topic)
    {
        _bootstrapServers = bootstrapServers;
        _topic = topic;
    }

    public async Task<List<StockQuote>> ProduceAsync(string[] symbols, int count)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = Acks.All
        };

        var quotes = new List<StockQuote>();

        using var producer = new ProducerBuilder<string, string>(config).Build();

        for (int i = 0; i < count; i++)
        {
            var symbol = symbols[i % symbols.Length];
            var quote = StockQuote.Generate(symbol, "Kafka");
            quotes.Add(quote);

            await producer.ProduceAsync(_topic, new Message<string, string>
            {
                Key = symbol,
                Value = quote.ToJson()
            });
        }

        producer.Flush(TimeSpan.FromSeconds(5));
        return quotes;
    }

    public async Task<List<StockQuote>> ConsumeAsync(int maxMessages, CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"multi-protocol-kafka-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var quotes = new List<StockQuote>();

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_topic);

        var consumed = 0;
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(5);

        while (consumed < maxMessages && DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (result?.Message?.Value == null) continue;

                var quote = StockQuote.FromJson(result.Message.Value);
                if (quote != null)
                {
                    quotes.Add(quote);
                    consumed++;
                }
            }
            catch (ConsumeException)
            {
                // Topic may not exist yet
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
        AnsiConsole.MarkupLine("[cyan]Kafka Protocol:[/]");
        AnsiConsole.MarkupLine("  • Uses standard Confluent.Kafka client library");
        AnsiConsole.MarkupLine("  • Wire-compatible with Apache Kafka");
        AnsiConsole.MarkupLine("  • Supports all Kafka features (consumer groups, transactions, etc.)");
        AnsiConsole.MarkupLine("  • Best for: Existing Kafka applications, cross-language support");
        AnsiConsole.WriteLine();
    }
}
