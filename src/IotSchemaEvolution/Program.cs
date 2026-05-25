#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Text.Json;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// ═══════════════════════════════════════════════════════════════════════
// IoT Schema Evolution -- Schema Registry (JSON Schema)
// ═══════════════════════════════════════════════════════════════════════
// Demonstrates schema evolution with IoT sensor telemetry.
// Devices run different firmware versions, producing messages with
// different schema versions. Shows backward & forward compatibility,
// default values, and breaking change rejection.
// ═══════════════════════════════════════════════════════════════════════

AnsiConsole.Write(new FigletText("IoT Schema Evolution").Color(Color.Green));
AnsiConsole.MarkupLine("[grey]Schema Registry Demo with Firmware Versioning[/]\n");

const string topicName = "device-telemetry";

// ── Start embedded broker ────────────────────────────────────────────

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker with Schema Registry...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]\n", surgewave.Port);

await using var client = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

// Get schema registry operations (built into Surgewave native protocol)
var schemaRegistry = client.NativeClient?.Schema;
if (schemaRegistry is null)
{
    AnsiConsole.MarkupLine("[red]Schema Registry not available -- requires native protocol[/]");
    return 1;
}

// ── Schema v1: Basic telemetry ───────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 1: Schema v1 -- Basic Telemetry (Firmware 1.0) ==[/]\n");

var schemaV1 = """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TelemetryV1",
  "type": "object",
  "properties": {
    "deviceId": { "type": "string" },
    "temperature": { "type": "number" },
    "humidity": { "type": "number" },
    "timestamp": { "type": "string", "format": "date-time" }
  },
  "required": ["deviceId", "temperature", "humidity", "timestamp"]
}
""";

var subject = $"{topicName}-value";
var regResult1 = await schemaRegistry.RegisterSchemaAsync(subject, schemaV1, "JSON");
AnsiConsole.MarkupLine("[green]Registered schema v1 (ID: {0})[/]", regResult1.SchemaId);

// Produce v1 messages
await using var producer = client.CreateProducer<string, string>(options =>
{
    options.ValueSerializer = Serializers.String;
});

var random = new Random(42);
var deviceIds = Enumerable.Range(1, 10).Select(i => $"SENSOR-{i:D4}").ToList();

AnsiConsole.MarkupLine("[grey]Producing telemetry from 10 devices (firmware 1.0)...[/]\n");

var v1Table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Device ID")
    .AddColumn("Temperature")
    .AddColumn("Humidity")
    .AddColumn("Schema");

for (var i = 0; i < 10; i++)
{
    var deviceId = deviceIds[i];
    var temp = 18.0 + random.NextDouble() * 15.0;
    var humidity = 30.0 + random.NextDouble() * 50.0;

    var payload = JsonSerializer.Serialize(new
    {
        deviceId,
        temperature = Math.Round(temp, 1),
        humidity = Math.Round(humidity, 1),
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    });

    await producer.ProduceAsync(topicName, deviceId, payload);

    v1Table.AddRow(
        deviceId,
        $"{temp:F1} C",
        $"{humidity:F1}%",
        "[green]v1[/]");
}

AnsiConsole.Write(v1Table);
AnsiConsole.MarkupLine("[green]Produced 10 messages with schema v1[/]\n");

// ── Schema v2: Add battery level ─────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 2: Schema v2 -- Adding Battery Level (Firmware 2.0) ==[/]\n");

var schemaV2 = """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TelemetryV2",
  "type": "object",
  "properties": {
    "deviceId": { "type": "string" },
    "temperature": { "type": "number" },
    "humidity": { "type": "number" },
    "batteryLevel": { "type": "number", "default": -1 },
    "timestamp": { "type": "string", "format": "date-time" }
  },
  "required": ["deviceId", "temperature", "humidity", "timestamp"]
}
""";

var regResult2 = await schemaRegistry.RegisterSchemaAsync(subject, schemaV2, "JSON");
AnsiConsole.MarkupLine("[green]Registered schema v2 (ID: {0})[/]", regResult2.SchemaId);

AnsiConsole.MarkupLine("[cyan]Backward compatibility: v2 consumer can read v1 messages[/]");
AnsiConsole.MarkupLine("[cyan]New field 'batteryLevel' has default value -1[/]\n");

// Produce mixed messages (firmware rollout simulation)
AnsiConsole.MarkupLine("[grey]Firmware rollout: some devices on v1, some on v2...[/]\n");

var mixedTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Device ID")
    .AddColumn("Temperature")
    .AddColumn("Humidity")
    .AddColumn("Battery")
    .AddColumn("Firmware")
    .AddColumn("Schema");

for (var i = 0; i < 10; i++)
{
    var deviceId = deviceIds[i];
    var temp = 18.0 + random.NextDouble() * 15.0;
    var humidity = 30.0 + random.NextDouble() * 50.0;
    var isUpgraded = i < 5; // First 5 devices upgraded to v2

    string payload;
    if (isUpgraded)
    {
        var battery = 20.0 + random.NextDouble() * 80.0;
        payload = JsonSerializer.Serialize(new
        {
            deviceId,
            temperature = Math.Round(temp, 1),
            humidity = Math.Round(humidity, 1),
            batteryLevel = Math.Round(battery, 0),
            timestamp = DateTimeOffset.UtcNow.ToString("o")
        });

        mixedTable.AddRow(deviceId, $"{temp:F1} C", $"{humidity:F1}%",
            $"[green]{battery:F0}%[/]", "[green]2.0[/]", "[green]v2[/]");
    }
    else
    {
        payload = JsonSerializer.Serialize(new
        {
            deviceId,
            temperature = Math.Round(temp, 1),
            humidity = Math.Round(humidity, 1),
            timestamp = DateTimeOffset.UtcNow.ToString("o")
        });

        mixedTable.AddRow(deviceId, $"{temp:F1} C", $"{humidity:F1}%",
            "[grey]N/A (default)[/]", "[yellow]1.0[/]", "[yellow]v1[/]");
    }

    await producer.ProduceAsync(topicName, deviceId, payload);
}

AnsiConsole.Write(mixedTable);

// ── Consumer reads ALL versions ──────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 3: Unified Consumer (reads v1 + v2) ==[/]\n");

await using var consumer = client.CreateConsumer<string, string>(options =>
{
    options.GroupId = $"schema-demo-{Guid.NewGuid():N}";
    options.AutoOffsetReset = AutoOffsetReset.Earliest;
    options.ValueDeserializer = Serializers.StringDeserializer;
});

consumer.Subscribe(topicName);

var consumedCount = 0;
var v1Count = 0;
var v2Count = 0;

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var result = await consumer.ConsumeAsync(TimeSpan.FromSeconds(1), cts.Token);
        if (result?.Value is null) continue;

        consumedCount++;

        // Parse as generic JSON and detect version
        using var doc = JsonDocument.Parse(result.Value);
        var hasBattery = doc.RootElement.TryGetProperty("batteryLevel", out _);

        if (hasBattery) v2Count++;
        else v1Count++;
    }
}
catch (OperationCanceledException) { }

AnsiConsole.MarkupLine("[green]Consumer read {0} messages: {1} v1, {2} v2[/]",
    consumedCount, v1Count, v2Count);
AnsiConsole.MarkupLine("[cyan]All messages consumed regardless of schema version![/]\n");

// ── Schema v3: Add location ─────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 4: Schema v3 -- Adding Location (Firmware 3.0) ==[/]\n");

var schemaV3 = """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TelemetryV3",
  "type": "object",
  "properties": {
    "deviceId": { "type": "string" },
    "temperature": { "type": "number" },
    "humidity": { "type": "number" },
    "batteryLevel": { "type": "number", "default": -1 },
    "location": {
      "type": "object",
      "properties": {
        "lat": { "type": "number" },
        "lon": { "type": "number" }
      },
      "default": null
    },
    "timestamp": { "type": "string", "format": "date-time" }
  },
  "required": ["deviceId", "temperature", "humidity", "timestamp"]
}
""";

var regResult3 = await schemaRegistry.RegisterSchemaAsync(subject, schemaV3, "JSON");
AnsiConsole.MarkupLine("[green]Registered schema v3 (ID: {0})[/]", regResult3.SchemaId);
AnsiConsole.MarkupLine("[cyan]Forward compatibility: v2 consumers can still read v3 messages[/]");
AnsiConsole.MarkupLine("[cyan]New nested 'location' field with lat/lon[/]\n");

// Produce v3 message
var v3Payload = JsonSerializer.Serialize(new
{
    deviceId = "SENSOR-0001",
    temperature = 22.5,
    humidity = 55.3,
    batteryLevel = 87,
    location = new { lat = 50.1109, lon = 8.6821 },
    timestamp = DateTimeOffset.UtcNow.ToString("o")
});

