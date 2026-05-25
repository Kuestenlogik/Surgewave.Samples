using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.IotDashboard.Generator;
using Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;
using Spectre.Console;

// =====================================================================
// IOT DASHBOARD -- Sensor Reading Generator
// =====================================================================
// Simulates 20 IoT sensors across 4 locations producing readings
// (temperature, humidity, pressure, CO2, noise) every 500ms.
// Uses Surgewave native protocol for high-frequency sensor ingestion.
// =====================================================================

const string bootstrapServers = "localhost:9092";
const string topicName = "iot-sensors";
const int intervalMs = 500; // Reading interval per sensor

AnsiConsole.Write(new FigletText("IoT Generator").Color(Color.Green));
AnsiConsole.MarkupLine("[grey]Simulating IoT sensor readings[/]\n");

// Define sensors across multiple locations
var locations = new[] { "Building-A", "Building-B", "Warehouse", "Office" };
var sensorTypes = Enum.GetValues<SensorType>();

var simulators = new List<SensorSimulator>();
var seed = 42;

foreach (var location in locations)
{
    foreach (var type in sensorTypes)
    {
        var sensorId = $"{location.ToLowerInvariant()}-{type.ToString().ToLowerInvariant()}-{simulators.Count + 1:D3}";
        simulators.Add(new SensorSimulator(sensorId, type, location, seed++));
    }
}

AnsiConsole.MarkupLine("[yellow]Created {0} virtual sensors across {1} locations[/]", simulators.Count, locations.Length);

// ============= STEP 1: Connect to Surgewave =============
// UseSurgewaveProtocol() selects the native binary protocol for sub-millisecond
// message delivery -- essential for high-frequency sensor data ingestion.
AnsiConsole.MarkupLine("[yellow]Connecting to Surgewave broker...[/]");

ISurgewaveClient client;
try
{
    client = await SurgewaveClient.Create(bootstrapServers)
        .UseSurgewaveProtocol()
        .BuildAsync();

    AnsiConsole.MarkupLine("[green]Connected to Surgewave at {0}[/]\n", bootstrapServers);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Failed to connect: {0}[/]", ex.Message);
    AnsiConsole.MarkupLine("[grey]Make sure Surgewave broker is running: dotnet run --project src/Kuestenlogik.Surgewave.Broker[/]");
    return 1;
}

await using (client)
{
    // ============= STEP 2: Create Typed Producer =============
    // JSON serialization for sensor readings. Using sensorId as the message
    // key ensures all readings for the same sensor go to the same partition,
    // enabling per-sensor ordering and efficient consumer-side aggregation.
    await using var producer = client.CreateProducer<string, SensorReading>(options =>
    {
        options.ValueSerializer = Serializers.Json<SensorReading>();
    });

    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]\n");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var messageCount = 0L;
    var startTime = DateTime.UtcNow;

    // Create a table for live status
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Sensor")
        .AddColumn("Type")
        .AddColumn("Location")
        .AddColumn("Value")
        .AddColumn("Status");

    await AnsiConsole.Live(table)
        .AutoClear(false)
        .StartAsync(async ctx =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                table.Rows.Clear();

                foreach (var simulator in simulators)
                {
                    var reading = simulator.NextReading();

                    // Produce the reading
                    await producer.ProduceAsync(topicName, reading.SensorId, reading);
                    messageCount++;

                    // Check for alerts
                    var severity = SensorThresholds.CheckThreshold(reading.Type, reading.Value);
                    var statusColor = severity switch
                    {
                        AlertSeverity.Critical => "red",
                        AlertSeverity.Warning => "yellow",
                        _ => "green"
                    };
                    var statusText = severity?.ToString() ?? "Normal";

                    table.AddRow(
                        $"[cyan]{reading.SensorId}[/]",
                        reading.Type.ToString(),
                        reading.Location,
                        $"{reading.Value:F2} {reading.Unit}",
                        $"[{statusColor}]{statusText}[/]");
                }

                // Add summary row
                var elapsed = DateTime.UtcNow - startTime;
                var rate = elapsed.TotalSeconds > 0 ? messageCount / elapsed.TotalSeconds : 0;
                table.Caption = new TableTitle(
                    $"[grey]Messages: {messageCount:N0} | Rate: {rate:F1} msg/s | Elapsed: {elapsed:hh\\:mm\\:ss}[/]");

                ctx.Refresh();

                try
                {
                    await Task.Delay(intervalMs, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

    AnsiConsole.MarkupLine("\n[green]Generated {0:N0} sensor readings[/]", messageCount);
}

return 0;
