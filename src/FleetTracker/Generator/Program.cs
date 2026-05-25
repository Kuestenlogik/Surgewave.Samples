using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.FleetTracker.Generator;
using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

// =====================================================================
// FLEET TRACKER -- Vehicle Position Generator
// =====================================================================
// Simulates 20 vehicles moving around Berlin, publishing GPS positions
// to Surgewave at 1Hz. Demonstrates Surgewave's native protocol for real-time
// location streaming with JSON serialization and keyed messages.
// =====================================================================

Console.WriteLine("=== Fleet Tracker - Vehicle Position Generator ===");
Console.WriteLine("Demonstrating Surgewave Native Client API\n");

const string brokerAddress = "localhost:9092";
const string topicName = "fleet-positions";
const int vehicleCount = 20;
const int updateIntervalMs = 1000;

// Berlin city center coordinates
const double berlinCenterLat = 52.52;
const double berlinCenterLon = 13.405;
const double spreadRadius = 0.1; // degrees

Console.WriteLine($"Configuration:");
Console.WriteLine($"  Broker: {brokerAddress}");
Console.WriteLine($"  Topic: {topicName}");
Console.WriteLine($"  Vehicles: {vehicleCount}");
Console.WriteLine($"  Update interval: {updateIntervalMs}ms");
Console.WriteLine();

try
{
    // ============= STEP 1: Connect to Surgewave =============
    // UseSurgewaveProtocol() selects Surgewave's optimized binary protocol (45us P50 latency)
    // instead of the Kafka wire protocol (15ms). For GPS tracking, low latency
    // means positions appear on the dashboard almost instantly.
    Console.WriteLine("Connecting to Surgewave broker...");
    await using var client = await SurgewaveClient.Create(brokerAddress)
        .WithClientId("fleet-generator")
        .UseSurgewaveProtocol()
        .BuildAsync();

    Console.WriteLine($"Connected! Protocol: {client.Protocol}\n");

    // ============= STEP 2: Create Typed Producer =============
    // Generic producer with JSON serialization. Using VehicleId as the message
    // key ensures all positions for the same vehicle go to the same partition,
    // preserving chronological order per vehicle.
    await using var producer = client.CreateProducer<string, VehiclePosition>(options =>
    {
        options.ValueSerializer = Serializers.Json<VehiclePosition>();
    });

    // Initialize vehicle simulators spread around Berlin
    var random = new Random(42);
    var vehicles = new List<VehicleSimulator>();

    for (int i = 0; i < vehicleCount; i++)
    {
        var startLat = berlinCenterLat + (random.NextDouble() - 0.5) * 2 * spreadRadius;
        var startLon = berlinCenterLon + (random.NextDouble() - 0.5) * 2 * spreadRadius;
        var vehicleId = $"vehicle-{i + 1:D3}";
        vehicles.Add(new VehicleSimulator(vehicleId, startLat, startLon, i));
    }

    Console.WriteLine($"Initialized {vehicleCount} vehicles around Berlin.");
    Console.WriteLine("Press Ctrl+C to stop.\n");
    Console.WriteLine("Publishing vehicle positions...\n");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    long messageCount = 0;
    var startTime = DateTime.UtcNow;

    while (!cts.Token.IsCancellationRequested)
    {
        var tickStart = DateTime.UtcNow;

        foreach (var vehicle in vehicles)
        {
            var position = vehicle.Tick();

            try
            {
                var result = await producer.ProduceAsync(
                    topicName,
                    position.VehicleId,
                    position);

                messageCount++;

                if (messageCount % 100 == 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var rate = messageCount / elapsed.TotalSeconds;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Published {messageCount:N0} messages ({rate:F1} msg/s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing: {ex.Message}");
            }
        }

        // Wait for next tick
        var tickElapsed = DateTime.UtcNow - tickStart;
        var delay = updateIntervalMs - (int)tickElapsed.TotalMilliseconds;
        if (delay > 0)
        {
            await Task.Delay(delay, cts.Token);
        }
    }

    Console.WriteLine($"\nStopped. Total messages published: {messageCount:N0}");
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
