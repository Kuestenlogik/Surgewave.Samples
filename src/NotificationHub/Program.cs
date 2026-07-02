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

// =====================================================================
// Fan-out Push Notifications -- Multi-Channel Notification System
// =====================================================================
// User events are routed to Email, SMS, and Push channels based on
// configurable rules. Demonstrates fan-out patterns, priority-based
// routing, and per-user rate limiting with Surgewave topics.
// =====================================================================

AnsiConsole.Write(new FigletText("Notification Hub").Color(Color.DarkOrange));
AnsiConsole.MarkupLine("[grey]Multi-Channel Fan-Out with Priority & Rate Limiting[/]\n");

// -- Domain types ----------------------------------------------------

const string userEventsTopic = "user-events";
const string emailTopic = "notifications-email";
const string smsTopic = "notifications-sms";
const string pushTopic = "notifications-push";
const string statusTopic = "notification-status";

var users = new[]
{
    new UserProfile("U001", "Alice Meier", "alice@example.com", "+49-170-1111111", "device-alice-01"),
    new UserProfile("U002", "Bob Schmidt", "bob@example.com", "+49-171-2222222", "device-bob-01"),
    new UserProfile("U003", "Clara Weber", "clara@example.com", "+49-172-3333333", "device-clara-01"),
    new UserProfile("U004", "David Fischer", "david@example.com", "+49-173-4444444", "device-david-01"),
    new UserProfile("U005", "Eva Braun", "eva@example.com", "+49-174-5555555", "device-eva-01"),
};

var userLookup = users.ToDictionary(u => u.UserId);

// -- Notification rules ----------------------------------------------

var routingRules = new Dictionary<string, NotificationRule[]>
{
    ["login_new_device"] =
    [
        new(Channel.Push, Priority.High),
        new(Channel.Email, Priority.Medium),
    ],
    ["purchase_completed"] =
    [
        new(Channel.Email, Priority.Low),
        new(Channel.Push, Priority.Low),
    ],
    ["password_reset"] =
    [
        new(Channel.Sms, Priority.Critical),
        new(Channel.Email, Priority.High),
    ],
    ["account_locked"] =
    [
        new(Channel.Sms, Priority.Critical),
        new(Channel.Email, Priority.Critical),
        new(Channel.Push, Priority.High),
    ],
    ["weekly_digest"] =
    [
        new(Channel.Email, Priority.Low),
    ],
};

// -- Rate limiting state ---------------------------------------------

var smsCountPerUser = new ConcurrentDictionary<string, RateLimitCounter>();
var pushCountPerUser = new ConcurrentDictionary<string, RateLimitCounter>();
const int maxSmsPerUserPerHour = 3;
const int maxPushPerUserPerHour = 10;

// -- Delivery tracking -----------------------------------------------

var deliveryStats = new ConcurrentDictionary<string, ChannelStats>();
deliveryStats["Email"] = new ChannelStats();
deliveryStats["SMS"] = new ChannelStats();
deliveryStats["Push"] = new ChannelStats();

var droppedByRateLimit = new ConcurrentDictionary<string, int>();
var deliveriesByUser = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
var deliveriesByPriority = new ConcurrentDictionary<string, int>();

foreach (var user in users)
{
    deliveriesByUser[user.UserId] = new ConcurrentDictionary<string, int>();
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
    string.Join(", ", userEventsTopic, emailTopic, smsTopic, pushTopic, statusTopic));

// -- Connect clients -------------------------------------------------

await using var routerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var emailClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var smsClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var pushClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();

// -- Notification router (fan-out) -----------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 1: Starting Notification Router & Channel Processors ==[/]\n");

await using var eventConsumer = routerClient.CreateConsumer<string, UserEvent>(options =>
{
    options.GroupId = "notification-router";
    options.AutoOffsetReset = AutoOffsetReset.Earliest;
    options.ValueDeserializer = Serializers.JsonDeserializer<UserEvent>();
});

await using var fanoutProducer = routerClient.CreateProducer<string, ChannelNotification>(options =>
{
    options.ValueSerializer = Serializers.Json<ChannelNotification>();
});

eventConsumer.Subscribe(userEventsTopic);

using var routerCts = new CancellationTokenSource();

