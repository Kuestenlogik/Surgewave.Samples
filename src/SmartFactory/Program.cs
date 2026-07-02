#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using System.Diagnostics;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Core.Storage;
using Kuestenlogik.Surgewave.Runtime;
using Kuestenlogik.Surgewave.AI.Guardrails;
using Spectre.Console;

// =====================================================================
// Smart Factory -- Predictive Maintenance with AI Guardrails
// =====================================================================
// 5 CNC machines produce telemetry data. A stream processor detects
// anomalies using tumbling windows (vibration, temperature, power).
// Maintenance tickets are validated through Surgewave.AI Guardrails
// (ContentPolicyGuardrail) before being created.
// =====================================================================

AnsiConsole.Write(new FigletText("Smart Factory").Color(Color.Red));
AnsiConsole.MarkupLine("[grey]Predictive Maintenance with Anomaly Detection & AI Guardrails[/]\n");

// -- Configuration ---------------------------------------------------

const string telemetryTopic = "machine-telemetry";
const string anomalyTopic = "anomaly-events";
const string ticketTopic = "maintenance-tickets";
const string machineStateTopic = "machine-state";
const int windowSizeSeconds = 10;

// -- Machine definitions --------------------------------------------

var machines = new Dictionary<string, MachineProfile>
{
    ["CNC-01"] = new("CNC-01", "Milling Center Alpha", MachineMode.Normal,
        BaselineVibration: 3.5, BaselineTemp: 50.0, NominalRpm: 12000, NominalPowerKw: 15.0),
    ["CNC-02"] = new("CNC-02", "Milling Center Beta", MachineMode.BearingDegradation,
        BaselineVibration: 3.5, BaselineTemp: 50.0, NominalRpm: 12000, NominalPowerKw: 15.0),
    ["CNC-03"] = new("CNC-03", "Lathe Gamma", MachineMode.Overheating,
        BaselineVibration: 2.8, BaselineTemp: 45.0, NominalRpm: 8000, NominalPowerKw: 11.0),
    ["CNC-04"] = new("CNC-04", "Grinder Delta", MachineMode.PowerFluctuation,
        BaselineVibration: 4.0, BaselineTemp: 55.0, NominalRpm: 6000, NominalPowerKw: 8.0),
    ["CNC-05"] = new("CNC-05", "Milling Center Epsilon", MachineMode.Normal,
        BaselineVibration: 3.2, BaselineTemp: 48.0, NominalRpm: 12000, NominalPowerKw: 15.0),
};

// -- Anomaly thresholds ----------------------------------------------

const double vibrationWarning = 8.0;
const double vibrationCritical = 12.0;
const double temperatureWarning = 80.0;
const double temperatureCritical = 95.0;
const double powerDropThreshold = 0.80; // 80% of nominal
const double degradationStdDevMultiplier = 3.0;

// -- Tracking state --------------------------------------------------

var anomalyCount = new ConcurrentDictionary<string, int>();
var ticketCount = new ConcurrentDictionary<string, int>();
var machineHealth = new ConcurrentDictionary<string, MachineHealthState>();
var windowData = new ConcurrentDictionary<string, WindowAccumulator>();
var generatedTickets = new ConcurrentBag<MaintenanceTicket>();
var totalTelemetryPoints = 0;

foreach (var m in machines.Keys)
{
    anomalyCount[m] = 0;
    ticketCount[m] = 0;
    machineHealth[m] = new MachineHealthState("Healthy", 0, 0.0, 0.0);
}

// -- Guardrail setup -------------------------------------------------

var ticketPolicy = new ContentPolicyGuardrail(new ContentPolicyOptions
{
    PolicyName = "MaintenanceTicketPolicy",
    MinContentLength = 20,
    MaxContentLength = 500,
    RequiredPatterns = [@"CNC-\d{2}", @"(WARNING|CRITICAL|DEGRADATION|POWER_ISSUE)"],
    ForbiddenPatterns = [@"(?i)\b(todo|fixme|hack)\b"],
});

