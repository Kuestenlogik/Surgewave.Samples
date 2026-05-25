using System.Runtime.CompilerServices;
using System.Text;
using Kuestenlogik.Surgewave.AI.Agents;
using Kuestenlogik.Surgewave.Samples.AgentDemo.Models;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;

/// <summary>
/// AI-powered chat agent using local Ollama LLM models.
/// Demonstrates integration with local LLMs for privacy-first AI conversations.
/// Supports conversation persistence via Surgewave checkpoint store.
/// </summary>
public sealed class OllamaChatAgent : SurgewaveAgentBase
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly Dictionary<string, List<Message>> _sessions = new();
    private readonly HashSet<string> _loadedSessions = new();

    private const string SystemPrompt = """
        You are a helpful AI assistant running locally via Ollama, integrated with Surgewave,
        a high-performance message broker. You can answer questions, help with coding tasks,
        and engage in natural conversation. Keep responses concise but informative.
        """;

    public OllamaChatAgent(OllamaApiClient client, string model = "llama3.2")
    {
        _client = client;
        _model = model;
    }

    private List<Message> GetOrCreateSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var history))
        {
            history = [new Message(ChatRole.System, SystemPrompt)];
            _sessions[sessionId] = history;
        }
        return history;
    }

    private async Task<List<Message>> LoadSessionAsync(string sessionId, SurgewaveAgentContext context)
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
                    history.Add(new Message(role, msg.Content));
                }
                Console.WriteLine($"  (Restored {saved.Messages.Count} messages from previous session)");
            }
        }

        return history;
    }

    private async Task SaveSessionAsync(string sessionId, List<Message> history, SurgewaveAgentContext context)
    {
        var state = new ConversationState
        {
            ConversationId = sessionId,
            LastUpdatedAt = DateTime.UtcNow,
            Messages = history.Select(m => new ConversationMessage
            {
                Role = m.Role.ToString() ?? "user",
                Content = m.Content ?? ""
            }).ToList()
        };
        await context.SaveStateAsync($"conversation:{sessionId}", state);
    }

    public override string AgentId => "ollama-chat-agent";

    public override string Name => "Ollama Chat Agent";

    public override string Description => $"A local AI-powered conversational agent using Ollama ({_model}).";

    public override IReadOnlyList<AgentSkill> Skills =>
    [
        new AgentSkill
        {
            Id = "local-chat",
            Name = "Local Chat",
            Description = "Engages in natural language conversation using local LLM.",
            Tags = ["ai", "chat", "local", "ollama"]
        },
        new AgentSkill
        {
            Id = "private-ai",
            Name = "Private AI",
            Description = "All processing happens locally - no data sent to external APIs.",
            Tags = ["ai", "privacy", "local"]
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
            history.Add(new Message(ChatRole.System, SystemPrompt));
            await SaveSessionAsync(context.SessionId, history, context);
            return TextResponse("Conversation history cleared.");
        }

        if (message.Content.Equals("/history", StringComparison.OrdinalIgnoreCase))
        {
            var count = history.Count - 1; // Exclude system message
            return TextResponse($"Current session has {count} message(s). Session ID: {context.SessionId}");
        }

        if (message.Content.Equals("/models", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var models = await _client.ListLocalModelsAsync(cancellationToken);
                var modelList = string.Join("\n", models.Select(m => $"  - {m.Name}"));
                return TextResponse($"Available models:\n{modelList}");
            }
            catch (Exception ex)
            {
                return TextResponse($"Error listing models: {ex.Message}");
            }
        }

        // Add user message to history
        history.Add(new Message(ChatRole.User, message.Content));

        try
        {
            var chat = new ChatRequest
            {
                Model = _model,
                Messages = history,
                Stream = false
            };

            // Collect all response chunks
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _client.ChatAsync(chat, cancellationToken))
            {
                if (chunk?.Message?.Content != null)
                {
                    responseBuilder.Append(chunk.Message.Content);
                }
            }

            var responseText = responseBuilder.ToString();

            // Add assistant response to history
            history.Add(new Message(ChatRole.Assistant, responseText));

            // Save after successful response
            await SaveSessionAsync(context.SessionId, history, context);

            return TextResponse(responseText);
        }
        catch (Exception ex)
        {
            // Remove the failed user message from history
            history.RemoveAt(history.Count - 1);
            return TextResponse($"Error communicating with Ollama: {ex.Message}");
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
            history.Add(new Message(ChatRole.System, SystemPrompt));
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

        if (message.Content.Equals("/models", StringComparison.OrdinalIgnoreCase))
        {
            var response = await ProcessMessageAsync(message, context, cancellationToken);
            yield return new AgentResponseChunk
            {
                Content = response.Content,
                IsFinal = true
            };
            yield break;
        }

        // Add user message to history
        history.Add(new Message(ChatRole.User, message.Content));

        var chat = new ChatRequest
        {
            Model = _model,
            Messages = history,
            Stream = true
        };

        var fullResponse = new StringBuilder();

        await foreach (var chunk in _client.ChatAsync(chat, cancellationToken))
        {
            if (chunk?.Message?.Content != null)
            {
                fullResponse.Append(chunk.Message.Content);
                yield return new AgentResponseChunk
                {
                    Content = chunk.Message.Content,
                    IsFinal = false
                };
            }
        }

        // Add assistant response to history
        history.Add(new Message(ChatRole.Assistant, fullResponse.ToString()));

        // Save after successful response
        await SaveSessionAsync(context.SessionId, history, context);

        yield return new AgentResponseChunk
        {
            Content = "",
            IsFinal = true
        };
    }
}