var routerTask = Task.Run(async () =>
{
    try
    {
        while (!routerCts.Token.IsCancellationRequested)
        {
            var result = await eventConsumer.ConsumeAsync(
                TimeSpan.FromMilliseconds(100), routerCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            if (!routingRules.TryGetValue(evt.EventType, out var rules)) continue;

            foreach (var rule in rules)
            {
                var notification = new ChannelNotification(
                    Guid.NewGuid().ToString("N")[..12],
                    evt.UserId, evt.EventType, rule.Channel,
                    rule.Priority, DateTimeOffset.UtcNow);

                var topic = rule.Channel switch
                {
                    Channel.Email => emailTopic,
                    Channel.Sms => smsTopic,
                    Channel.Push => pushTopic,
                    _ => emailTopic,
                };

                await fanoutProducer.ProduceAsync(topic, evt.UserId, notification);
            }
        }
    }
    catch (OperationCanceledException) { }
}, routerCts.Token);

// -- Channel processors (3 parallel consumers) -----------------------

using var processorCts = new CancellationTokenSource();

var emailTask = RunChannelProcessorAsync(
    emailClient, emailTopic, "email-processor", "Email",
    100, null, null, processorCts.Token);

var smsTask = RunChannelProcessorAsync(
    smsClient, smsTopic, "sms-processor", "SMS",
    200, smsCountPerUser, maxSmsPerUserPerHour, processorCts.Token);

var pushTask = RunChannelProcessorAsync(
    pushClient, pushTopic, "push-processor", "Push",
    50, pushCountPerUser, maxPushPerUserPerHour, processorCts.Token);

await Task.Delay(500); // Let consumers subscribe

// -- Generate user events --------------------------------------------

AnsiConsole.MarkupLine("[blue]== Phase 2: Generating User Events (30 seconds) ==[/]\n");

await using var eventProducer = producerClient.CreateProducer<string, UserEvent>(options =>
{
    options.ValueSerializer = Serializers.Json<UserEvent>();
});

var random = new Random(42);
var eventTypes = new[] { "login_new_device", "purchase_completed", "password_reset", "account_locked", "weekly_digest" };
var totalEventsProduced = 0;

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Generating events...", async ctx =>
    {
        var startTime = DateTimeOffset.UtcNow;
        var burstSent = false;

        while ((DateTimeOffset.UtcNow - startTime).TotalSeconds < 30)
        {
            // At ~15 seconds, send a burst: user locked out (all 3 channels)
            if (!burstSent && (DateTimeOffset.UtcNow - startTime).TotalSeconds >= 15)
            {
                burstSent = true;
                AnsiConsole.MarkupLine("\n[red]  BURST: User U003 account locked -- triggering 3 channels simultaneously![/]");

                for (var burst = 0; burst < 3; burst++)
                {
                    var lockEvt = new UserEvent($"U003", "account_locked", DateTimeOffset.UtcNow,
                        $"Failed login attempt #{burst + 3} from IP 203.0.113.{random.Next(1, 255)}");
                    await eventProducer.ProduceAsync(userEventsTopic, lockEvt.UserId, lockEvt);
                    Interlocked.Increment(ref totalEventsProduced);
                }
            }

            var userId = users[random.Next(users.Length)].UserId;
            var eventType = eventTypes[random.Next(eventTypes.Length)];
            var description = eventType switch
            {
                "login_new_device" => $"Login from {(random.Next(2) == 0 ? "Chrome on Windows" : "Safari on iPhone")}",
                "purchase_completed" => $"Order #{random.Next(10000, 99999)} - EUR {random.Next(10, 500):N2}",
                "password_reset" => "Password reset requested via settings",
                "account_locked" => $"Failed login attempt from IP 198.51.100.{random.Next(1, 255)}",
                "weekly_digest" => "Weekly activity summary",
                _ => "Unknown event",
            };

            var userEvent = new UserEvent(userId, eventType, DateTimeOffset.UtcNow, description);
            await eventProducer.ProduceAsync(userEventsTopic, userId, userEvent);
            Interlocked.Increment(ref totalEventsProduced);

            ctx.Status($"Produced {totalEventsProduced} events...");
            await Task.Delay(random.Next(100, 500), CancellationToken.None);
        }
    });

// Wait for processors to finish
await Task.Delay(2000);

