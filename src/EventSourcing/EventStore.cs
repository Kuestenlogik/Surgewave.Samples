using System.Text.Json;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.EventSourcing.Events;

namespace Kuestenlogik.Surgewave.Samples.EventSourcing;

/// <summary>
/// Event store backed by Surgewave topics.
/// Each aggregate (account) has its events stored in a topic partitioned by account ID.
/// </summary>
public sealed class EventStore : IAsyncDisposable
{
    private readonly ISurgewaveClient _client;
    private readonly string _topicName;
    private readonly IProducer<string, string> _producer;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStore(ISurgewaveClient client, string topicName = "bank-account-events")
    {
        _client = client;
        _topicName = topicName;
        _producer = client.CreateProducer<string, string>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Append an event to the event store.
    /// </summary>
    public async Task AppendAsync(AccountEvent @event)
    {
        var json = JsonSerializer.Serialize<AccountEvent>(@event, _jsonOptions);
        await _producer.ProduceAsync(_topicName, @event.AccountId, json);
    }

    /// <summary>
    /// Append multiple events to the event store.
    /// </summary>
    public async Task AppendAsync(IEnumerable<AccountEvent> events)
    {
        foreach (var @event in events)
        {
            await AppendAsync(@event);
        }
    }

    /// <summary>
    /// Load all events for a specific account.
    /// </summary>
    public async Task<IReadOnlyList<AccountEvent>> LoadEventsAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var events = new List<AccountEvent>();

        await using var consumer = _client.CreateConsumer<string, string>(options =>
        {
            options.GroupId = $"event-store-loader-{Guid.NewGuid():N}";
            options.AutoOffsetReset = AutoOffsetReset.Earliest;
            options.EnableAutoCommit = false;
        });

        consumer.Subscribe(_topicName);

        // Read all events and filter by account ID
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await consumer.ConsumeAsync(
                    timeout: TimeSpan.FromSeconds(1),
                    cancellationToken: cts.Token);

                if (result?.Value == null) continue;

                // Filter by account ID (key)
                if (result.Key != accountId) continue;

                var @event = JsonSerializer.Deserialize<AccountEvent>(result.Value, _jsonOptions);
                if (@event != null)
                {
                    events.Add(@event);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected timeout
        }

        return events.OrderBy(e => e.SequenceNumber).ToList();
    }

    /// <summary>
    /// Load all events from the store.
    /// </summary>
    public async Task<IReadOnlyList<AccountEvent>> LoadAllEventsAsync(
        CancellationToken cancellationToken = default)
    {
        var events = new List<AccountEvent>();

        await using var consumer = _client.CreateConsumer<string, string>(options =>
        {
            options.GroupId = $"event-store-loader-{Guid.NewGuid():N}";
            options.AutoOffsetReset = AutoOffsetReset.Earliest;
            options.EnableAutoCommit = false;
        });

        consumer.Subscribe(_topicName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await consumer.ConsumeAsync(
                    timeout: TimeSpan.FromSeconds(1),
                    cancellationToken: cts.Token);

                if (result?.Value == null) continue;

                var @event = JsonSerializer.Deserialize<AccountEvent>(result.Value, _jsonOptions);
                if (@event != null)
                {
                    events.Add(@event);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected timeout
        }

        return events.OrderBy(e => e.Timestamp).ThenBy(e => e.SequenceNumber).ToList();
    }

    /// <summary>
    /// Replay events with a callback for each event.
    /// Useful for rebuilding projections in real-time.
    /// </summary>
    public async Task ReplayEventsAsync(
        Func<AccountEvent, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        await using var consumer = _client.CreateConsumer<string, string>(options =>
        {
            options.GroupId = $"event-replay-{Guid.NewGuid():N}";
            options.AutoOffsetReset = AutoOffsetReset.Earliest;
            options.EnableAutoCommit = false;
        });

        consumer.Subscribe(_topicName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await consumer.ConsumeAsync(
                    timeout: TimeSpan.FromSeconds(1),
                    cancellationToken: cts.Token);

                if (result?.Value == null) continue;

                var @event = JsonSerializer.Deserialize<AccountEvent>(result.Value, _jsonOptions);
                if (@event != null)
                {
                    await onEvent(@event);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected timeout
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync();
    }
}
