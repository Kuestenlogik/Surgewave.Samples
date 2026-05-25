#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// =====================================================================
// Real-Time Fraud Detection -- Credit Card Transaction Monitoring
// =====================================================================
// Applies four fraud detection rules in real-time:
//   Rule 1: Velocity Check -- too many transactions in short window
//   Rule 2: Amount Anomaly -- transaction far exceeds card average
//   Rule 3: Geo-Velocity  -- impossible travel between locations
//   Rule 4: Card Testing   -- many micro-transactions (stolen card test)
// =====================================================================

AnsiConsole.Write(new FigletText("Fraud Detection").Color(Color.Red));
AnsiConsole.MarkupLine("[grey]Real-Time Credit Card Transaction Monitoring[/]\n");

// -- Configuration ----------------------------------------------------

const int partitionCount = 3;
var totalTransactions = 0;
var totalAlerts = 0;
var ruleHits = new ConcurrentDictionary<string, int>();
var allAlerts = new ConcurrentBag<FraudAlert>();
var approvedCount = 0;

// Card profile tracking for Rule 2 (amount anomaly)
var cardAvgAmount = new ConcurrentDictionary<string, (decimal Total, int Count)>();

// Card velocity tracking for Rule 1 (velocity check)
var cardRecentTimes = new ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>>();

// Card location tracking for Rule 3 (geo-velocity)
var cardLastLocation = new ConcurrentDictionary<string, (double Lat, double Lon, DateTimeOffset Time)>();

// Card micro-transaction tracking for Rule 4 (card testing)
var cardMicroTxTimes = new ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>>();

// -- City database for realistic geo data -----------------------------

var cities = new Dictionary<string, (double Lat, double Lon, string Country)>
{
    ["Berlin"] = (52.5200, 13.4050, "DE"),
    ["Munich"] = (48.1351, 11.5820, "DE"),
    ["Hamburg"] = (53.5511, 9.9937, "DE"),
    ["Frankfurt"] = (50.1109, 8.6821, "DE"),
    ["Tokyo"] = (35.6762, 139.6503, "JP"),
    ["New York"] = (40.7128, -74.0060, "US"),
    ["London"] = (51.5074, -0.1278, "GB"),
    ["Paris"] = (48.8566, 2.3522, "FR"),
    ["Sydney"] = (-33.8688, 151.2093, "AU"),
    ["Singapore"] = (1.3521, 103.8198, "SG"),
};

var normalCities = new[] { "Berlin", "Munich", "Hamburg", "Frankfurt" };
var merchants = new[] { "Amazon.de", "MediaMarkt", "REWE", "Lidl", "DM Drogerie", "Saturn", "Zalando", "IKEA" };

// -- Start embedded broker --------------------------------------------

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(partitionCount)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]\n", surgewave.Port);

// -- Create clients ---------------------------------------------------

await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var detectorClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var alertClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();

// -- Create producers/consumers ---------------------------------------

await using var txProducer = producerClient.CreateProducer<string, Transaction>(options =>
{
    options.ValueSerializer = Serializers.Json<Transaction>();
});

await using var alertProducer = producerClient.CreateProducer<string, FraudAlert>(options =>
{
    options.ValueSerializer = Serializers.Json<FraudAlert>();
});

// -- Fraud Detection Engine -------------------------------------------

using var detectorCts = new CancellationTokenSource();