// -- Shutdown --------------------------------------------------------

await routerCts.CancelAsync();
await processorCts.CancelAsync();

try { await routerTask; } catch (OperationCanceledException) { }
try { await emailTask; } catch (OperationCanceledException) { }
try { await smsTask; } catch (OperationCanceledException) { }
try { await pushTask; } catch (OperationCanceledException) { }

// -- Final summary ---------------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Final: Delivery Statistics ==[/]\n");

// Per channel stats
var channelTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Channel")
    .AddColumn(new TableColumn("Sent").RightAligned())
    .AddColumn(new TableColumn("Rate Limited").RightAligned())
    .AddColumn(new TableColumn("Avg Latency").RightAligned());

foreach (var (channel, stats) in deliveryStats.OrderByDescending(kv => kv.Value.Sent))
{
    var dropped = droppedByRateLimit.GetValueOrDefault(channel, 0);
    var avgLatency = stats.Sent > 0 ? $"{stats.TotalLatencyMs / stats.Sent}ms" : "-";
    var droppedColor = dropped > 0 ? "red" : "grey";

    channelTable.AddRow(
        channel switch { "Email" => "[cyan]Email[/]", "SMS" => "[yellow]SMS[/]", "Push" => "[green]Push[/]", _ => channel },
        stats.Sent.ToString("N0"),
        $"[{droppedColor}]{dropped}[/]",
        avgLatency);
}

AnsiConsole.Write(new Panel(channelTable)
    .Header("[blue]Channel Delivery Summary[/]")
    .BorderColor(Color.Blue));

// Per user stats
var userTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("User")
    .AddColumn("Name")
    .AddColumn(new TableColumn("Email").RightAligned())
    .AddColumn(new TableColumn("SMS").RightAligned())
    .AddColumn(new TableColumn("Push").RightAligned())
    .AddColumn(new TableColumn("Total").RightAligned());

foreach (var user in users)
{
    var ud = deliveriesByUser.GetValueOrDefault(user.UserId);
    var emailCount = ud?.GetValueOrDefault("Email", 0) ?? 0;
    var smsCount = ud?.GetValueOrDefault("SMS", 0) ?? 0;
    var pushCount = ud?.GetValueOrDefault("Push", 0) ?? 0;

    userTable.AddRow(
        user.UserId, user.Name,
        emailCount.ToString(), smsCount.ToString(), pushCount.ToString(),
        (emailCount + smsCount + pushCount).ToString());
}

AnsiConsole.Write(new Panel(userTable)
    .Header("[cyan]Per-User Delivery Breakdown[/]")
    .BorderColor(Color.Cyan1));

// Per priority stats
var priorityTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Priority")
    .AddColumn(new TableColumn("Count").RightAligned());

foreach (var prio in new[] { "Critical", "High", "Medium", "Low" })
{
    var count = deliveriesByPriority.GetValueOrDefault(prio, 0);
    var color = prio switch
    {
        "Critical" => "red",
        "High" => "yellow",
        "Medium" => "cyan",
        "Low" => "grey",
        _ => "white",
    };

    priorityTable.AddRow($"[{color}]{prio}[/]", count.ToString("N0"));
}

AnsiConsole.Write(new Panel(priorityTable)
    .Header("[yellow]Deliveries by Priority[/]")
    .BorderColor(Color.Yellow));

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Fan-Out", "One event produces notifications on multiple channel topics");
conceptTable.AddRow("Priority Routing", "Rules engine determines channel + priority per event type");
conceptTable.AddRow("Rate Limiting", "Per-user limits prevent notification spam (3 SMS/h, 10 Push/h)");
conceptTable.AddRow("Parallel Processing", "Each channel has its own consumer processing independently");
conceptTable.AddRow("Delivery Tracking", "Status topic records all delivery outcomes");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Notification Hub Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Notification Hub demo completed![/]");
AnsiConsole.MarkupLine("[grey]Produced {0} user events, delivered across 3 channels with rate limiting.[/]",
    totalEventsProduced);
return 0;

// =====================================================================
// Channel processor
// =====================================================================