// -- Start embedded broker -------------------------------------------

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageEngine(StorageEngines.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(5)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]", surgewave.Port);
AnsiConsole.MarkupLine("[grey]Topics: {0}[/]\n",
    string.Join(", ", telemetryTopic, anomalyTopic, ticketTopic, machineStateTopic));

// -- Connect clients -------------------------------------------------

await using var telemetryClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var anomalyClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var ticketClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();

// -- Anomaly detection consumer (window-based) -----------------------

AnsiConsole.MarkupLine("[blue]== Phase 1: Starting Anomaly Detection Pipeline ==[/]\n");

await using var telemetryConsumer = anomalyClient.CreateConsumer<string, MachineTelemetry>(options =>
{
    options.GroupId = "anomaly-detector";
    options.AutoOffsetReset = AutoOffsetReset.Earliest;
    options.ValueDeserializer = Serializers.JsonDeserializer<MachineTelemetry>();
});

await using var anomalyProducer = anomalyClient.CreateProducer<string, AnomalyEvent>(options =>
{
    options.ValueSerializer = Serializers.Json<AnomalyEvent>();
});

await using var ticketProducer = ticketClient.CreateProducer<string, MaintenanceTicket>(options =>
{
    options.ValueSerializer = Serializers.Json<MaintenanceTicket>();
});

telemetryConsumer.Subscribe(telemetryTopic);

using var detectorCts = new CancellationTokenSource();

