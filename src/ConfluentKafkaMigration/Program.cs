// =============================================================================
// CONFLUENT.KAFKA MIGRATION SAMPLE WITH PERFORMANCE MEASUREMENT
// =============================================================================
//
// This sample demonstrates zero-code migration from Confluent.Kafka to Surgewave
// with built-in performance measurement for comparing protocols.
//
// USAGE:
//   ConfluentKafkaMigration [protocol] [messageCount]
//   protocol:     surgewave | kafka | auto (default: auto)
//   messageCount: number of messages (default: 100)
//
// EXAMPLES:
//   ConfluentKafkaMigration                    # auto protocol, 100 messages
//   ConfluentKafkaMigration surgewave              # surgewave protocol, 100 messages
//   ConfluentKafkaMigration surgewave 10000        # surgewave protocol, 10000 messages
//   ConfluentKafkaMigration kafka 10000        # kafka protocol for comparison
//
// MIGRATION STEPS:
// 1. Replace NuGet package: Confluent.Kafka -> Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka
// 2. That's it! The code is 100% compatible.
// =============================================================================

using System.Diagnostics;
using Confluent.Kafka;  // <-- This namespace is provided by Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka!

Console.WriteLine("=== Surgewave Confluent.Kafka Wrapper Performance Demo ===\n");

// Parse command line
var protocol = args.Length > 0 ? args[0] : "auto";
var messageCount = args.Length > 1 ? int.Parse(args[1]) : 100;

if (protocol != "surgewave" && protocol != "kafka" && protocol != "auto")
{
    Console.WriteLine("Usage: ConfluentKafkaMigration [protocol] [messageCount]");
    Console.WriteLine("  protocol:     surgewave | kafka | auto (default: auto)");
    Console.WriteLine("  messageCount: number of messages (default: 100)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  ConfluentKafkaMigration surgewave 10000");
    Console.WriteLine("  ConfluentKafkaMigration kafka 10000");
    return 1;
}

Console.WriteLine($"Configuration:");
Console.WriteLine($"  Protocol:     {protocol.ToUpperInvariant()}");
Console.WriteLine($"  Messages:     {messageCount:N0}");
Console.WriteLine();

const string brokerAddress = "localhost:9092";
var topicName = $"perf-test-{protocol}-{Guid.NewGuid():N}";
var groupId = $"perf-group-{Guid.NewGuid():N}";

try
{
    // =========================================================================
    // PRODUCER PERFORMANCE TEST
    // =========================================================================
    Console.WriteLine($"Producer Test: Producing {messageCount:N0} messages...");

    var producerConfig = new ProducerConfig
    {
        BootstrapServers = brokerAddress,
        ClientId = "perf-producer",
        Acks = Acks.Leader,
        LingerMs = 0,  // No batching for accurate per-message timing

        // Surgewave EXTENSION: Protocol selection
        SurgewaveProtocol = protocol
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
        ClientId = "perf-consumer",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,

        // Surgewave EXTENSION: Protocol selection
        SurgewaveProtocol = protocol
    };

    sw.Restart();
    int receivedCount = 0;

    using (var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build())
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
    Console.WriteLine($"Protocol:   {protocol.ToUpperInvariant()}");
    Console.WriteLine($"Messages:   {messageCount:N0}");
    Console.WriteLine();
    Console.WriteLine($"Producer:   {produceMs,8:N0} ms  ({produceMsgPerSec,10:N0} msg/sec)");
    Console.WriteLine($"Consumer:   {consumeMs,8:N0} ms  ({consumeMsgPerSec,10:N0} msg/sec)");
    Console.WriteLine();
    Console.WriteLine("To compare protocols, run:");
    Console.WriteLine($"  ConfluentKafkaMigration kafka {messageCount}");
    Console.WriteLine($"  ConfluentKafkaMigration surgewave {messageCount}");
    Console.WriteLine();

    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("\nMake sure Surgewave broker is running on localhost:9092");
    Console.ResetColor();
    return 1;
}
