using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;

// =====================================================================
// DIGITAL TWIN -- Equipment Telemetry Generator
// =====================================================================
// Simulates 20 industrial equipment units (pumps, motors, conveyors,
// compressors) producing continuous telemetry and discrete events.
// Uses Surgewave's native protocol for high-frequency sensor data streaming.
// =====================================================================

Console.WriteLine("=== Digital Twin - Equipment Telemetry Generator ===");
Console.WriteLine("Demonstrating Industrial IoT with Surgewave\n");

const string brokerAddress = "localhost:9092";
const string telemetryTopic = "digitaltwin-telemetry";
const string eventsTopic = "digitaltwin-events";
const int telemetryIntervalMs = 500;

Console.WriteLine("Configuration:");
Console.WriteLine($"  Broker: {brokerAddress}");
Console.WriteLine($"  Telemetry topic: {telemetryTopic}");
Console.WriteLine($"  Events topic: {eventsTopic}");
Console.WriteLine($"  Telemetry interval: {telemetryIntervalMs}ms");
Console.WriteLine();

try
{
    // Initialize equipment with 3D positions for factory floor
    var equipmentList = InitializeEquipment();
    Console.WriteLine($"Initialized {equipmentList.Count} equipment units across 3 zones.");
    
    // Create simulators for each equipment
    var simulators = equipmentList.Select(CreateSimulator).ToList();
    
    // ============= STEP 1: Connect to Surgewave =============
    // UseSurgewaveProtocol() selects the native binary protocol for sub-millisecond
    // latency -- critical for high-frequency telemetry (500ms per equipment).
    Console.WriteLine("\nConnecting to Surgewave broker...");
    await using var client = await SurgewaveClient.Create(brokerAddress)
        .WithClientId("digitaltwin-generator")
        .UseSurgewaveProtocol()
        .BuildAsync();
    
    Console.WriteLine($"Connected! Protocol: {client.Protocol}\n");
    
    // ============= STEP 2: Create Typed Producers =============
    // Separate producers for telemetry (continuous) and events (discrete).
    // JSON serialization provides human-readable messages and schema flexibility.
    await using var telemetryProducer = client.CreateProducer<string, TelemetryReading>(options =>
    {
        options.ValueSerializer = Serializers.Json<TelemetryReading>();
    });

    // Events use polymorphic JSON serialization -- EquipmentStartedEvent,
    // FaultDetected, etc. all serialize through the same producer.
    await using var eventProducer = client.CreateProducer<string, EquipmentEvent>(options =>
    {
        options.ValueSerializer = Serializers.Json<EquipmentEvent>();
    });
    
    Console.WriteLine("Press Ctrl+C to stop.\n");
    Console.WriteLine("Publishing telemetry...\n");
    
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    long telemetryCount = 0;
    long eventCount = 0;
    var startTime = DateTime.UtcNow;
    
    // ============= STEP 3: Initialize Equipment State =============
    // Start all equipment in running mode by publishing start events.
    // Using equipment ID as the message key ensures all events for the
    // same equipment land in the same partition, preserving order.
    foreach (var sim in simulators)
    {
        var startEvent = new EquipmentStartedEvent
        {
            EventId = Guid.NewGuid().ToString("N")[..8],
            EquipmentId = sim.Equipment.Id,
            Timestamp = DateTime.UtcNow,
            PreviousMode = OperatingMode.Stopped
        };
        sim.ApplyEvent(startEvent);
        await eventProducer.ProduceAsync(eventsTopic, sim.Equipment.Id, startEvent);
        eventCount++;
    }
    
    while (!cts.Token.IsCancellationRequested)
    {
        var tickStart = DateTime.UtcNow;
        
        foreach (var sim in simulators)
        {
            // Generate and publish telemetry
            var telemetry = sim.GenerateTelemetry();
            await telemetryProducer.ProduceAsync(telemetryTopic, sim.Equipment.Id, telemetry);
            telemetryCount++;
            
            // Try to generate events (probabilistic)
            var evt = sim.TryGenerateEvent();
            if (evt != null)
            {
                sim.ApplyEvent(evt);
                await eventProducer.ProduceAsync(eventsTopic, sim.Equipment.Id, evt);
                eventCount++;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Event: {evt.GetType().Name} for {sim.Equipment.Id}");
            }
        }
        
        if (telemetryCount % 200 == 0)
        {
            var elapsed = DateTime.UtcNow - startTime;
            var rate = telemetryCount / elapsed.TotalSeconds;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Telemetry: {telemetryCount:N0} ({rate:F1}/s), Events: {eventCount:N0}");
        }
        
        // Wait for next tick
        var tickElapsed = DateTime.UtcNow - tickStart;
        var delay = telemetryIntervalMs - (int)tickElapsed.TotalMilliseconds;
        if (delay > 0)
        {
            await Task.Delay(delay, cts.Token);
        }
    }
    
    Console.WriteLine($"\nStopped. Telemetry: {telemetryCount:N0}, Events: {eventCount:N0}");
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

// Initialize factory equipment across 3 zones
static List<Equipment> InitializeEquipment()
{
    var equipment = new List<Equipment>();
    
    // Zone A - Pumping station (6 pumps)
    for (int i = 1; i <= 6; i++)
    {
        equipment.Add(new Equipment
        {
            Id = $"P-{i:D3}",
            Name = $"Pump {i}",
            Type = EquipmentType.Pump,
            Zone = "A",
            PositionX = -8 + (i - 1) * 3,
            PositionY = 0,
            PositionZ = -6
        });
    }
    
    // Zone B - Motor drive area (6 motors)
    for (int i = 1; i <= 6; i++)
    {
        equipment.Add(new Equipment
        {
            Id = $"M-{i:D3}",
            Name = $"Motor {i}",
            Type = EquipmentType.Motor,
            Zone = "B",
            PositionX = -8 + (i - 1) * 3,
            PositionY = 0,
            PositionZ = 0
        });
    }
    
    // Zone B - Conveyors (4 conveyors)
    for (int i = 1; i <= 4; i++)
    {
        equipment.Add(new Equipment
        {
            Id = $"C-{i:D3}",
            Name = $"Conveyor {i}",
            Type = EquipmentType.Conveyor,
            Zone = "B",
            PositionX = -6 + (i - 1) * 4,
            PositionY = 0,
            PositionZ = 6
        });
    }
    
    // Zone C - Compressor room (4 compressors)
    for (int i = 1; i <= 4; i++)
    {
        equipment.Add(new Equipment
        {
            Id = $"K-{i:D3}",
            Name = $"Compressor {i}",
            Type = EquipmentType.Compressor,
            Zone = "C",
            PositionX = 10,
            PositionY = 0,
            PositionZ = -4 + (i - 1) * 3
        });
    }
    
    return equipment;
}

static EquipmentSimulator CreateSimulator(Equipment equipment) => equipment.Type switch
{
    EquipmentType.Pump => new PumpSimulator(equipment),
    EquipmentType.Motor => new MotorSimulator(equipment),
    EquipmentType.Conveyor => new ConveyorSimulator(equipment),
    EquipmentType.Compressor => new CompressorSimulator(equipment),
    _ => throw new ArgumentException($"Unknown equipment type: {equipment.Type}")
};