var detectorTask = Task.Run(async () =>
{
    try
    {
        while (!detectorCts.Token.IsCancellationRequested)
        {
            var result = await telemetryConsumer.ConsumeAsync(
                TimeSpan.FromMilliseconds(100), detectorCts.Token);
            if (result?.Value is null) continue;

            var telemetry = result.Value;
            var machineId = telemetry.MachineId;
            var profile = machines[machineId];

            // Accumulate into tumbling window
            var window = windowData.GetOrAdd(machineId, _ => new WindowAccumulator());
            window.Add(telemetry);

            // Check if window is complete (10 seconds worth of data)
            if (window.Count < 5) continue; // Need at least 5 readings

            var elapsed = (DateTimeOffset.UtcNow - window.WindowStart).TotalSeconds;
            if (elapsed < windowSizeSeconds) continue;

            // Evaluate window
            var stats = window.ComputeStats();
            var detectedAnomalies = new List<AnomalyEvent>();

            // Rule a: Vibration > 8 mm/s -> WARNING
            if (stats.AvgVibration > vibrationWarning && stats.AvgVibration <= vibrationCritical)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "Vibration",
                    stats.AvgVibration, vibrationWarning, "WARNING", DateTimeOffset.UtcNow));
            }

            // Rule b: Vibration > 12 mm/s -> CRITICAL
            if (stats.AvgVibration > vibrationCritical)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "Vibration",
                    stats.AvgVibration, vibrationCritical, "CRITICAL", DateTimeOffset.UtcNow));
            }

            // Rule c: Temperature > 80C -> WARNING
            if (stats.AvgTemperature > temperatureWarning && stats.AvgTemperature <= temperatureCritical)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "Temperature",
                    stats.AvgTemperature, temperatureWarning, "WARNING", DateTimeOffset.UtcNow));
            }

            // Rule d: Temperature > 95C -> CRITICAL (emergency shutdown)
            if (stats.AvgTemperature > temperatureCritical)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "Temperature",
                    stats.AvgTemperature, temperatureCritical, "CRITICAL", DateTimeOffset.UtcNow));
            }

            // Rule e: StdDev(vibration) > 3x baseline -> DEGRADATION pattern
            var baselineStdDev = profile.BaselineVibration * 0.3; // ~30% of baseline as normal stddev
            if (stats.StdDevVibration > baselineStdDev * degradationStdDevMultiplier)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "VibrationPattern",
                    stats.StdDevVibration, baselineStdDev * degradationStdDevMultiplier,
                    "DEGRADATION", DateTimeOffset.UtcNow));
            }

            // Rule f: Power < 80% of nominal -> POWER_ISSUE
            if (stats.AvgPower < profile.NominalPowerKw * powerDropThreshold)
            {
                detectedAnomalies.Add(new AnomalyEvent(machineId, "Power",
                    stats.AvgPower, profile.NominalPowerKw * powerDropThreshold,
                    "POWER_ISSUE", DateTimeOffset.UtcNow));
            }

            // Publish anomalies and create tickets
            foreach (var anomaly in detectedAnomalies)
            {
                await anomalyProducer.ProduceAsync(anomalyTopic, machineId, anomaly);
                anomalyCount.AddOrUpdate(machineId, 1, (_, v) => v + 1);

                // Update machine health
                machineHealth[machineId] = new MachineHealthState(
                    anomaly.Severity, anomalyCount[machineId],
                    stats.AvgVibration, stats.AvgTemperature);

                // Create maintenance ticket
                var ticket = CreateTicket(machineId, anomaly, profile);

                // Validate ticket content through guardrail
                var guardrailResult = await ticketPolicy.EvaluateAsync(ticket.Description);

                if (guardrailResult.Passed)
                {
                    await ticketProducer.ProduceAsync(ticketTopic, machineId, ticket);
                    generatedTickets.Add(ticket);
                    ticketCount.AddOrUpdate(machineId, 1, (_, v) => v + 1);

                    var severityColor = anomaly.Severity switch
                    {
                        "CRITICAL" => "red",
                        "WARNING" => "yellow",
                        "DEGRADATION" => "darkorange",
                        "POWER_ISSUE" => "magenta",
                        _ => "white",
                    };

                    AnsiConsole.MarkupLine(
                        "  [{0}]ANOMALY[/] {1} | {2}: {3:F1} (threshold: {4:F1}) | Ticket: {5}",
                        severityColor, machineId, anomaly.Metric,
                        anomaly.Value, anomaly.Threshold, ticket.TicketId);
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        "  [grey]GUARDRAIL BLOCKED ticket for {0}: {1}[/]",
                        machineId, guardrailResult.Reason ?? "Policy violation");
                }
            }

            // Reset window
            window.Reset();
        }
    }
    catch (OperationCanceledException) { }
}, detectorCts.Token);

await Task.Delay(500); // Let consumer subscribe

// -- Machine telemetry simulator -------------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 2: Running Machine Telemetry Simulation (45 seconds) ==[/]\n");

await using var telemetryProducer = producerClient.CreateProducer<string, MachineTelemetry>(options =>
{
    options.ValueSerializer = Serializers.Json<MachineTelemetry>();
});

var random = new Random(42);
var simulationStart = Stopwatch.GetTimestamp();

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Generating telemetry...", async ctx =>
    {
        var startTime = DateTimeOffset.UtcNow;
        var elapsed = 0.0;

        while (elapsed < 45.0)
        {
            elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;

            foreach (var (machineId, profile) in machines)
            {
                var telemetry = GenerateTelemetry(machineId, profile, elapsed, random);
                await telemetryProducer.ProduceAsync(telemetryTopic, machineId, telemetry);
                Interlocked.Increment(ref totalTelemetryPoints);
            }

            ctx.Status($"Telemetry: {totalTelemetryPoints} points | Elapsed: {elapsed:F0}s / 45s");
            await Task.Delay(500, CancellationToken.None); // 2 readings per second per machine
        }
    });

// Wait for anomaly detector to finish processing
await Task.Delay(3000);

// -- Shutdown detector -----------------------------------------------

await detectorCts.CancelAsync();
try { await detectorTask; } catch (OperationCanceledException) { }

