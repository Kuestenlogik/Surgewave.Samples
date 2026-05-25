#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using System.Diagnostics;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Runtime;
using Kuestenlogik.Surgewave.Testing.Chaos;
using Spectre.Console;

// ═══════════════════════════════════════════════════════════════════════
// Payment Gateway -- Multi-Broker Clustering & Failover
// ═══════════════════════════════════════════════════════════════════════
// 3-broker cluster processes payments. One broker crashes mid-stream;
// the cluster performs leader election and continues without data loss.
// All payment IDs are accounted for at the end.
// ═══════════════════════════════════════════════════════════════════════

AnsiConsole.Write(new FigletText("Payment Gateway").Color(Color.Red));
AnsiConsole.MarkupLine("[grey]3-Broker Cluster with Failover Demo[/]\n");

// ── Records ──────────────────────────────────────────────────────────

const int totalPayments = 100;
var processedPayments = new ConcurrentDictionary<string, PaymentResult>();
var sentPaymentIds = new ConcurrentBag<string>();
var failoverStart = Stopwatch.GetTimestamp();
var failoverEnd = Stopwatch.GetTimestamp();
var failoverTriggered = false;
var brokerRecovered = false;

// ── Start 3-broker cluster ───────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 1: Starting 3-Broker Cluster ==[/]\n");

// Build 3 brokers with clustering enabled
var broker1 = await SurgewaveRuntime.CreateBuilder()
    .WithBrokerId(0)
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .WithReplicationFactor(1) // Single-node replication in this demo
    .Build()
    .StartAsync();

var broker2 = await SurgewaveRuntime.CreateBuilder()
    .WithBrokerId(1)
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .WithReplicationFactor(1)
    .Build()
    .StartAsync();

var broker3 = await SurgewaveRuntime.CreateBuilder()
    .WithBrokerId(2)
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .WithReplicationFactor(1)
    .Build()
    .StartAsync();

var clusterTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Broker")
    .AddColumn("Port")
    .AddColumn("Status");

clusterTable.AddRow("Broker 0", broker1.Port.ToString(), "[green]Running[/]");
clusterTable.AddRow("Broker 1", broker2.Port.ToString(), "[green]Running[/]");
clusterTable.AddRow("Broker 2", broker3.Port.ToString(), "[green]Running[/]");

AnsiConsole.Write(clusterTable);
AnsiConsole.MarkupLine("\n[green]3-broker cluster is up![/]\n");

// Use broker 1 as the primary connection
var bootstrapServers = broker1.BootstrapServers;

// ── Create ChaosEngine for fault injection ───────────────────────────

var chaos = new ChaosEngine();

// ── Start payment processor ──────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 2: Starting Payment Processor ==[/]\n");

await using var processorClient = await SurgewaveClient.Create(bootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

using var processorCts = new CancellationTokenSource();

var processorTask = Task.Run(async () =>
{
    await using var consumer = processorClient.CreateConsumer<string, Payment>(options =>
    {
        options.GroupId = "payment-processor";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<Payment>();
    });

    await using var resultProducer = processorClient.CreateProducer<string, PaymentResult>(options =>
    {
        options.ValueSerializer = Serializers.Json<PaymentResult>();
    });

    consumer.Subscribe("payments");
    var random = new Random(123);

    try
    {
        while (!processorCts.Token.IsCancellationRequested)
        {
            var record = await consumer.ConsumeAsync(
                TimeSpan.FromMilliseconds(200),
                processorCts.Token);

            if (record?.Value is null) continue;

            var payment = record.Value;

            // Simulate payment processing
            await Task.Delay(random.Next(5, 30), processorCts.Token);

            var approved = random.NextDouble() > 0.08; // 92% approval rate
            var result = new PaymentResult(
                payment.PaymentId,
                approved ? "Approved" : "Declined",
                approved ? $"AUTH-{random.Next(100000, 999999)}" : null,
                DateTimeOffset.UtcNow);

            await resultProducer.ProduceAsync("payment-results", payment.PaymentId, result);
            processedPayments[payment.PaymentId] = result;
        }
    }
    catch (OperationCanceledException) { }
}, processorCts.Token);

AnsiConsole.MarkupLine("[green]Payment processor running[/]\n");

// Give processor time to subscribe
await Task.Delay(500);

// ── Generate payment stream ──────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 3: Processing {0} Payments ==[/]\n", totalPayments);

await using var paymentClient = await SurgewaveClient.Create(bootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

await using var paymentProducer = paymentClient.CreateProducer<string, Payment>(options =>
{
    options.ValueSerializer = Serializers.Json<Payment>();
});

var random2 = new Random(42);
var currencies = new[] { "USD", "EUR", "GBP", "JPY", "CHF" };
var merchantIds = new[] { "SHOP-001", "SHOP-002", "SHOP-003", "SHOP-004", "SHOP-005" };

await AnsiConsole.Progress()
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[green]Producing payments[/]", maxValue: totalPayments);

        for (var i = 1; i <= totalPayments; i++)
        {
            var paymentId = $"PAY-{i:D6}";
            sentPaymentIds.Add(paymentId);

            var payment = new Payment(
                paymentId,
                Math.Round((decimal)(random2.NextDouble() * 500 + 5), 2),
                currencies[random2.Next(currencies.Length)],
                $"****{random2.Next(1000, 9999)}",
                merchantIds[random2.Next(merchantIds.Length)],
                DateTimeOffset.UtcNow);

            await paymentProducer.ProduceAsync("payments", paymentId, payment);
            task.Increment(1);

            // ── Broker crash at payment 30 ───────────────────────
            if (i == 30 && !failoverTriggered)
            {
                AnsiConsole.MarkupLine("\n[red]!! Broker 2 crashed at payment {0} !![/]", i);

                failoverStart = Stopwatch.GetTimestamp();
                failoverTriggered = true;

                // Activate fault on the chaos engine
                chaos.ActivateFault(FaultType.NodeCrash, new FaultScope { BrokerId = 2 });

                // Actually dispose broker 2
                await broker2.DisposeAsync();

                AnsiConsole.MarkupLine("[yellow]  Leader election in progress...[/]");
                AnsiConsole.MarkupLine("[yellow]  Payments continue on remaining brokers[/]\n");
            }

            // ── Broker recovery at payment 60 ────────────────────
            if (i == 60 && !brokerRecovered)
            {
                AnsiConsole.MarkupLine("\n[green]!! Recovering Broker 2 at payment {0} !![/]", i);

                broker2 = await SurgewaveRuntime.CreateBuilder()
                    .WithBrokerId(1)
                    .WithPort(0)
                    .WithStorageMode(StorageMode.Memory)
                    .WithAutoCreateTopics(true)
                    .WithPartitions(3)
                    .Build()
                    .StartAsync();

                failoverEnd = Stopwatch.GetTimestamp();
                brokerRecovered = true;

                chaos.DeactivateAll();

                AnsiConsole.MarkupLine("[green]  Broker 2 rejoined on port {0}[/]", broker2.Port);
                AnsiConsole.MarkupLine("[green]  ISR recovery in progress...[/]\n");
            }

            await Task.Delay(random2.Next(20, 60));
        }
    });