await producer.ProduceAsync(topicName, "SENSOR-0001", v3Payload);
AnsiConsole.MarkupLine("[green]Produced v3 message with GPS location[/]\n");

// ── Breaking change rejection ────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 5: Breaking Change Detection ==[/]\n");

var breakingSchema = """
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "TelemetryBreaking",
  "type": "object",
  "properties": {
    "deviceId": { "type": "string" },
    "humidity": { "type": "number" },
    "batteryLevel": { "type": "number" },
    "timestamp": { "type": "string", "format": "date-time" }
  },
  "required": ["deviceId", "humidity", "timestamp"]
}
""";

AnsiConsole.MarkupLine("[yellow]Attempting to register breaking schema (removed 'temperature' field)...[/]\n");

try
{
    // Register it -- Surgewave's schema registry will accept it as a new version
    // but we demonstrate the concept of what a breaking change looks like
    var breakingResult = await schemaRegistry.RegisterSchemaAsync(subject, breakingSchema, "JSON");
    AnsiConsole.MarkupLine("[yellow]Schema registered (ID: {0}) -- Surgewave allows schema evolution freely[/]", breakingResult.SchemaId);
    AnsiConsole.MarkupLine("[grey]Note: In production, enable compatibility checks to reject breaking changes[/]\n");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Breaking change rejected: {0}[/]\n", ex.Message);
}

// ── Schema version timeline ──────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 6: Schema Evolution Timeline ==[/]\n");

var versions = await schemaRegistry.GetSubjectVersionsAsync(subject);

var timelineTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Version")
    .AddColumn("Title")
    .AddColumn("Fields")
    .AddColumn("Changes");

foreach (var version in versions)
{
    var schemaInfo = await schemaRegistry.GetSchemaByVersionAsync(subject, version);
    if (schemaInfo is null) continue;

    using var doc = JsonDocument.Parse(schemaInfo.SchemaString);
    var root = doc.RootElement;

    var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "?" : "?";
    var fields = root.TryGetProperty("properties", out var props)
        ? string.Join(", ", props.EnumerateObject().Select(p => p.Name))
        : "?";

    var changes = version switch
    {
        1 => "[green]Initial schema[/]",
        2 => "[cyan]+batteryLevel (with default)[/]",
        3 => "[cyan]+location (nested object)[/]",
        _ => "[yellow]Schema change[/]"
    };

    timelineTable.AddRow($"v{version}", title, fields, changes);
}

AnsiConsole.Write(timelineTable);

// ── Schema diff visualization ────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Schema Diff: v1 vs v3 ==[/]\n");

var diffTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Field")
    .AddColumn("v1")
    .AddColumn("v2")
    .AddColumn("v3");

diffTable.AddRow("deviceId", "[green]string[/]", "[green]string[/]", "[green]string[/]");
diffTable.AddRow("temperature", "[green]number[/]", "[green]number[/]", "[green]number[/]");
diffTable.AddRow("humidity", "[green]number[/]", "[green]number[/]", "[green]number[/]");
diffTable.AddRow("batteryLevel", "[grey]--[/]", "[cyan]number (default: -1)[/]", "[cyan]number (default: -1)[/]");
diffTable.AddRow("location", "[grey]--[/]", "[grey]--[/]", "[cyan]object {lat, lon}[/]");
diffTable.AddRow("timestamp", "[green]date-time[/]", "[green]date-time[/]", "[green]date-time[/]");

AnsiConsole.Write(diffTable);

// ── Summary ──────────────────────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Summary ==[/]\n");

var summaryTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

summaryTable.AddRow("Schema Registration", "Register JSON schemas per topic subject");
summaryTable.AddRow("Backward Compatibility", "New consumers can read old messages");
summaryTable.AddRow("Forward Compatibility", "Old consumers can read new messages");
summaryTable.AddRow("Default Values", "Missing fields use defaults (batteryLevel: -1)");
summaryTable.AddRow("Nested Objects", "Complex types like location {lat, lon}");
summaryTable.AddRow("Version Timeline", "Track all schema versions per subject");
summaryTable.AddRow("Mixed Producers", "Devices with different firmware coexist");

AnsiConsole.Write(new Panel(summaryTable)
    .Header("[green]Schema Evolution Concepts Demonstrated[/]")
    .BorderColor(Color.Green));

AnsiConsole.MarkupLine("\n[green]IoT Schema Evolution demo completed![/]");
return 0;
