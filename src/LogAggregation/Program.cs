#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Core.Storage;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// ═══════════════════════════════════════════════════════════════════════
// DevOps Log Aggregation -- Consumer Groups & Rebalancing
// ═══════════════════════════════════════════════════════════════════════
// 3 workers process application logs in parallel via the same consumer
// group. Demonstrates dynamic scaling: crash a worker, add a new one,
// and watch partition rebalancing in action.
// ═══════════════════════════════════════════════════════════════════════

AnsiConsole.Write(new FigletText("Log Aggregation").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Consumer Group Rebalancing Demo[/]\n");

// ── Records ──────────────────────────────────────────────────────────

const string topicName = "application-logs";
const string groupId = "log-processors";
const int partitionCount = 6;

var services = new[] { "api-gateway", "user-service", "order-service" };
var levels = new[] { "DEBUG", "INFO", "INFO", "INFO", "WARN", "ERROR" }; // weighted
var messageTemplates = new Dictionary<string, string[]>
{
    ["api-gateway"] =
    [
        "GET /api/users 200 12ms",
        "POST /api/orders 201 45ms",
        "GET /api/products 200 8ms",
        "PUT /api/users/123 403 3ms",
        "DELETE /api/sessions 204 5ms",
        "GET /api/health 200 1ms",
        "POST /api/auth/login 200 120ms",
        "GET /api/orders?page=2 200 34ms"
    ],
    ["user-service"] =
    [
        "User created: user-4521",
        "Password reset requested for user-1234",
        "Session expired for user-789",
        "Profile updated: user-3210",
        "Login attempt failed: invalid credentials",
        "Email verification sent to user-5678",
        "Account locked after 5 failed attempts"
    ],
    ["order-service"] =
    [
        "Order ORD-001234 placed successfully",
        "Payment processed for ORD-001230",
        "Inventory check failed for SKU-X99",
        "Shipping label generated for ORD-001228",
        "Refund initiated for ORD-001100",
        "Order ORD-001235 moved to fulfillment",
        "Database connection pool exhausted"
    ]
};

// ── Worker state tracking ────────────────────────────────────────────

var workerStats = new ConcurrentDictionary<int, WorkerStats>();
var workerCts = new ConcurrentDictionary<int, CancellationTokenSource>();
var workerTasks = new ConcurrentDictionary<int, Task>();

// ── Start embedded broker ────────────────────────────────────────────

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageEngine(StorageEngines.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(partitionCount)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]", surgewave.Port);
AnsiConsole.MarkupLine("[grey]Topic '{0}' with {1} partitions[/]\n", topicName, partitionCount);

// ── Start log producer ───────────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 1: Starting 3 Workers + Log Producer ==[/]\n");

await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

var totalProduced = 0;
using var producerCts = new CancellationTokenSource();

var producerTask = Task.Run(async () =>
{
    await using var producer = producerClient.CreateProducer<string, LogEntry>(options =>
    {
        options.ValueSerializer = Serializers.Json<LogEntry>();
    });

    var random = new Random(42);

    try
    {
        while (!producerCts.Token.IsCancellationRequested)
        {
            var service = services[random.Next(services.Length)];
            var level = levels[random.Next(levels.Length)];
            var templates = messageTemplates[service];
            var message = templates[random.Next(templates.Length)];

            var log = new LogEntry(
                DateTimeOffset.UtcNow,
                service,
                level,
                message);

            // Partition key = service name (logs from same service go to same partition)
            await producer.ProduceAsync(topicName, service, log);
            Interlocked.Increment(ref totalProduced);

            await Task.Delay(random.Next(20, 80), producerCts.Token);
        }
    }
    catch (OperationCanceledException) { }
}, producerCts.Token);

// ── Start 3 workers ──────────────────────────────────────────────────

for (var i = 1; i <= 3; i++)
{
    await StartWorkerAsync(i);
    await Task.Delay(500); // Stagger start to show assignment
}

// Let workers process for a bit
AnsiConsole.MarkupLine("[grey]Workers processing logs...[/]\n");
await Task.Delay(8000);
ShowWorkerStatus();

// ── Phase 2: Crash worker 3 ─────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 2: Simulating Worker 3 Crash ==[/]\n");
AnsiConsole.MarkupLine("[red]Worker 3 crashed! Partitions will be redistributed...[/]\n");

await StopWorkerAsync(3);
await Task.Delay(5000); // Wait for rebalancing

ShowWorkerStatus();

// ── Phase 3: Scale up with worker 4 ─────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 3: Scaling Up -- Adding Worker 4 ==[/]\n");
AnsiConsole.MarkupLine("[green]Worker 4 joining consumer group...[/]\n");

await StartWorkerAsync(4);
await Task.Delay(5000); // Wait for rebalancing

ShowWorkerStatus();

// ── Phase 4: Let it run and collect final stats ──────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 4: Steady State Processing ==[/]\n");
AnsiConsole.MarkupLine("[grey]Processing for 5 more seconds...[/]\n");
await Task.Delay(5000);

// ── Shutdown ─────────────────────────────────────────────────────────

await producerCts.CancelAsync();
try { await producerTask; }
catch (OperationCanceledException) { }

foreach (var workerId in workerCts.Keys.ToList())
{
    await StopWorkerAsync(workerId);
}

// ── Final summary ────────────────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Final Summary ==[/]\n");

var summaryTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Metric")
    .AddColumn(new TableColumn("Value").RightAligned());

var allStats = workerStats.Values.ToList();
var totalProcessed = allStats.Sum(s => s.TotalMessages);
var totalErrors = allStats.Sum(s => s.GetCount("ERROR"));
var totalWarnings = allStats.Sum(s => s.GetCount("WARN"));

summaryTable.AddRow("Total logs produced", totalProduced.ToString("N0"));
summaryTable.AddRow("Total logs processed", totalProcessed.ToString("N0"));
summaryTable.AddRow("[red]ERROR count[/]", totalErrors.ToString("N0"));
summaryTable.AddRow("[yellow]WARN count[/]", totalWarnings.ToString("N0"));
summaryTable.AddRow("Workers used", workerStats.Count.ToString());

AnsiConsole.Write(summaryTable);

// Distribution across workers
var distTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Worker")
    .AddColumn(new TableColumn("Messages").RightAligned())
    .AddColumn(new TableColumn("Share").RightAligned())
    .AddColumn("Status");

foreach (var (workerId, stats) in workerStats.OrderBy(kv => kv.Key))
{
    var share = totalProcessed > 0 ? (double)stats.TotalMessages / totalProcessed * 100 : 0;
    var status = workerCts.ContainsKey(workerId) ? "[green]Active[/]" : "[red]Stopped[/]";

    distTable.AddRow(
        $"Worker {workerId}",
        stats.TotalMessages.ToString("N0"),
        $"{share:F1}%",
        status);
}

AnsiConsole.Write(new Panel(distTable)
    .Header("[cyan]Work Distribution[/]")
    .BorderColor(Color.Cyan1));

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Consumer Groups", "Multiple consumers share topic partitions");
conceptTable.AddRow("Partition Assignment", "Each partition assigned to exactly one consumer");
conceptTable.AddRow("Rebalancing", "Partitions redistributed on join/leave/crash");
conceptTable.AddRow("Fault Tolerance", "Crashed consumer's partitions reassigned automatically");
conceptTable.AddRow("Elastic Scaling", "Add/remove consumers to scale processing");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Surgewave Consumer Group Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Log Aggregation demo completed![/]");
return 0;

// ═══════════════════════════════════════════════════════════════════════
// Worker management
// ═══════════════════════════════════════════════════════════════════════

async Task StartWorkerAsync(int workerId)
{
    var stats = new WorkerStats(workerId);
    workerStats[workerId] = stats;

    var workerCtsLocal = new CancellationTokenSource();
    workerCts[workerId] = workerCtsLocal;

    var workerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
        .UseSurgewaveProtocol()
        .BuildAsync();

    var task = Task.Run(async () =>
    {
        try
        {
            await using (workerClient)
            await using (var consumer = workerClient.CreateConsumer<string, LogEntry>(options =>
            {
                options.GroupId = groupId;
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.ValueDeserializer = Serializers.JsonDeserializer<LogEntry>();
            }))
            {
                consumer.Subscribe(topicName);
                AnsiConsole.MarkupLine("[green]  Worker {0} started[/]", workerId);

                while (!workerCtsLocal.Token.IsCancellationRequested)
                {
                    var result = await consumer.ConsumeAsync(
                        TimeSpan.FromMilliseconds(200),
                        workerCtsLocal.Token);

                    if (result?.Value is null) continue;

                    var log = result.Value;
                    stats.RecordMessage(log.Level);

                    // Track partition assignment
                    var assignment = consumer.Assignment;
                    stats.UpdateAssignment(assignment);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]  Worker {0} error: {1}[/]", workerId, ex.Message);
        }
    }, workerCtsLocal.Token);

    workerTasks[workerId] = task;
}

async Task StopWorkerAsync(int workerId)
{
    if (workerCts.TryRemove(workerId, out var cts))
    {
        await cts.CancelAsync();

        if (workerTasks.TryRemove(workerId, out var task))
        {
            try { await task; }
            catch (OperationCanceledException) { }
        }

        cts.Dispose();
        AnsiConsole.MarkupLine("[yellow]  Worker {0} stopped[/]", workerId);
    }
}

void ShowWorkerStatus()
{
    var statusTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Worker")
        .AddColumn("Partitions")
        .AddColumn(new TableColumn("Messages").RightAligned())
        .AddColumn(new TableColumn("DEBUG").RightAligned())
        .AddColumn(new TableColumn("INFO").RightAligned())
        .AddColumn(new TableColumn("[yellow]WARN[/]").RightAligned())
        .AddColumn(new TableColumn("[red]ERROR[/]").RightAligned())
        .AddColumn("Status");

    foreach (var (workerId, stats) in workerStats.OrderBy(kv => kv.Key))
    {
        var isActive = workerCts.ContainsKey(workerId);
        var partitions = stats.LastKnownPartitions;

        statusTable.AddRow(
            $"Worker {workerId}",
            partitions.Length > 0 ? string.Join(", ", partitions) : "[grey]none[/]",
            stats.TotalMessages.ToString("N0"),
            stats.GetCount("DEBUG").ToString(),
            stats.GetCount("INFO").ToString(),
            $"[yellow]{stats.GetCount("WARN")}[/]",
            $"[red]{stats.GetCount("ERROR")}[/]",
            isActive ? "[green]Active[/]" : "[red]Stopped[/]");
    }

    AnsiConsole.Write(new Panel(statusTable)
        .Header("[cyan]Worker Status[/]")
        .BorderColor(Color.Cyan1));
}

// ═══════════════════════════════════════════════════════════════════════
// Domain types
// ═══════════════════════════════════════════════════════════════════════

sealed record LogEntry(DateTimeOffset Timestamp, string Service, string Level, string Message);

sealed class WorkerStats(int workerId)
{
    private const string Topic = "application-logs";
    private readonly ConcurrentDictionary<string, int> _countByLevel = new();
    private volatile int _totalMessages;
    private volatile string[] _lastKnownPartitions = [];

    public int WorkerId => workerId;
    public int TotalMessages => _totalMessages;
    public string[] LastKnownPartitions => _lastKnownPartitions;

    public void RecordMessage(string level)
    {
        _countByLevel.AddOrUpdate(level, 1, (_, v) => v + 1);
        Interlocked.Increment(ref _totalMessages);
    }

    public int GetCount(string level) =>
        _countByLevel.GetValueOrDefault(level, 0);

    public void UpdateAssignment(IReadOnlyList<(string topic, int partition)> assignment)
    {
        _lastKnownPartitions = assignment
            .Where(a => a.topic == Topic)
            .Select(a => $"P{a.partition}")
            .OrderBy(p => p)
            .ToArray();
    }
}
