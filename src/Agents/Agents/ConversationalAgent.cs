using System.Runtime.CompilerServices;
using Kuestenlogik.Surgewave.AI.Agents;
using Kuestenlogik.Surgewave.AI.Agents.Checkpointing;

namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;

/// <summary>
/// Conversational agent that maintains state across messages.
/// Demonstrates checkpointing, streaming, and multi-turn conversation.
/// </summary>
public sealed class ConversationalAgent : SurgewaveAgentBase
{
    private readonly ICheckpointStore _checkpointStore;

    public ConversationalAgent(ICheckpointStore checkpointStore)
    {
        _checkpointStore = checkpointStore;
    }

    public override string AgentId => "conversational-agent";

    public override string Name => "Conversational Agent";

    public override string Description => "A conversational agent that remembers your name and tracks message count.";

    public override IReadOnlyList<AgentSkill> Skills =>
    [
        new AgentSkill
        {
            Id = "conversation",
            Name = "Conversation",
            Description = "Engages in multi-turn conversation with memory.",
            Tags = ["chat", "memory"]
        },
        new AgentSkill
        {
            Id = "remember-name",
            Name = "Remember Name",
            Description = "Remembers the user's name across sessions.",
            Tags = ["memory", "personalization"]
        }
    ];

    public override async Task<AgentResponse> ProcessMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        CancellationToken cancellationToken = default)
    {
        // Load conversation state from checkpoint
        var state = await _checkpointStore.LoadAsync<ConversationState>(
            AgentId, context.SessionId, cancellationToken) ?? new ConversationState();

        state.MessageCount++;
        var content = message.Content.Trim();

        string response;

        // Check for name introduction
        if (content.StartsWith("my name is ", StringComparison.OrdinalIgnoreCase))
        {
            state.UserName = content[11..].Trim();
            response = $"Nice to meet you, {state.UserName}! I'll remember your name.";
        }
        else if (content.Equals("what is my name?", StringComparison.OrdinalIgnoreCase) ||
                 content.Equals("who am i?", StringComparison.OrdinalIgnoreCase))
        {
            response = string.IsNullOrEmpty(state.UserName)
                ? "I don't know your name yet. Tell me by saying 'My name is [your name]'."
                : $"Your name is {state.UserName}.";
        }
        else if (content.Equals("how many messages?", StringComparison.OrdinalIgnoreCase) ||
                 content.Equals("message count", StringComparison.OrdinalIgnoreCase))
        {
            response = $"We've exchanged {state.MessageCount} messages in this conversation.";
        }
        else if (content.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            await _checkpointStore.DeleteAsync(AgentId, context.SessionId, cancellationToken);
            return TextResponse("Conversation state has been reset.");
        }
        else
        {
            var greeting = string.IsNullOrEmpty(state.UserName)
                ? "Hello!"
                : $"Hello, {state.UserName}!";
            response = $"{greeting} You said: \"{content}\" (Message #{state.MessageCount})";
        }

        // Save updated state
        await _checkpointStore.SaveAsync(AgentId, context.SessionId, state, cancellationToken);

        return TextResponse(response);
    }

    public override async IAsyncEnumerable<AgentResponseChunk> ProcessStreamingMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Demonstrate streaming by yielding response word by word
        var response = await ProcessMessageAsync(message, context, cancellationToken);
        var words = response.Content.Split(' ');

        for (var i = 0; i < words.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var word = i == words.Length - 1 ? words[i] : words[i] + " ";
            yield return new AgentResponseChunk
            {
                Content = word,
                IsFinal = i == words.Length - 1
            };

            // Simulate thinking delay
            await Task.Delay(50, cancellationToken);
        }
    }

    private sealed class ConversationState
    {
        public string? UserName { get; set; }
        public int MessageCount { get; set; }
    }
}