var detectorTask = Task.Run(async () =>
{
    await using var consumer = detectorClient.CreateConsumer<string, Transaction>(options =>
    {
        options.GroupId = "fraud-detector";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<Transaction>();
    });
    consumer.Subscribe("transactions");

    try
    {
        while (!detectorCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(50), detectorCts.Token);
            if (result?.Value is null) continue;

            var tx = result.Value;
            var alerts = new List<FraudAlert>();

            // -- Rule 1: Velocity Check (> 3 transactions in 5 minutes) --
            var times = cardRecentTimes.GetOrAdd(tx.CardId, _ => new ConcurrentQueue<DateTimeOffset>());
            times.Enqueue(tx.Timestamp);

            // Evict old entries outside window
            while (times.TryPeek(out var oldest) && tx.Timestamp - oldest > TimeSpan.FromMinutes(5))
                times.TryDequeue(out _);

            if (times.Count > 3)
            {
                alerts.Add(new FraudAlert(
                    tx.Id, tx.CardId, "Velocity Check",
                    "HIGH",
                    $"{times.Count} transactions in 5 minutes (threshold: 3)",
                    tx.Timestamp));
            }

            // -- Rule 2: Amount Anomaly (> 10x average) --
            var profile = cardAvgAmount.GetOrAdd(tx.CardId, _ => (0m, 0));
            var newTotal = profile.Total + tx.Amount;
            var newCount = profile.Count + 1;
            cardAvgAmount[tx.CardId] = (newTotal, newCount);

            if (profile.Count >= 3) // Need at least 3 prior transactions
            {
                var avg = profile.Total / profile.Count;
                if (avg > 0 && tx.Amount > avg * 10)
                {
                    alerts.Add(new FraudAlert(
                        tx.Id, tx.CardId, "Amount Anomaly",
                        "CRITICAL",
                        $"Transaction {tx.Amount:C} is {tx.Amount / avg:F1}x the average ({avg:C})",
                        tx.Timestamp));
                }
            }

            // -- Rule 3: Geo-Velocity (> 500 km/h impossible travel) --
            if (cardLastLocation.TryGetValue(tx.CardId, out var lastLoc))
            {
                var distanceKm = HaversineDistance(lastLoc.Lat, lastLoc.Lon, tx.Lat, tx.Lon);
                var timeDiff = tx.Timestamp - lastLoc.Time;
                if (timeDiff.TotalHours > 0.001) // Avoid division by zero
                {
                    var speedKmh = distanceKm / timeDiff.TotalHours;
                    if (speedKmh > 500 && distanceKm > 100)
                    {
                        alerts.Add(new FraudAlert(
                            tx.Id, tx.CardId, "Geo-Velocity",
                            "CRITICAL",
                            $"Impossible travel: {distanceKm:F0} km in {timeDiff.TotalMinutes:F1} min ({speedKmh:F0} km/h) from {lastLoc.Lat:F1},{lastLoc.Lon:F1} to {tx.Lat:F1},{tx.Lon:F1}",
                            tx.Timestamp));
                    }
                }
            }
            cardLastLocation[tx.CardId] = (tx.Lat, tx.Lon, tx.Timestamp);

            // -- Rule 4: Card Testing (> 5 micro-transactions < 5 EUR in 10 min) --
            if (tx.Amount < 5.00m)
            {
                var microTimes = cardMicroTxTimes.GetOrAdd(tx.CardId, _ => new ConcurrentQueue<DateTimeOffset>());
                microTimes.Enqueue(tx.Timestamp);

                while (microTimes.TryPeek(out var oldestMicro) && tx.Timestamp - oldestMicro > TimeSpan.FromMinutes(10))
                    microTimes.TryDequeue(out _);

                if (microTimes.Count > 5)
                {
                    alerts.Add(new FraudAlert(
                        tx.Id, tx.CardId, "Card Testing",
                        "HIGH",
                        $"{microTimes.Count} micro-transactions (< 5 EUR) in 10 minutes",
                        tx.Timestamp));
                }
            }

            // Publish alerts or approve
            if (alerts.Count > 0)
            {
                foreach (var alert in alerts)
                {
                    await alertProducer.ProduceAsync("fraud-alerts", alert.CardId, alert);
                    allAlerts.Add(alert);
                    ruleHits.AddOrUpdate(alert.Rule, 1, (_, v) => v + 1);
                    Interlocked.Increment(ref totalAlerts);
                }
            }
            else
            {
                Interlocked.Increment(ref approvedCount);
            }
        }
    }
    catch (OperationCanceledException) { }
}, detectorCts.Token);

// -- Alert Monitor (live display) -------------------------------------