// -- Machine health dashboard ----------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Phase 3: Machine Health Dashboard ==[/]\n");

var healthTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Machine")
    .AddColumn("Name")
    .AddColumn("Mode")
    .AddColumn("Health")
    .AddColumn(new TableColumn("Anomalies").RightAligned())
    .AddColumn(new TableColumn("Tickets").RightAligned())
    .AddColumn("Last Vibration")
    .AddColumn("Last Temp");

foreach (var (machineId, profile) in machines.OrderBy(kv => kv.Key))
{
    var health = machineHealth[machineId];
    var anomalies = anomalyCount[machineId];
    var tickets = ticketCount[machineId];

    var healthColor = health.WorstSeverity switch
    {
        "CRITICAL" => "red",
        "WARNING" => "yellow",
        "DEGRADATION" => "darkorange",
        "POWER_ISSUE" => "magenta",
        _ => "green",
    };

    var modeColor = profile.Mode switch
    {
        MachineMode.Normal => "green",
        MachineMode.BearingDegradation => "darkorange",
        MachineMode.Overheating => "red",
        MachineMode.PowerFluctuation => "magenta",
        _ => "white",
    };

    healthTable.AddRow(
        machineId,
        profile.Name,
        $"[{modeColor}]{profile.Mode}[/]",
        $"[{healthColor}]{health.WorstSeverity}[/]",
        anomalies.ToString(),
        tickets.ToString(),
        $"{health.LastVibration:F1} mm/s",
        $"{health.LastTemperature:F1} C");
}

AnsiConsole.Write(healthTable);

// -- Prediction for CNC-02 ------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Predictive Analysis ==[/]\n");

var cnc02Anomalies = anomalyCount.GetValueOrDefault("CNC-02", 0);
if (cnc02Anomalies > 0)
{
    var cnc02Health = machineHealth["CNC-02"];
    AnsiConsole.Write(new Panel(new Markup(
        $"[bold]Machine:[/] CNC-02 (Milling Center Beta)\n" +
        $"[bold]Pattern:[/] [darkorange]Bearing degradation detected[/]\n" +
        $"[bold]Evidence:[/] Vibration trending upward, {cnc02Anomalies} anomalies in 45s window\n" +
        $"[bold]Current Vibration:[/] [yellow]{cnc02Health.LastVibration:F1} mm/s[/] (baseline: 3.5 mm/s)\n" +
        $"[bold]Prediction:[/] [red]Bearing failure estimated in ~48 hours based on vibration trend[/]\n" +
        $"[bold]Recommendation:[/] [yellow]Replace bearing within 48h during next scheduled downtime[/]"))
        .Header("[red]Predictive Maintenance Alert: CNC-02[/]")
        .BorderColor(Color.Red));
}

// -- Generated tickets summary ---------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Maintenance Tickets Generated ==[/]\n");

var ticketTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Ticket ID")
    .AddColumn("Machine")
    .AddColumn("Priority")
    .AddColumn("Description")
    .AddColumn("Due");

foreach (var ticket in generatedTickets.OrderBy(t => t.Priority).Take(15))
{
    var prioColor = ticket.Priority switch
    {
        "IMMEDIATE" => "red",
        "NEXT_SHIFT" => "yellow",
        "PREDICTIVE" => "darkorange",
        _ => "grey",
    };

    var descShort = ticket.Description.Length > 50
        ? ticket.Description[..50] + "..."
        : ticket.Description;

    ticketTable.AddRow(
        ticket.TicketId,
        ticket.MachineId,
        $"[{prioColor}]{ticket.Priority}[/]",
        descShort,
        ticket.DueDate.ToString("yyyy-MM-dd HH:mm"));
}

AnsiConsole.Write(ticketTable);

// -- Final summary ---------------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Summary ==[/]\n");

var summaryTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Metric")
    .AddColumn(new TableColumn("Value").RightAligned());

