using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;

namespace Kuestenlogik.Surgewave.Samples.SurgewaveChat;

/// <summary>
/// Represents a chat room backed by a Surgewave topic.
/// Each room is a separate topic, allowing independent message streams.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler instances - Action<T> is simpler for samples
public sealed class ChatRoom : IAsyncDisposable
{
    private readonly ISurgewaveClient _client;
    private readonly string _roomName;
    private readonly string _username;
    private readonly string _topicName;

    private IProducer<string, ChatMessage>? _producer;
    private IConsumer<string, ChatMessage>? _consumer;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;

    /// <summary>
    /// Event raised when a message is received in this room.
    /// </summary>
    public event Action<ChatMessage>? OnMessageReceived;

    /// <summary>
    /// Gets the room name.
    /// </summary>
    public string Name => _roomName;

    /// <summary>
    /// Gets whether the room is currently active (joined).
    /// </summary>
    public bool IsJoined { get; private set; }

    public ChatRoom(ISurgewaveClient client, string roomName, string username)
    {
        _client = client;
        _roomName = roomName;
        _username = username;
        _topicName = $"chat-{roomName}";
    }

    /// <summary>
    /// Join the room and start receiving messages.
    /// </summary>
    public async Task JoinAsync()
    {
        if (IsJoined)
            return;

        // Create producer for sending messages
        _producer = _client.CreateProducer<string, ChatMessage>(options =>
        {
            options.ValueSerializer = Serializers.Json<ChatMessage>();
        });

        // Create consumer with unique group per user to receive all messages
        // Using unique group ID means each user gets all messages (broadcast pattern)
        var groupId = $"chat-{_roomName}-{_username}-{Guid.NewGuid():N}";

        _consumer = _client.CreateConsumer<string, ChatMessage>(options =>
        {
            options.GroupId = groupId;
            options.AutoOffsetReset = AutoOffsetReset.Latest; // Only new messages
            options.EnableAutoCommit = true;
            options.ValueDeserializer = Serializers.JsonDeserializer<ChatMessage>();
        });

        _consumer.Subscribe(_topicName);

        // Start consuming messages in background
        _consumerCts = new CancellationTokenSource();
        _consumerTask = ConsumeMessagesAsync(_consumerCts.Token);

        IsJoined = true;

        // Send join notification
        await SendSystemMessageAsync("join", $"{_username} joined the room");
    }

    /// <summary>
    /// Leave the room and stop receiving messages.
    /// </summary>
    public async Task LeaveAsync()
    {
        if (!IsJoined)
            return;

        // Send leave notification
        await SendSystemMessageAsync("leave", $"{_username} left the room");

        // Stop consumer
        _consumerCts?.Cancel();
        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Cleanup
        if (_consumer != null)
        {
            await _consumer.DisposeAsync();
            _consumer = null;
        }

        if (_producer != null)
        {
            await _producer.DisposeAsync();
            _producer = null;
        }

        _consumerCts?.Dispose();
        _consumerCts = null;

        IsJoined = false;
    }

    /// <summary>
    /// Send a message to the room.
    /// </summary>
    public async Task SendMessageAsync(string content)
    {
        if (!IsJoined || _producer == null)
            throw new InvalidOperationException("Not joined to room");

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Room = _roomName,
            Username = _username,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow,
            Type = "message"
        };

        await _producer.ProduceAsync(_topicName, _username, message);
    }

    private async Task SendSystemMessageAsync(string type, string content)
    {
        if (_producer == null)
            return;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Room = _roomName,
            Username = _username,
            Content = content,
            Timestamp = DateTimeOffset.UtcNow,
            Type = type
        };

        await _producer.ProduceAsync(_topicName, _username, message);
    }

    private async Task ConsumeMessagesAsync(CancellationToken cancellationToken)
    {
        if (_consumer == null)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _consumer.ConsumeAsync(
                    timeout: TimeSpan.FromSeconds(1),
                    cancellationToken: cancellationToken);

                if (result?.Value != null)
                {
                    OnMessageReceived?.Invoke(result.Value);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Ignore consume errors, keep trying
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (IsJoined)
        {
            await LeaveAsync();
        }
    }
}