using var alertCts = new CancellationTokenSource();

var alertTask = Task.Run(async () =>
{
    await using var consumer = alertClient.CreateConsumer<string, FraudAlert>(options =>
    {
        options.GroupId = "alert-monitor";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<FraudAlert>();
    });
    consumer.Subscribe("fraud-alerts");

    try
    {
        while (!alertCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), alertCts.Token);
            if (result?.Value is null) continue;

            var alert = result.Value;
            var severityColor = alert.Severity == "CRITICAL" ? "red" : "yellow";
            AnsiConsole.MarkupLine(
                "  [{0}]FRAUD ALERT[/] | Card: {1} | Rule: [{0}]{2}[/] | {3}",
                severityColor, alert.CardId, alert.Rule, alert.Description);
        }
    }
    catch (OperationCanceledException) { }
}, alertCts.Token);

// Give consumers time to subscribe
await Task.Delay(1000);

// -- Transaction Generator --------------------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 1: Generating Transaction Stream ==[/]\n");
AnsiConsole.MarkupLine("[grey]Producing mixed legitimate and fraudulent transactions over ~30 seconds...[/]\n");

var random = new Random(42);
var txId = 0;
var startTime = DateTimeOffset.UtcNow;

// Build initial card profiles with normal transactions
AnsiConsole.MarkupLine("[yellow]Building card profiles with normal history...[/]\n");

for (var card = 1; card <= 20; card++)
{
    var cardId = $"Card-{card}";
    for (var i = 0; i < 5; i++)
    {
        txId++;
        var city = normalCities[random.Next(normalCities.Length)];
        var (lat, lon, country) = cities[city];
        var tx = new Transaction(
            $"TX-{txId:D6}", cardId,
            Math.Round((decimal)(random.NextDouble() * 150 + 10), 2),
            "EUR",
            merchants[random.Next(merchants.Length)],
            city, country, lat, lon,
            startTime.AddSeconds(-600 + i * 120)); // Spread over past 10 minutes

        await txProducer.ProduceAsync("transactions", cardId, tx);
        Interlocked.Increment(ref totalTransactions);
    }
}

await Task.Delay(2000);
AnsiConsole.MarkupLine("[green]Card profiles built. Starting real-time monitoring...[/]\n");

// Phase 2: Live transactions with fraud patterns
AnsiConsole.MarkupLine("[blue]== Phase 2: Live Monitoring with Fraud Injection ==[/]\n");