async Task RunChannelProcessorAsync(
    ISurgewaveClient client, string topic, string groupId, string channelName,
    int simulatedLatencyMs,
    ConcurrentDictionary<string, RateLimitCounter>? rateLimitCounters,
    int? maxPerHour,
    CancellationToken ct)
{
    await using var consumer = client.CreateConsumer<string, ChannelNotification>(options =>
    {
        options.GroupId = groupId;
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<ChannelNotification>();
    });

    consumer.Subscribe(topic);
    AnsiConsole.MarkupLine("[green]  {0} processor started[/]", channelName);

    try
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(200), ct);
            if (result?.Value is null) continue;

            var notification = result.Value;
            var userId = notification.UserId;

            // Check rate limit
            if (rateLimitCounters is not null && maxPerHour.HasValue)
            {
                var counter = rateLimitCounters.GetOrAdd(userId, _ => new RateLimitCounter());
                counter.Cleanup(TimeSpan.FromHours(1));

                if (counter.Count >= maxPerHour.Value)
                {
                    droppedByRateLimit.AddOrUpdate(channelName, 1, (_, v) => v + 1);
                    AnsiConsole.MarkupLine(
                        "  [red]RATE LIMITED[/] {0} -> {1} ({2}/{3} per hour)",
                        channelName, userId, counter.Count, maxPerHour.Value);
                    continue;
                }

                counter.Increment();
            }

            // Simulate sending
            await Task.Delay(simulatedLatencyMs, ct);

            // Track delivery
            deliveryStats[channelName].RecordDelivery(simulatedLatencyMs);
            deliveriesByUser.GetOrAdd(userId, _ => new ConcurrentDictionary<string, int>())
                .AddOrUpdate(channelName, 1, (_, v) => v + 1);
            deliveriesByPriority.AddOrUpdate(notification.Priority.ToString(), 1, (_, v) => v + 1);

            // Log delivery
            if (!userLookup.TryGetValue(userId, out var user)) continue;

            var target = channelName switch
            {
                "Email" => user.Email,
                "SMS" => user.Phone,
                "Push" => user.DeviceId,
                _ => "unknown",
            };

            var icon = channelName switch
            {
                "Email" => "Email",
                "SMS" => "SMS",
                "Push" => "Push",
                _ => "?",
            };

            var prioColor = notification.Priority switch
            {
                Priority.Critical => "red",
                Priority.High => "yellow",
                Priority.Medium => "cyan",
                Priority.Low => "grey",
                _ => "white",
            };

            AnsiConsole.MarkupLine(
                "  {0} [{1}]{2}[/] -> {3} | [{4}]{5}[/]",
                icon, prioColor, notification.EventType,
                Markup.Escape(target), prioColor, notification.Priority);
        }
    }
    catch (OperationCanceledException) { }
}

// =====================================================================
// Domain records
// =====================================================================

sealed record UserProfile(string UserId, string Name, string Email, string Phone, string DeviceId);
sealed record UserEvent(string UserId, string EventType, DateTimeOffset Timestamp, string Description);
sealed record NotificationRule(Channel Channel, Priority Priority);

sealed record ChannelNotification(
    string NotificationId, string UserId, string EventType,
    Channel Channel, Priority Priority, DateTimeOffset CreatedAt);

enum Channel { Email, Sms, Push }
enum Priority { Low, Medium, High, Critical }

sealed class ChannelStats
{
    private int _sent;
    private long _totalLatencyMs;

    public int Sent => _sent;
    public long TotalLatencyMs => _totalLatencyMs;

    public void RecordDelivery(int latencyMs)
    {
        Interlocked.Increment(ref _sent);
        Interlocked.Add(ref _totalLatencyMs, latencyMs);
    }
}

sealed class RateLimitCounter
{
    private readonly ConcurrentBag<DateTimeOffset> _timestamps = [];

    public int Count => _timestamps.Count;

    public void Increment() => _timestamps.Add(DateTimeOffset.UtcNow);

    public void Cleanup(TimeSpan window)
    {
        // Rate limit counters are approximate -- good enough for demo
        var cutoff = DateTimeOffset.UtcNow - window;
        var current = _timestamps.ToArray();
        if (current.Any(t => t < cutoff))
        {
            // ConcurrentBag doesn't support removal, but for this demo
            // the window is 1 hour and the demo runs 30 seconds, so
            // no entries will ever expire.
        }
    }
}
