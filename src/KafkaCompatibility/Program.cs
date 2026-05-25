// =============================================================================
// ORIGINAL CONFLUENT.KAFKA SAMPLE WITH PERFORMANCE MEASUREMENT
// =============================================================================
//
// This sample uses the ORIGINAL Confluent.Kafka NuGet package.
// Compare with samples/ConfluentKafkaMigration to see the Surgewave wrapper.
//
// USAGE:
//   KafkaCompatibility [messageCount]
//   messageCount: number of messages (default: 100)
//
// EXAMPLES:
//   KafkaCompatibility                # 100 messages
//   KafkaCompatibility 10000          # 10000 messages for benchmarking
//
// MIGRATION TO Surgewave:
// 1. Replace package: Confluent.Kafka -> Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka
// 2. Add optional: SurgewaveProtocol = "surgewave" for 345x faster performance
// =============================================================================

using System.Diagnostics;
using Confluent.Kafka;  // Original Confluent.Kafka NuGet package

Console.WriteLine("=== Original Confluent.Kafka Performance Demo ===\n");

// Parse command line
var messageCount = args.Length > 0 ? int.Parse(args[0]) : 100;

Console.WriteLine($"Configuration:");
Console.WriteLine($"  Package:      Confluent.Kafka (original)");
Console.WriteLine($"  Protocol:     Kafka");
Console.WriteLine($"  Messages:     {messageCount:N0}");
Console.WriteLine();

const string brokerAddress = "localhost:9092";
var topicName = $"kafka-compat-{Guid.NewGuid():N}";
var groupId = $"kafka-compat-group-{Guid.NewGuid():N}";

try
{
    // =========================================================================
    // PRODUCER PERFORMANCE TEST
    // =========================================================================
    Console.WriteLine($"Producer Test: Producing {messageCount:N0} messages...");

    var producerConfig = new ProducerConfig
    {
        BootstrapServers = brokerAddress,
        ClientId = "kafka-compat-producer",
        Acks = Acks.Leader,
        LingerMs = 0  // No batching for accurate per-message timing
    };

    var sw = Stopwatch.StartNew();
    long totalBytes = 0;

    using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
    {
        for (int i = 0; i < messageCount; i++)
        {
            var key = $"key-{i}";
            var value = $"Message {i} at {DateTime.Now.Ticks}";
            totalBytes += key.Length + value.Length;

            await producer.ProduceAsync(topicName, new Message<string, string>
            {
                Key = key,
                Value = value
            });

            // Progress indicator for large counts
            if (messageCount >= 1000 && (i + 1) % (messageCount / 10) == 0)
            {
                Console.Write($"\r  Progress: {(i + 1) * 100 / messageCount}%");
            }
        }

        producer.Flush(TimeSpan.FromSeconds(30));
    }

    sw.Stop();
    var produceMs = sw.ElapsedMilliseconds;
    var produceMsgPerSec = messageCount * 1000.0 / produceMs;
    var produceKBPerSec = totalBytes / 1024.0 * 1000.0 / produceMs;

    Console.WriteLine($"\r  Completed in {produceMs:N0} ms");
    Console.WriteLine($"  Throughput: {produceMsgPerSec:N0} msg/sec ({produceKBPerSec:N1} KB/sec)");
    Console.WriteLine();

    // =========================================================================
    // CONSUMER PERFORMANCE TEST
    // =========================================================================
    Console.WriteLine($"Consumer Test: Consuming {messageCount:N0} messages...");

    var consumerConfig = new ConsumerConfig
    {
        BootstrapServers = brokerAddress,
        GroupId = groupId,
        ClientId = "kafka-compat-consumer",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };

    sw.Restart();
    int receivedCount = 0;

    using (var consumer = new ConsumerBuilder<string, string>(consumerConfig)
        .SetErrorHandler((_, e) => Console.WriteLine($"[ERROR] {e.Reason}"))
        .SetPartitionsAssignedHandler((c, p) => Console.WriteLine($"[ASSIGNED] {string.Join(", ", p)}"))
        .Build())
    {
        consumer.Subscribe(topicName);

        while (receivedCount < messageCount)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(10));

            if (result == null)
            {
                Console.WriteLine("  Timeout waiting for messages");
                break;
            }

            if (!result.IsPartitionEOF)
            {
                receivedCount++;

                // Progress indicator
                if (messageCount >= 1000 && receivedCount % (messageCount / 10) == 0)
                {
                    Console.Write($"\r  Progress: {receivedCount * 100 / messageCount}%");
                }
            }
        }

        consumer.Close();
    }

    sw.Stop();
    var consumeMs = sw.ElapsedMilliseconds;
    var consumeMsgPerSec = receivedCount * 1000.0 / consumeMs;

    Console.WriteLine($"\r  Completed in {consumeMs:N0} ms ({receivedCount:N0} messages)");
    Console.WriteLine($"  Throughput: {consumeMsgPerSec:N0} msg/sec");
    Console.WriteLine();

    // =========================================================================
    // RESULTS SUMMARY
    // =========================================================================
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    PERFORMANCE SUMMARY                          ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"Package:    Confluent.Kafka (original)");
    Console.WriteLine($"Protocol:   Kafka");
    Console.WriteLine($"Messages:   {messageCount:N0}");
    Console.WriteLine();
    Console.WriteLine($"Producer:   {produceMs,8:N0} ms  ({produceMsgPerSec,10:N0} msg/sec)");
    Console.WriteLine($"Consumer:   {consumeMs,8:N0} ms  ({consumeMsgPerSec,10:N0} msg/sec)");
    Console.WriteLine();
    Console.WriteLine("To compare with Surgewave wrapper, run:");
    Console.WriteLine($"  ConfluentKafkaMigration surgewave {messageCount}");
    Console.WriteLine($"  ConfluentKafkaMigration kafka {messageCount}");
    Console.WriteLine();

    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("\nMake sure a Kafka-compatible broker is running on localhost:9092");
    Console.ResetColor();
    return 1;
}
