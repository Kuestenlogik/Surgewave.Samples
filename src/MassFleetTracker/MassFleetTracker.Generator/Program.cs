using System.Diagnostics;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Serialization;
using MassFleetTracker.Generator;
using MassFleetTracker.Shared;

// Parse command-line arguments
var vehicleCount = args.Length > 0 && int.TryParse(args[0], out var vc) ? vc : 100_000;
var partitionCount = args.Length > 1 && int.TryParse(args[1], out var pc) ? pc : Math.Max(1, vehicleCount / 1000);
var updateIntervalMs = args.Length > 2 && int.TryParse(args[2], out var ui) ? ui : 1000;
var brokerAddress = args.Length > 3 ? args[3] : "localhost:9092";

Console.WriteLine("=== MassFleetTracker - Vehicle Simulation ===");
Console.WriteLine("High-throughput Surgewave stress test\n");

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run [vehicleCount] [partitionCount] [updateIntervalMs] [brokerAddress]");
    Console.WriteLine("Example: dotnet run 10000 10 1000 localhost:9092");
    Console.WriteLine();
}

// Configuration
const string topicName = "mass-fleet-positions";
var batchSize = Math.Min(1000, vehicleCount);
var parallelism = Environment.ProcessorCount * 2;

// Berlin area bounds
const double minLat = 52.35, maxLat = 52.65;
const double minLon = 13.1, maxLon = 13.7;

Console.WriteLine("Configuration:");
Console.WriteLine($"  Broker: {brokerAddress}");
Console.WriteLine($"  Topic: {topicName}");
Console.WriteLine($"  Partitions: {partitionCount}");
Console.WriteLine($"  Vehicles: {vehicleCount:N0}");
Console.WriteLine($"  Update interval: {updateIntervalMs}ms");
Console.WriteLine($"  Batch size: {batchSize}");
Console.WriteLine($"  Parallelism: {parallelism}");
Console.WriteLine();

try
{
    Console.Write("Initializing vehicles... ");
    var initSw = Stopwatch.StartNew();

    var vehicles = new VehicleSimulator[vehicleCount];
    var random = new Random(42);

    Parallel.For(0, vehicleCount, i =>
    {
        var lat = minLat + (maxLat - minLat) * ((i % 1000) / 1000.0);
        var lon = minLon + (maxLon - minLon) * ((i / 1000) / 100.0);
        vehicles[i] = new VehicleSimulator(i, lat, lon);
    });

    Console.WriteLine($"done ({initSw.ElapsedMilliseconds}ms)");

    Console.Write("Connecting to Surgewave broker... ");
    await using var client = await SurgewaveClient.Create(brokerAddress)
        .WithClientId("mass-fleet-generator")
        .UseSurgewaveProtocol()
        .BuildAsync();

    Console.WriteLine($"connected ({client.Protocol})");

    // Ensure topic exists with correct partition count
    Console.Write($"Ensuring topic has {partitionCount} partitions... ");
    try
    {
        var nativeClient = client.NativeClient!;
        var topics = await nativeClient.Topics.ListAsync();
        var existingTopic = topics.Find(t => t.Name == topicName);

        if (existingTopic == null)
        {
            // Create topic with 100 partitions
            await nativeClient.Topics.CreateAsync(topicName, partitionCount);
            Console.WriteLine("created");
        }
        else if (existingTopic.PartitionCount < partitionCount)
        {
            // Expand to 100 partitions
            await nativeClient.Topics.CreatePartitionsAsync(topicName, partitionCount);
            Console.WriteLine($"expanded from {existingTopic.PartitionCount} to {partitionCount}");
        }
        else
        {
            Console.WriteLine($"ok ({existingTopic.PartitionCount} partitions)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"warning: {ex.Message}");
    }

    await using var producer = client.CreateProducer<string, VehiclePosition>(options =>
    {
        options.ValueSerializer = Serializers.Json<VehiclePosition>();
    });

    Console.WriteLine("\nStarting simulation. Press Ctrl+C to stop.\n");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // Statistics tracking
    long totalMessages = 0;
    long messagesThisSecond = 0;
    var statsSw = Stopwatch.StartNew();
    var lastStatsTime = statsSw.Elapsed;

    // Latency tracking (ring buffer for P50/P99)
    var latencies = new double[1000];
    var latencyIndex = 0;

    while (!cts.Token.IsCancellationRequested)
    {
        var tickStart = Stopwatch.GetTimestamp();

        // Process vehicles in batches with parallelism
        var batchCount = (vehicleCount + batchSize - 1) / batchSize; // Ceiling division
        await Parallel.ForEachAsync(
            Enumerable.Range(0, batchCount),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism,
                CancellationToken = cts.Token
            },
            async (batchIndex, ct) =>
            {
                var batchStart = batchIndex * batchSize;
                var batchEnd = Math.Min(batchStart + batchSize, vehicleCount);

                for (int i = batchStart; i < batchEnd; i++)
                {
                    var position = vehicles[i].Tick();

                    var sendStart = Stopwatch.GetTimestamp();

                    // Distribute vehicles evenly across partitions: vehicle i goes to partition i % 100
                    var partition = i % partitionCount;
                    await producer.ProduceAsync(
                        topicName,
                        partition,
                        position.VehicleId,
                        position);

                    // Track latency
                    var sendLatencyMs = (Stopwatch.GetTimestamp() - sendStart) * 1000.0 / Stopwatch.Frequency;
                    var idx = Interlocked.Increment(ref latencyIndex) % latencies.Length;
                    latencies[idx] = sendLatencyMs;

                    Interlocked.Increment(ref totalMessages);
                    Interlocked.Increment(ref messagesThisSecond);
                }
            });

        // Print stats every second
        var now = statsSw.Elapsed;
        if ((now - lastStatsTime).TotalSeconds >= 1.0)
        {
            var msgPerSec = Interlocked.Exchange(ref messagesThisSecond, 0);

            // Calculate percentiles
            var sortedLatencies = latencies.Where(l => l > 0).OrderBy(l => l).ToArray();
            var p50 = sortedLatencies.Length > 0 ? sortedLatencies[sortedLatencies.Length / 2] : 0;
            var p99 = sortedLatencies.Length > 0 ? sortedLatencies[(int)(sortedLatencies.Length * 0.99)] : 0;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msgPerSec,8:N0} msg/s | Total: {totalMessages,12:N0} | P50: {p50,6:F2}ms | P99: {p99,6:F2}ms");

            lastStatsTime = now;
        }

        // Maintain tick rate
        var tickElapsed = (Stopwatch.GetTimestamp() - tickStart) * 1000.0 / Stopwatch.Frequency;
        var delay = (int)(updateIntervalMs - tickElapsed);
        if (delay > 0)
        {
            await Task.Delay(delay, cts.Token);
        }
    }

    Console.WriteLine($"\nSimulation stopped. Total messages: {totalMessages:N0}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nShutdown requested.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
    return 1;
}

return 0;