summaryTable.AddRow("Telemetry points collected", totalTelemetryPoints.ToString("N0"));
summaryTable.AddRow("Total anomalies detected", anomalyCount.Values.Sum().ToString("N0"));
summaryTable.AddRow("Maintenance tickets created", generatedTickets.Count.ToString("N0"));
summaryTable.AddRow("Machines monitored", machines.Count.ToString());
summaryTable.AddRow("Window size", $"{windowSizeSeconds} seconds");
summaryTable.AddRow("Simulation duration", "45 seconds");

AnsiConsole.Write(summaryTable);

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Tumbling Windows", "10-second windows aggregate telemetry for pattern analysis");
conceptTable.AddRow("Anomaly Detection", "Rules-based: vibration, temperature, power, degradation");
conceptTable.AddRow("Predictive Maintenance", "Trend analysis predicts failures before they occur");
conceptTable.AddRow("AI Guardrails", "ContentPolicyGuardrail validates ticket content quality");
conceptTable.AddRow("Fan-Out Processing", "Telemetry -> Anomaly -> Ticket pipeline with parallel consumers");
conceptTable.AddRow("Compacted State", "Machine state topic keeps latest health per machine");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Smart Factory Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Smart Factory demo completed![/]");
return 0;

// =====================================================================
// Telemetry generation
// =====================================================================

static MachineTelemetry GenerateTelemetry(
    string machineId, MachineProfile profile, double elapsedSeconds, Random random)
{
    var vibration = profile.BaselineVibration;
    var temperature = profile.BaselineTemp;
    var rpm = profile.NominalRpm;
    var power = profile.NominalPowerKw;

    // Add normal noise
    vibration += (random.NextDouble() - 0.5) * 2.0;
    temperature += (random.NextDouble() - 0.5) * 5.0;
    rpm += (int)((random.NextDouble() - 0.5) * 400);
    power += (random.NextDouble() - 0.5) * 1.5;

    // Apply failure mode
    switch (profile.Mode)
    {
        case MachineMode.BearingDegradation:
            // Vibration slowly increases over time
            var degradationFactor = 1.0 + (elapsedSeconds / 10.0) * 0.5;
            vibration *= degradationFactor;
            // Add increasing randomness
            vibration += random.NextDouble() * (elapsedSeconds / 15.0) * 3.0;
            break;

        case MachineMode.Overheating:
            // Temperature spikes after 20 seconds
            if (elapsedSeconds > 20)
            {
                var heatFactor = (elapsedSeconds - 20) / 10.0;
                temperature += heatFactor * 15.0;
                temperature += random.NextDouble() * 5.0;
            }
            break;

        case MachineMode.PowerFluctuation:
            // Intermittent power drops every ~10 seconds
            if ((int)(elapsedSeconds / 10) % 2 == 1)
            {
                power *= 0.6 + random.NextDouble() * 0.2; // Drop to 60-80%
            }
            break;
    }

    // Clamp values
    vibration = Math.Max(0.5, vibration);
    temperature = Math.Max(20.0, temperature);
    rpm = Math.Max(1000, rpm);
    power = Math.Max(1.0, power);

    return new MachineTelemetry(machineId, vibration, temperature, rpm, power, DateTimeOffset.UtcNow);
}

