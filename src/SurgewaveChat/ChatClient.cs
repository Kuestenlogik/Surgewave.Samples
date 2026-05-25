using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;

namespace Kuestenlogik.Surgewave.Samples.SurgewaveChat;

/// <summary>
/// Chat client that manages multiple rooms and handles user interaction.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler instances - Action<T> is simpler for samples
#pragma warning disable CA2213 // _currentRoom is disposed via _rooms dictionary in DisposeAsync
public sealed class ChatClient : IAsyncDisposable
{
    private readonly ISurgewaveClient _client;
    private readonly string _username;
    private readonly Dictionary<string, ChatRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private ChatRoom? _currentRoom;

    /// <summary>
    /// Event raised when a message is received in any joined room.
    /// </summary>
    public event Action<ChatMessage>? OnMessageReceived;

    /// <summary>
    /// Gets the username.
    /// </summary>
    public string Username => _username;

    /// <summary>
    /// Gets the current active room.
    /// </summary>
    public ChatRoom? CurrentRoom => _currentRoom;

    /// <summary>
    /// Gets all joined rooms.
    /// </summary>
    public IReadOnlyCollection<ChatRoom> JoinedRooms
    {
        get
        {
            lock (_lock)
            {
                return _rooms.Values.Where(r => r.IsJoined).ToList();
            }
        }
    }

    public ChatClient(ISurgewaveClient client, string username)
    {
        _client = client;
        _username = username;
    }

    /// <summary>
    /// Join a chat room. Creates the room if it doesn't exist.
    /// </summary>
    public async Task<ChatRoom> JoinRoomAsync(string roomName)
    {
        ChatRoom room;

        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomName, out room!))
            {
                room = new ChatRoom(_client, roomName, _username);
                room.OnMessageReceived += OnRoomMessageReceived;
                _rooms[roomName] = room;
            }
        }

        if (!room.IsJoined)
        {
            await room.JoinAsync();
        }

        _currentRoom = room;
        return room;
    }

    /// <summary>
    /// Leave a chat room.
    /// </summary>
    public async Task LeaveRoomAsync(string roomName)
    {
        ChatRoom? room;

        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomName, out room))
            {
                return;
            }
        }

        if (room.IsJoined)
        {
            await room.LeaveAsync();
        }

        if (_currentRoom == room)
        {
            // Switch to another room or null
            _currentRoom = _rooms.Values.FirstOrDefault(r => r.IsJoined && r != room);
        }
    }

    /// <summary>
    /// Switch to a different room (must already be joined).
    /// </summary>
    public bool SwitchRoom(string roomName)
    {
        lock (_lock)
        {
            if (_rooms.TryGetValue(roomName, out var room) && room.IsJoined)
            {
                _currentRoom = room;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Send a message to the current room.
    /// </summary>
    public async Task SendMessageAsync(string content)
    {
        if (_currentRoom == null || !_currentRoom.IsJoined)
        {
            throw new InvalidOperationException("Not in any room. Use /join <room> first.");
        }

        await _currentRoom.SendMessageAsync(content);
    }

    private void OnRoomMessageReceived(ChatMessage message)
    {
        OnMessageReceived?.Invoke(message);
    }

    public async ValueTask DisposeAsync()
    {
        List<ChatRoom> rooms;
        lock (_lock)
        {
            rooms = _rooms.Values.ToList();
            _rooms.Clear();
        }

        foreach (var room in rooms)
        {
            room.OnMessageReceived -= OnRoomMessageReceived;
            await room.DisposeAsync();
        }

        _currentRoom = null;
    }
}
