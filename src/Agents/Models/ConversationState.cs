namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Models;

/// <summary>
/// Serializable conversation state for persistence.
/// </summary>
public sealed class ConversationState
{
    /// <summary>
    /// The conversation ID.
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// When the conversation was started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The messages in the conversation.
    /// </summary>
    public List<ConversationMessage> Messages { get; init; } = [];
}

/// <summary>
/// A single message in a conversation.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// The role of the message sender (system, user, assistant).
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