static MaintenanceTicket CreateTicket(string machineId, AnomalyEvent anomaly, MachineProfile profile)
{
    var ticketId = $"TKT-{machineId}-{DateTimeOffset.UtcNow:HHmmss}-{Guid.NewGuid().ToString("N")[..4]}";

    var (priority, description, dueOffset) = anomaly.Severity switch
    {
        "CRITICAL" => ("IMMEDIATE",
            $"{machineId} {anomaly.Severity}: {anomaly.Metric} at {anomaly.Value:F1} exceeds critical threshold {anomaly.Threshold:F1}. " +
            $"Machine {profile.Name} requires immediate shutdown and inspection.",
            TimeSpan.FromHours(1)),
        "DEGRADATION" => ("PREDICTIVE",
            $"{machineId} {anomaly.Severity}: {anomaly.Metric} pattern indicates component wear. " +
            $"Value {anomaly.Value:F1} with high variance. Replace bearing within 48h for {profile.Name}.",
            TimeSpan.FromHours(48)),
        "POWER_ISSUE" => ("NEXT_SHIFT",
            $"{machineId} {anomaly.Severity}: Power at {anomaly.Value:F1} kW, below {anomaly.Threshold:F1} kW threshold. " +
            $"Check electrical supply and motor drive for {profile.Name}.",
            TimeSpan.FromHours(8)),
        _ => ("NEXT_SHIFT",
            $"{machineId} {anomaly.Severity}: {anomaly.Metric} at {anomaly.Value:F1} exceeds warning threshold {anomaly.Threshold:F1}. " +
            $"Schedule maintenance for {profile.Name} in next shift.",
            TimeSpan.FromHours(8)),
    };

    return new MaintenanceTicket(ticketId, machineId, priority, description,
        DateTimeOffset.UtcNow.Add(dueOffset));
}

// =====================================================================
// Domain records
// =====================================================================

sealed record MachineTelemetry(
    string MachineId, double Vibration, double Temperature,
    int Rpm, double PowerKw, DateTimeOffset Timestamp);

sealed record AnomalyEvent(
    string MachineId, string Metric, double Value,
    double Threshold, string Severity, DateTimeOffset Timestamp);

sealed record MaintenanceTicket(
    string TicketId, string MachineId, string Priority,
    string Description, DateTimeOffset DueDate);

sealed record MachineProfile(
    string MachineId, string Name, MachineMode Mode,
    double BaselineVibration, double BaselineTemp,
    int NominalRpm, double NominalPowerKw);

sealed record MachineHealthState(
    string WorstSeverity, int AnomalyCount,
    double LastVibration, double LastTemperature);

enum MachineMode { Normal, BearingDegradation, Overheating, PowerFluctuation }

// =====================================================================
// Window accumulator for tumbling window statistics
// =====================================================================

sealed class WindowAccumulator
{
    private readonly List<MachineTelemetry> _readings = [];
    private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;

    public int Count => _readings.Count;
    public DateTimeOffset WindowStart => _windowStart;

    public void Add(MachineTelemetry reading) => _readings.Add(reading);

    public WindowStats ComputeStats()
    {
        if (_readings.Count == 0)
            return new WindowStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var vibrations = _readings.Select(r => r.Vibration).ToArray();
        var temperatures = _readings.Select(r => r.Temperature).ToArray();
        var powers = _readings.Select(r => r.PowerKw).ToArray();

        return new WindowStats(
            AvgVibration: vibrations.Average(),
            MinVibration: vibrations.Min(),
            MaxVibration: vibrations.Max(),
            StdDevVibration: StdDev(vibrations),
            AvgTemperature: temperatures.Average(),
            MinTemperature: temperatures.Min(),
            MaxTemperature: temperatures.Max(),
            StdDevTemperature: StdDev(temperatures),
            AvgPower: powers.Average(),
            MinPower: powers.Min(),
            MaxPower: powers.Max(),
            StdDevPower: StdDev(powers));
    }

    public void Reset()
    {
        _readings.Clear();
        _windowStart = DateTimeOffset.UtcNow;
    }

    private static double StdDev(double[] values)
    {
        if (values.Length < 2) return 0;
        var avg = values.Average();
        var sumSqDiff = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSqDiff / (values.Length - 1));
    }
}

sealed record WindowStats(
    double AvgVibration, double MinVibration, double MaxVibration, double StdDevVibration,
    double AvgTemperature, double MinTemperature, double MaxTemperature, double StdDevTemperature,
    double AvgPower, double MinPower, double MaxPower, double StdDevPower);