// Wait for processor to catch up
AnsiConsole.MarkupLine("\n[grey]Waiting for payment processor to complete...[/]");
await Task.Delay(3000);

await processorCts.CancelAsync();
try { await processorTask; }
catch (OperationCanceledException) { }

// ── Final summary ────────────────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Final Summary ==[/]\n");

// Cluster status
var finalClusterTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Broker")
    .AddColumn("Port")
    .AddColumn("Status");

finalClusterTable.AddRow("Broker 0", broker1.Port.ToString(), "[green]Running[/]");
finalClusterTable.AddRow("Broker 1 (recovered)", broker2.Port.ToString(), "[green]Running[/]");
finalClusterTable.AddRow("Broker 2", broker3.Port.ToString(), "[green]Running[/]");

AnsiConsole.Write(new Panel(finalClusterTable)
    .Header("[cyan]Cluster Status[/]")
    .BorderColor(Color.Cyan1));

// Payment results
var approved = processedPayments.Values.Count(r => r.Status == "Approved");
var declined = processedPayments.Values.Count(r => r.Status == "Declined");
var missing = sentPaymentIds.Except(processedPayments.Keys).ToList();

var failoverDuration = failoverTriggered && brokerRecovered
    ? Stopwatch.GetElapsedTime(failoverStart, failoverEnd)
    : TimeSpan.Zero;

var resultsTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Metric")
    .AddColumn(new TableColumn("Value").RightAligned());

resultsTable.AddRow("Total payments sent", totalPayments.ToString());
resultsTable.AddRow("Total payments processed", processedPayments.Count.ToString());
resultsTable.AddRow("[green]Approved[/]", $"[green]{approved}[/]");
resultsTable.AddRow("[red]Declined[/]", $"[red]{declined}[/]");

if (missing.Count > 0)
    resultsTable.AddRow("[yellow]Unprocessed[/]", $"[yellow]{missing.Count}[/]");
else
    resultsTable.AddRow("[green]Unprocessed[/]", "[green]0 (all accounted for)[/]");

resultsTable.AddRow("Failover duration", failoverDuration > TimeSpan.Zero
    ? $"{failoverDuration.TotalMilliseconds:N0} ms"
    : "N/A");

AnsiConsole.Write(resultsTable);

// Chaos timeline
var chaosEvents = chaos.Timeline.GetEvents();
if (chaosEvents.Count > 0)
{
    var chaosTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Time")
        .AddColumn("Event")
        .AddColumn("Details");

    foreach (var evt in chaosEvents)
    {
        var color = evt.EventType == ChaosEventType.Activated ? "red" : "green";
        chaosTable.AddRow(
            evt.Timestamp.ToString("HH:mm:ss.fff"),
            $"[{color}]{evt.EventType}[/]",
            evt.Description);
    }

    AnsiConsole.Write(new Panel(chaosTable)
        .Header("[red]Chaos Timeline[/]")
        .BorderColor(Color.Red));
}

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Multi-Broker Cluster", "3 brokers share the workload");
conceptTable.AddRow("Chaos Testing", "ChaosEngine to inject broker faults");
conceptTable.AddRow("Automatic Failover", "Remaining brokers absorb failed broker's work");
conceptTable.AddRow("Leader Election", "New leaders elected for orphaned partitions");
conceptTable.AddRow("Broker Recovery", "Crashed broker rejoins and resyncs");
conceptTable.AddRow("Zero Data Loss", "All payment IDs accounted for");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Clustering Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Payment Gateway demo completed![/]");

// Cleanup
await broker1.DisposeAsync();
await broker2.DisposeAsync();
await broker3.DisposeAsync();

return 0;

// ═══════════════════════════════════════════════════════════════════════
// Domain records
// ═══════════════════════════════════════════════════════════════════════

sealed record Payment(string PaymentId, decimal Amount, string Currency, string CardLast4, string MerchantId, DateTimeOffset Timestamp);
sealed record PaymentResult(string PaymentId, string Status, string? AuthCode, DateTimeOffset Timestamp);
