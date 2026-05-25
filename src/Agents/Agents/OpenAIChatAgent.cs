using System.Runtime.CompilerServices;
using Kuestenlogik.Surgewave.AI.Agents;
using Kuestenlogik.Surgewave.Samples.AgentDemo.Models;
using Microsoft.Extensions.AI;

namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;

/// <summary>
/// AI-powered chat agent using OpenAI GPT models via Microsoft.Extensions.AI.
/// Demonstrates integration with LLM APIs for intelligent conversations.
/// Supports conversation persistence via Surgewave checkpoint store.
/// </summary>
public sealed class OpenAIChatAgent : SurgewaveAgentBase
{
    private readonly IChatClient _chatClient;
    private readonly Dictionary<string, List<ChatMessage>> _sessions = new();
    private readonly HashSet<string> _loadedSessions = new();

    private const string SystemPrompt = """
        You are a helpful AI assistant integrated with Surgewave, a high-performance
        message broker. You can answer questions, help with coding tasks, and
        engage in natural conversation. Keep responses concise but informative.
        """;

    public OpenAIChatAgent(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    private List<ChatMessage> GetOrCreateSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
        {
            history = [new ChatMessage(ChatRole.System, SystemPrompt)];
            _sessions[sessionId] = history;
        }
        return history;
    }

    private async Task<List<ChatMessage>> LoadSessionAsync(string sessionId, SurgewaveAgentContext context)
    {
        var history = GetOrCreateSession(sessionId);

        // Only load from checkpoint store once per session
        if (_loadedSessions.Add(sessionId))
        {
            var saved = await context.LoadStateAsync<ConversationState>($"conversation:{sessionId}");
            if (saved != null && saved.Messages.Count > 0)
            {
                history.Clear();
                foreach (var msg in saved.Messages)
                {
                    var role = msg.Role.ToLowerInvariant() switch
                    {
                        "system" => ChatRole.System,
                        "user" => ChatRole.User,
                        "assistant" => ChatRole.Assistant,
                        _ => ChatRole.User
                    };
                    history.Add(new ChatMessage(role, msg.Content));
                }
                Console.WriteLine($"  (Restored {saved.Messages.Count} messages from previous session)");
            }
        }

        return history;
    }

    private async Task SaveSessionAsync(string sessionId, List<ChatMessage> history, SurgewaveAgentContext context)
    {
        var state = new ConversationState
        {
            ConversationId = sessionId,
            LastUpdatedAt = DateTime.UtcNow,
            Messages = history.Select(m => new ConversationMessage
            {
                Role = m.Role.ToString(),
                Content = m.Text ?? ""
            }).ToList()
        };
        await context.SaveStateAsync($"conversation:{sessionId}", state);
    }

    public override string AgentId => "openai-chat-agent";

    public override string Name => "OpenAI Chat Agent";

    public override string Description => "An AI-powered conversational agent using OpenAI GPT models.";

    public override IReadOnlyList<AgentSkill> Skills =>
    [
        new AgentSkill
        {
            Id = "chat",
            Name = "Chat",
            Description = "Engages in natural language conversation powered by GPT.",
            Tags = ["ai", "chat", "gpt"]
        },
        new AgentSkill
        {
            Id = "code-help",
            Name = "Code Help",
            Description = "Assists with coding questions and tasks.",
            Tags = ["ai", "coding", "help"]
        }
    ];

    public override async Task<AgentResponse> ProcessMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var history = await LoadSessionAsync(context.SessionId, context);

        // Handle special commands
        if (message.Content.Equals("/clear", StringComparison.OrdinalIgnoreCase))
        {
            history.Clear();
            history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
            await SaveSessionAsync(context.SessionId, history, context);
            return TextResponse("Conversation history cleared.");
        }

        if (message.Content.Equals("/history", StringComparison.OrdinalIgnoreCase))
        {
            var count = history.Count - 1; // Exclude system message
            return TextResponse($"Current session has {count} message(s). Session ID: {context.SessionId}");
        }

        // Add user message to history
        history.Add(new ChatMessage(ChatRole.User, message.Content));

        try
        {
            // Call OpenAI
            var response = await _chatClient.GetResponseAsync(
                history,
                cancellationToken: cancellationToken);

            // Add assistant response to history
            history.Add(new ChatMessage(ChatRole.Assistant, response.Text ?? ""));

            // Save after successful response
            await SaveSessionAsync(context.SessionId, history, context);

            return TextResponse(response.Text ?? "No response generated.");
        }
        catch (Exception ex)
        {
            // Remove the failed user message from history
            history.RemoveAt(history.Count - 1);
            return TextResponse($"Error communicating with OpenAI: {ex.Message}");
        }
    }

    public override async IAsyncEnumerable<AgentResponseChunk> ProcessStreamingMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = await LoadSessionAsync(context.SessionId, context);

        // Handle special commands
        if (message.Content.Equals("/clear", StringComparison.OrdinalIgnoreCase))
        {
            history.Clear();
            history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
            await SaveSessionAsync(context.SessionId, history, context);
            yield return new AgentResponseChunk
            {
                Content = "Conversation history cleared.",
                IsFinal = true
            };
            yield break;
        }

        if (message.Content.Equals("/history", StringComparison.OrdinalIgnoreCase))
        {
            var count = history.Count - 1;
            yield return new AgentResponseChunk
            {
                Content = $"Current session has {count} message(s). Session ID: {context.SessionId}",
                IsFinal = true
            };
            yield break;
        }

        // Add user message to history
        history.Add(new ChatMessage(ChatRole.User, message.Content));

        var fullResponse = new System.Text.StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(
            history,
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullResponse.Append(update.Text);
                yield return new AgentResponseChunk
                {
                    Content = update.Text,
                    IsFinal = false
                };
            }
        }

        // Add assistant response to history
        history.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));

        // Save after successful response
        await SaveSessionAsync(context.SessionId, history, context);

        yield return new AgentResponseChunk
        {
            Content = "",
            IsFinal = true
        };
    }
}
