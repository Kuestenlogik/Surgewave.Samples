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
using Spectre.Console;

// =====================================================================
// Supply Chain State Machine -- Order Tracking from Factory to Customer
// =====================================================================
// Tracks 10 orders through a multi-step supply chain with state
// transitions, failure paths (QA rejection, customs hold, delivery
// failure), and real-time ETA calculation. Demonstrates compacted
// topics for latest state and event-driven state machines.
// =====================================================================

AnsiConsole.Write(new FigletText("Supply Chain").Color(Color.Green));
AnsiConsole.MarkupLine("[grey]State Machine Tracking from Factory to Customer[/]\n");

// -- State definitions -----------------------------------------------

const string eventsTopic = "supply-chain-events";
const string stateTopic = "order-state";
const string alertsTopic = "supply-chain-alerts";

// Average minutes per stage (for ETA calculation)
var stageDurations = new Dictionary<string, int>
{
    ["OrderPlaced"] = 2,
    ["InProduction"] = 5,
    ["QualityCheck"] = 2,
    ["QualityApproved"] = 1,
    ["Packed"] = 2,
    ["PickedUp"] = 1,
    ["InTransit"] = 8,
    ["CustomsClearance"] = 3,
    ["OutForDelivery"] = 4,
    ["Rework"] = 4,
    ["DocumentsSubmitted"] = 2,
    ["Rescheduled"] = 3,
};

// Happy path for ETA calculation
var happyPath = new[] { "OrderPlaced", "InProduction", "QualityCheck", "QualityApproved",
    "Packed", "PickedUp", "InTransit", "CustomsClearance", "OutForDelivery", "Delivered" };

// -- Order scenarios -------------------------------------------------

var orderScenarios = new Dictionary<string, OrderScenario>
{
    ["ORD-001"] = new("Happy path (standard)", false, false, false, false, 1.0),
    ["ORD-002"] = new("Happy path (quick)", false, false, false, false, 0.8),
    ["ORD-003"] = new("Happy path (slow)", false, false, false, false, 1.5),
    ["ORD-004"] = new("Happy path (medium)", false, false, false, false, 1.1),
    ["ORD-005"] = new("Happy path (standard)", false, false, false, false, 1.0),
    ["ORD-006"] = new("QA rejected -> rework", true, false, false, false, 1.0),
    ["ORD-007"] = new("Customs hold -> resubmit", false, true, false, false, 1.0),
    ["ORD-008"] = new("Delivery failed -> rescheduled", false, false, true, false, 1.0),
    ["ORD-009"] = new("Express order", false, false, false, false, 0.4),
    ["ORD-010"] = new("International (extra customs)", false, false, false, true, 1.2),
};

// -- Tracking state --------------------------------------------------

var orderStates = new ConcurrentDictionary<string, OrderState>();
var stageStats = new ConcurrentDictionary<string, StageStatistics>();
var alerts = new ConcurrentBag<string>();
var orderStartTimes = new ConcurrentDictionary<string, DateTimeOffset>();
var orderPaths = new ConcurrentDictionary<string, List<string>>();

foreach (var orderId in orderScenarios.Keys)
{
    orderStates[orderId] = new OrderState(orderId, "Created", 0, []);
    orderPaths[orderId] = [];
}

// -- Start embedded broker -------------------------------------------

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageEngine(StorageEngines.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]", surgewave.Port);
AnsiConsole.MarkupLine("[grey]Topics: {0}[/]\n",
    string.Join(", ", eventsTopic, stateTopic, alertsTopic));

// -- Connect clients -------------------------------------------------

await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var trackerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();

// -- State tracker consumer ------------------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 1: Starting State Tracker ==[/]\n");

await using var stateConsumer = trackerClient.CreateConsumer<string, SupplyChainEvent>(options =>
{
    options.GroupId = "state-tracker";
    options.AutoOffsetReset = AutoOffsetReset.Earliest;
    options.ValueDeserializer = Serializers.JsonDeserializer<SupplyChainEvent>();
});

await using var stateProducer = trackerClient.CreateProducer<string, OrderState>(options =>
{
    options.ValueSerializer = Serializers.Json<OrderState>();
});

stateConsumer.Subscribe(eventsTopic);

using var trackerCts = new CancellationTokenSource();