var liveStart = DateTimeOffset.UtcNow;

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Monitoring transactions...", async ctx =>
    {
        // Normal traffic + fraud patterns over ~25 seconds
        for (var second = 0; second < 25; second++)
        {
            var now = liveStart.AddSeconds(second);

            // Normal transactions (2-3 per second from random cards)
            var normalCount = random.Next(2, 4);
            for (var n = 0; n < normalCount; n++)
            {
                txId++;
                var cardId = $"Card-{random.Next(1, 21)}";
                var city = normalCities[random.Next(normalCities.Length)];
                var (lat, lon, country) = cities[city];

                var tx = new Transaction(
                    $"TX-{txId:D6}", cardId,
                    Math.Round((decimal)(random.NextDouble() * 190 + 10), 2),
                    "EUR",
                    merchants[random.Next(merchants.Length)],
                    city, country, lat, lon,
                    now.AddMilliseconds(random.Next(0, 900)));

                await txProducer.ProduceAsync("transactions", cardId, tx);
                Interlocked.Increment(ref totalTransactions);
            }

            // -- Inject fraud pattern A: Velocity fraud (Card-5, seconds 3-5) --
            if (second >= 3 && second <= 5)
            {
                for (var v = 0; v < 2; v++)
                {
                    txId++;
                    var tx = new Transaction(
                        $"TX-{txId:D6}", "Card-5",
                        Math.Round((decimal)(random.NextDouble() * 100 + 20), 2),
                        "EUR", "Amazon.de", "Berlin", "DE", 52.52, 13.41,
                        now.AddMilliseconds(v * 200));

                    await txProducer.ProduceAsync("transactions", "Card-5", tx);
                    Interlocked.Increment(ref totalTransactions);
                }
            }

            // -- Inject fraud pattern B: Amount anomaly (Card-8, second 8) --
            if (second == 8)
            {
                txId++;
                var tx = new Transaction(
                    $"TX-{txId:D6}", "Card-8",
                    4999.99m,
                    "EUR", "Luxury Jeweler", "Frankfurt", "DE", 50.11, 8.68,
                    now);

                await txProducer.ProduceAsync("transactions", "Card-8", tx);
                Interlocked.Increment(ref totalTransactions);

                ctx.Status("[red]Injected amount anomaly on Card-8...[/]");
            }

            // -- Inject fraud pattern C: Geo-velocity (Card-12, seconds 12-13) --
            if (second == 12)
            {
                txId++;
                var tx = new Transaction(
                    $"TX-{txId:D6}", "Card-12",
                    89.99m,
                    "EUR", "Saturn", "Berlin", "DE", 52.52, 13.41,
                    now);

                await txProducer.ProduceAsync("transactions", "Card-12", tx);
                Interlocked.Increment(ref totalTransactions);
            }
            if (second == 13)
            {
                txId++;
                // Same card, 1 second later, in Tokyo -- impossible!
                var tx = new Transaction(
                    $"TX-{txId:D6}", "Card-12",
                    125.00m,
                    "JPY", "Shibuya Electronics", "Tokyo", "JP", 35.68, 139.65,
                    now);

                await txProducer.ProduceAsync("transactions", "Card-12", tx);
                Interlocked.Increment(ref totalTransactions);

                ctx.Status("[red]Injected geo-velocity fraud on Card-12...[/]");
            }

            // -- Inject fraud pattern D: Card testing (Card-15, seconds 16-20) --
            if (second >= 16 && second <= 20)
            {
                for (var m = 0; m < 2; m++)
                {
                    txId++;
                    var tx = new Transaction(
                        $"TX-{txId:D6}", "Card-15",
                        Math.Round((decimal)(random.NextDouble() * 3 + 0.50), 2),
                        "EUR", "Online Test Store", "Hamburg", "DE", 53.55, 9.99,
                        now.AddMilliseconds(m * 300));

                    await txProducer.ProduceAsync("transactions", "Card-15", tx);
                    Interlocked.Increment(ref totalTransactions);
                }
            }

            ctx.Status($"Monitoring... {totalTransactions} transactions | {totalAlerts} alerts");
            await Task.Delay(300);
        }
    });

// Wait for detection engine to catch up
await Task.Delay(2000);

// -- Shutdown ---------------------------------------------------------

await detectorCts.CancelAsync();
await alertCts.CancelAsync();

try { await detectorTask; } catch (OperationCanceledException) { }
try { await alertTask; } catch (OperationCanceledException) { }

// -- Final Summary ----------------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Final Summary ==[/]\n");

// Overall statistics
var summaryTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Metric")
    .AddColumn(new TableColumn("Value").RightAligned());

summaryTable.AddRow("Total transactions processed", totalTransactions.ToString("N0"));
summaryTable.AddRow("[green]Approved transactions[/]", $"[green]{approvedCount:N0}[/]");
summaryTable.AddRow("[red]Fraud alerts raised[/]", $"[red]{totalAlerts:N0}[/]");
summaryTable.AddRow("Alert rate",
    totalTransactions > 0 ? $"{(double)totalAlerts / totalTransactions * 100:F1}%" : "0%");

AnsiConsole.Write(summaryTable);

// Rule breakdown
var ruleTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Rule")
    .AddColumn("Description")
    .AddColumn(new TableColumn("Alerts").RightAligned())
    .AddColumn("Severity");

var ruleDescriptions = new Dictionary<string, (string Desc, string Severity)>
{
    ["Velocity Check"] = ("> 3 transactions in 5-minute window", "HIGH"),
    ["Amount Anomaly"] = ("Transaction > 10x card average", "CRITICAL"),
    ["Geo-Velocity"] = ("Impossible travel speed > 500 km/h", "CRITICAL"),
    ["Card Testing"] = ("> 5 micro-transactions (< 5 EUR) in 10 min", "HIGH"),
};

