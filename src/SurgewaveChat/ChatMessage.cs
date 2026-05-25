namespace Kuestenlogik.Surgewave.Samples.SurgewaveChat;

/// <summary>
/// Represents a chat message in a room.
/// </summary>
public sealed record ChatMessage
{
    /// <summary>
    /// Unique message ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The room/channel this message belongs to.
    /// </summary>
    public required string Room { get; init; }

    /// <summary>
    /// Username of the sender.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Message type (message, join, leave, system).
    /// </summary>
    public string Type { get; init; } = "message";
}