var trackerTask = Task.Run(async () =>
{
    try
    {
        while (!trackerCts.Token.IsCancellationRequested)
        {
            var result = await stateConsumer.ConsumeAsync(
                TimeSpan.FromMilliseconds(100), trackerCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            var orderId = evt.OrderId;

            // Calculate ETA based on remaining stages
            var etaMinutes = CalculateEta(evt.ToState);

            // Record stage duration statistics
            var stageStat = stageStats.GetOrAdd(evt.FromState, _ => new StageStatistics());
            if (stageDurations.TryGetValue(evt.FromState, out var expectedDuration))
            {
                stageStat.RecordDuration(expectedDuration);
            }

            // Update order state
            var history = orderPaths.GetOrAdd(orderId, _ => []);
            history.Add(evt.ToState);

            var state = new OrderState(orderId, evt.ToState, etaMinutes,
                [.. history]);
            orderStates[orderId] = state;

            // Publish updated state to compacted topic
            await stateProducer.ProduceAsync(stateTopic, orderId, state);

            // Check for delays and generate alerts
            if (evt.ToState is "QualityRejected" or "CustomsHeld" or "DeliveryFailed")
            {
                var alertMsg = $"[red]ALERT[/] {orderId}: {evt.ToState} at {evt.Location} -- {evt.Notes}";
                alerts.Add(alertMsg);
                AnsiConsole.MarkupLine("  {0}", alertMsg);
            }

            // Log state transition
            var stateColor = evt.ToState switch
            {
                "Delivered" => "green",
                "QualityRejected" or "CustomsHeld" or "DeliveryFailed" => "red",
                "Rework" or "DocumentsSubmitted" or "Rescheduled" => "yellow",
                _ => "cyan",
            };

            AnsiConsole.MarkupLine(
                "  [{0}]{1}[/]: {2} -> [{0}]{3}[/] (ETA: {4})",
                stateColor, orderId, evt.FromState, evt.ToState,
                etaMinutes > 0 ? $"{etaMinutes} min" : "Arrived");
        }
    }
    catch (OperationCanceledException) { }
}, trackerCts.Token);

await Task.Delay(500); // Let consumer subscribe

// -- Simulate order lifecycle ----------------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 2: Simulating 10 Order Lifecycles ==[/]\n");

await using var eventProducer = producerClient.CreateProducer<string, SupplyChainEvent>(options =>
{
    options.ValueSerializer = Serializers.Json<SupplyChainEvent>();
});

var random = new Random(42);
var locations = new[] { "Factory-Shenzhen", "QA-Lab", "Warehouse-A", "Carrier-Hub",
    "Frankfurt-Customs", "Distribution-Center", "Last-Mile-Depot", "Customer-Address" };

// Run all orders concurrently
var orderTasks = orderScenarios.Select(kv => SimulateOrderAsync(kv.Key, kv.Value)).ToList();
await Task.WhenAll(orderTasks);

// Wait for tracker to finish processing
await Task.Delay(2000);

// -- Show tracking table ---------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Phase 3: Order Tracking Dashboard ==[/]\n");

var trackingTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Order")
    .AddColumn("Scenario")
    .AddColumn("Current State")
    .AddColumn("Steps")
    .AddColumn("Path");

foreach (var (orderId, scenario) in orderScenarios.OrderBy(kv => kv.Key))
{
    var state = orderStates.GetValueOrDefault(orderId);
    var path = orderPaths.GetValueOrDefault(orderId) ?? [];
    var currentState = state?.CurrentState ?? "Unknown";

    var stateColor = currentState switch
    {
        "Delivered" => "green",
        "QualityRejected" or "CustomsHeld" or "DeliveryFailed" => "red",
        _ => "yellow",
    };

    var pathDisplay = path.Count > 5
        ? string.Join(" -> ", path.Take(3)) + " -> ... -> " + path[^1]
        : string.Join(" -> ", path);

    trackingTable.AddRow(
        orderId,
        scenario.Description,
        $"[{stateColor}]{currentState}[/]",
        path.Count.ToString(),
        pathDisplay.Length > 50 ? pathDisplay[..50] + "..." : pathDisplay);
}

AnsiConsole.Write(trackingTable);

// -- Stage duration statistics ---------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Stage Duration Statistics ==[/]\n");

var stageTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Stage")
    .AddColumn(new TableColumn("Expected (min)").RightAligned())
    .AddColumn(new TableColumn("Transitions").RightAligned());

foreach (var stage in happyPath.Where(s => s != "Delivered"))
{
    var expected = stageDurations.GetValueOrDefault(stage, 0);
    var stats = stageStats.GetValueOrDefault(stage);
    var count = stats?.Count ?? 0;

    stageTable.AddRow(stage, expected.ToString(), count.ToString());
}

AnsiConsole.Write(new Panel(stageTable)
    .Header("[cyan]Stage Transition Statistics[/]")
    .BorderColor(Color.Cyan1));

// -- Alerts summary --------------------------------------------------

if (!alerts.IsEmpty)
{
    AnsiConsole.MarkupLine("\n[blue]== Alerts Generated ==[/]\n");

    foreach (var alert in alerts)
    {
        AnsiConsole.MarkupLine("  {0}", alert);
    }
}

// -- Shutdown --------------------------------------------------------

await trackerCts.CancelAsync();
try { await trackerTask; } catch (OperationCanceledException) { }

// -- Concept summary -------------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Concepts Demonstrated ==[/]\n");

var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("State Machine", "Orders follow defined state transitions with branching paths");
conceptTable.AddRow("Compacted Topic", "Order state topic keeps only latest state per order ID");
conceptTable.AddRow("Event Sourcing", "Full history of state transitions stored in events topic");
conceptTable.AddRow("ETA Prediction", "Remaining time estimated from current stage and averages");
conceptTable.AddRow("Failure Recovery", "QA rejection, customs hold, delivery failure with retry paths");
conceptTable.AddRow("Alert Generation", "Anomalous transitions trigger alerts to a separate topic");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Supply Chain Tracking Concepts[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Supply Chain Tracker demo completed![/]");
AnsiConsole.MarkupLine("[grey]Tracked {0} orders through {1} total state transitions.[/]",
    orderScenarios.Count, orderPaths.Values.Sum(p => p.Count));
return 0;

// =====================================================================
// Order simulation
// =====================================================================

async Task SimulateOrderAsync(string orderId, OrderScenario scenario)
{
    orderStartTimes[orderId] = DateTimeOffset.UtcNow;
    var speedMultiplier = scenario.SpeedMultiplier;

    // Build the state sequence for this order
    var transitions = new List<(string from, string to, string location, string notes)>();

    transitions.Add(("Created", "OrderPlaced", "Online-Portal", "Order confirmed"));
    transitions.Add(("OrderPlaced", "InProduction", "Factory-Shenzhen", "Production started"));
    transitions.Add(("InProduction", "QualityCheck", "QA-Lab", "Batch inspection"));

    if (scenario.QaReject)
    {
        transitions.Add(("QualityCheck", "QualityRejected", "QA-Lab", "Defect found: surface scratch"));
        transitions.Add(("QualityRejected", "Rework", "Factory-Shenzhen", "Rework initiated"));
        transitions.Add(("Rework", "QualityCheck", "QA-Lab", "Re-inspection after rework"));
    }

    transitions.Add(("QualityCheck", "QualityApproved", "QA-Lab", "All checks passed"));
    transitions.Add(("QualityApproved", "Packed", "Warehouse-A", "Package sealed"));
    transitions.Add(("Packed", "PickedUp", "Carrier-Hub", "Collected by carrier"));
    transitions.Add(("PickedUp", "InTransit", "Carrier-Hub", "In transit to destination"));

    if (scenario.CustomsHold || scenario.International)
    {
        transitions.Add(("InTransit", "CustomsClearance", "Frankfurt-Customs", "Arrived at customs"));

        if (scenario.CustomsHold)
        {
            transitions.Add(("CustomsClearance", "CustomsHeld", "Frankfurt-Customs", "Missing documentation"));
            transitions.Add(("CustomsHeld", "DocumentsSubmitted", "Frankfurt-Customs", "Documents resubmitted"));
            transitions.Add(("DocumentsSubmitted", "CustomsClearance", "Frankfurt-Customs", "Re-evaluation"));
        }

        transitions.Add(("CustomsClearance", "OutForDelivery", "Distribution-Center", "Customs cleared"));
    }
    else
    {
        transitions.Add(("InTransit", "OutForDelivery", "Distribution-Center", "Arrived at local depot"));
    }

    if (scenario.DeliveryFail)
    {
        transitions.Add(("OutForDelivery", "DeliveryFailed", "Customer-Address", "Nobody home"));
        transitions.Add(("DeliveryFailed", "Rescheduled", "Last-Mile-Depot", "Rescheduled for next day"));
        transitions.Add(("Rescheduled", "OutForDelivery", "Last-Mile-Depot", "Second attempt"));
    }

    transitions.Add(("OutForDelivery", "Delivered", "Customer-Address", "Signed by recipient"));

    // Emit events with realistic delays
    foreach (var (from, to, location, notes) in transitions)
    {
        var baseDelay = stageDurations.GetValueOrDefault(from, 1);
        var delayMs = (int)(baseDelay * 100 * speedMultiplier) + random.Next(50, 200);
        await Task.Delay(delayMs, CancellationToken.None);

        var evt = new SupplyChainEvent(orderId, from, to, location,
            DateTimeOffset.UtcNow, notes);
        await eventProducer.ProduceAsync(eventsTopic, orderId, evt);
    }
}

int CalculateEta(string currentState)
{
    var stateIndex = Array.IndexOf(happyPath, currentState);
    if (stateIndex < 0 || currentState == "Delivered") return 0;

    var remaining = 0;
    for (var i = stateIndex; i < happyPath.Length - 1; i++)
    {
        remaining += stageDurations.GetValueOrDefault(happyPath[i], 2);
    }

    return remaining;
}

// =====================================================================
// Domain records
// =====================================================================

sealed record SupplyChainEvent(
    string OrderId, string FromState, string ToState,
    string Location, DateTimeOffset Timestamp, string Notes);

sealed record OrderState(
    string OrderId, string CurrentState, int EtaMinutes, string[] History);

sealed record OrderScenario(
    string Description, bool QaReject, bool CustomsHold,
    bool DeliveryFail, bool International, double SpeedMultiplier);

sealed class StageStatistics
{
    private int _count;
    private long _totalDuration;

    public int Count => _count;

    public void RecordDuration(int durationMinutes)
    {
        Interlocked.Increment(ref _count);
        Interlocked.Add(ref _totalDuration, durationMinutes);
    }
}