foreach (var (rule, (desc, severity)) in ruleDescriptions)
{
    var hits = ruleHits.GetValueOrDefault(rule, 0);
    var severityColor = severity == "CRITICAL" ? "red" : "yellow";
    ruleTable.AddRow(
        $"[bold]{rule}[/]",
        desc,
        hits > 0 ? $"[{severityColor}]{hits}[/]" : "[green]0[/]",
        $"[{severityColor}]{severity}[/]");
}

AnsiConsole.Write(new Panel(ruleTable)
    .Header("[red]Fraud Detection Rules[/]")
    .BorderColor(Color.Red));

// Flagged cards summary
var flaggedCards = allAlerts
    .GroupBy(a => a.CardId)
    .OrderByDescending(g => g.Count())
    .Take(10)
    .ToList();

if (flaggedCards.Count > 0)
{
    var cardTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Card")
        .AddColumn(new TableColumn("Alerts").RightAligned())
        .AddColumn("Rules Triggered");

    foreach (var group in flaggedCards)
    {
        var rulesTriggered = string.Join(", ", group.Select(a => a.Rule).Distinct());
        cardTable.AddRow(
            $"[red]{group.Key}[/]",
            group.Count().ToString(),
            rulesTriggered);
    }

    AnsiConsole.Write(new Panel(cardTable)
        .Header("[yellow]Flagged Cards[/]")
        .BorderColor(Color.Yellow));
}

// Recent alerts detail
var recentAlerts = allAlerts
    .OrderByDescending(a => a.Timestamp)
    .Take(8)
    .ToList();

if (recentAlerts.Count > 0)
{
    var alertDetailTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Time")
        .AddColumn("Transaction")
        .AddColumn("Card")
        .AddColumn("Rule")
        .AddColumn("Description");

    foreach (var alert in recentAlerts)
    {
        var severityColor = alert.Severity == "CRITICAL" ? "red" : "yellow";
        alertDetailTable.AddRow(
            alert.Timestamp.ToString("HH:mm:ss.fff"),
            alert.TransactionId,
            alert.CardId,
            $"[{severityColor}]{alert.Rule}[/]",
            alert.Description.Length > 60 ? alert.Description[..60] + "..." : alert.Description);
    }

    AnsiConsole.Write(new Panel(alertDetailTable)
        .Header("[red]Recent Fraud Alerts (Last 8)[/]")
        .BorderColor(Color.Red));
}

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Velocity Check", "Sliding window counts transactions per card in time window");
conceptTable.AddRow("Amount Anomaly", "Running average per card detects statistical outliers");
conceptTable.AddRow("Geo-Velocity", "Haversine distance + time delta detects impossible travel");
conceptTable.AddRow("Card Testing", "Micro-transaction pattern detection for stolen card validation");
conceptTable.AddRow("Real-Time", "Sub-second detection latency using Surgewave event streaming");
conceptTable.AddRow("Multi-Rule Engine", "Multiple rules evaluated per transaction, independent alerts");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Fraud Detection Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Fraud Detection demo completed![/]");
return 0;

// =====================================================================
// Helper methods
// =====================================================================

static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
{
    const double r = 6371.0; // Earth radius in km
    var dLat = ToRadians(lat2 - lat1);
    var dLon = ToRadians(lon2 - lon1);
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
    var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    return r * c;
}

static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

// =====================================================================
// Domain records
// =====================================================================

sealed record Transaction(
    string Id,
    string CardId,
    decimal Amount,
    string Currency,
    string MerchantName,
    string MerchantCity,
    string Country,
    double Lat,
    double Lon,
    DateTimeOffset Timestamp);

sealed record FraudAlert(
    string TransactionId,
    string CardId,
    string Rule,
    string Severity,
    string Description,
    DateTimeOffset Timestamp);
