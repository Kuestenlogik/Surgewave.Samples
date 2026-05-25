using Kuestenlogik.Surgewave.AI.Agents;
using Kuestenlogik.Surgewave.AI.Agents.Runtime;
using Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using OpenAI;

// =====================================================================
// AI AGENTS -- Multi-Agent Interactive Demo
// =====================================================================
// Demonstrates Surgewave's AI agent hosting runtime with multiple agent
// types. Agents checkpoint their state to Surgewave topics, enabling
// crash recovery and conversation persistence. Supports OpenAI,
// Ollama, and custom agent implementations.
// =====================================================================

var builder = Host.CreateApplicationBuilder(args);

// ============= STEP 1: Configure Agent Hosting =============
// AddSurgewaveAgentHosting() sets up the agent runtime with checkpointing.
// Agent state is persisted to Surgewave topics (__agent_checkpoints, __agent_workflows)
// so conversations survive restarts.
builder.Services.AddSurgewaveAgentHosting(options =>
{
    options.CheckpointIntervalMs = 5000;       // Checkpoint state every 5 seconds
    options.ConversationHistoryLimit = 100;     // Keep last 100 messages in memory
});

// ============= STEP 2: Register Agent Implementations =============
// Each agent is a DI-registered service discovered by the runtime.
builder.Services.AddSurgewaveAgent<EchoAgent>();
builder.Services.AddSurgewaveAgent<WeatherAgent>();
builder.Services.AddSurgewaveAgent<ConversationalAgent>();

// ============= STEP 3: Configure LLM Providers =============
// Register OpenAI chat client if API key is available
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (!string.IsNullOrEmpty(openAiApiKey))
{
    builder.Services.AddSingleton<IChatClient>(_ =>
        new OpenAIClient(openAiApiKey)
            .GetChatClient("gpt-4o-mini")
            .AsIChatClient());
    builder.Services.AddSurgewaveAgent<OpenAIChatAgent>();
}

// Register Ollama chat client for local LLM
var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2";
builder.Services.AddSingleton(_ => new OllamaApiClient(new Uri(ollamaUrl)));
builder.Services.AddSingleton(sp => new OllamaChatAgent(
    sp.GetRequiredService<OllamaApiClient>(),
    ollamaModel));
builder.Services.AddSingleton<ISurgewaveAgent>(sp => sp.GetRequiredService<OllamaChatAgent>());

var host = builder.Build();

// Run the interactive demo
await RunInteractiveDemo(host.Services);

static async Task RunInteractiveDemo(IServiceProvider services)
{
    var runtime = services.GetRequiredService<ISurgewaveAgentRuntime>();

    var hasOpenAi = runtime.GetAgent("openai-chat-agent") is not null;
    var hasOllama = runtime.GetAgent("ollama-chat-agent") is not null;

    ShowHelp(runtime, hasOpenAi, hasOllama);

    var currentAgentId = "echo-agent";
    var conversationId = Guid.NewGuid().ToString();
    var streamingMode = false;

    while (true)
    {
        Console.Write($"[{currentAgentId}]> ");
        var input = Console.ReadLine();

        // Exit if stdin is closed (non-interactive mode)
        if (input is null)
        {
            Console.WriteLine("\nNo input available. Exiting.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
            continue;

        // Handle commands
        if (input.StartsWith('/'))
        {
            var parts = input.Split(' ', 2);
            var command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "/quit":
                case "/exit":
                    Console.WriteLine("Goodbye!");
                    return;

                case "/list":
                    Console.WriteLine("\nRegistered agents:");
                    foreach (var agent in runtime.ListAgents())
                    {
                        var marker = agent.AgentId == currentAgentId ? "* " : "  ";
                        Console.WriteLine($"  {marker}{agent.AgentId} - {agent.Name}");
                        if (!string.IsNullOrEmpty(agent.Description))
                            Console.WriteLine($"      {agent.Description}");
                    }
                    Console.WriteLine();
                    continue;

                case "/switch":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Usage: /switch <agent-id>");
                        continue;
                    }
                    var newAgentId = parts[1].Trim();
                    var newAgent = runtime.GetAgent(newAgentId);
                    if (newAgent is null)
                    {
                        Console.WriteLine($"Agent '{newAgentId}' not found. Use /list to see available agents.");
                        continue;
                    }
                    currentAgentId = newAgentId;
                    conversationId = Guid.NewGuid().ToString(); // New conversation
                    Console.WriteLine($"Switched to {newAgent.Name}");
                    continue;

                case "/stream":
                    streamingMode = !streamingMode;
                    Console.WriteLine($"Streaming mode: {(streamingMode ? "ON" : "OFF")}");
                    continue;

                case "/session":
                    Console.WriteLine($"Current session ID: {conversationId}");
                    Console.WriteLine("  (Use this ID with /resume to continue this conversation later)");
                    continue;

                case "/resume":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Usage: /resume <session-id>");
                        continue;
                    }
                    conversationId = parts[1].Trim();
                    Console.WriteLine($"Resumed session: {conversationId}");
                    Console.WriteLine("  (Previous messages will be loaded on next message)");
                    continue;

                case "/new":
                    conversationId = Guid.NewGuid().ToString();
                    Console.WriteLine($"Started new conversation. Session ID: {conversationId}");
                    continue;

                case "/help":
                case "/h":
                case "/?":
                    ShowHelp(runtime, hasOpenAi, hasOllama);
                    continue;

                case "/history":
                    Console.WriteLine($"Session {conversationId} - Use /list to see agents");
                    continue;

                case "/clear":
                    Console.Clear();
                    continue;

                default:
                    Console.WriteLine($"Unknown command: {command}. Type /help for available commands.");
                    continue;
            }
        }

        // Send message to agent
        var message = new AgentMessage { Content = input };

        try
        {
            if (streamingMode)
            {
                Console.Write("Agent: ");
                await foreach (var chunk in runtime.ProcessStreamingMessageAsync(
                    currentAgentId, message, conversationId))
                {
                    Console.Write(chunk.Content);
                }
                Console.WriteLine();
            }
            else
            {
                var response = await runtime.ProcessMessageAsync(
                    currentAgentId, message, conversationId);
                Console.WriteLine($"Agent: {response.Content}");

                if (response.RequiresInput)
                {
                    Console.WriteLine("  (Agent is waiting for more input)");
                }

                if (response.Artifacts?.Count > 0)
                {
                    Console.WriteLine($"  (Received {response.Artifacts.Count} artifact(s))");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }
}

static void ShowHelp(ISurgewaveAgentRuntime runtime, bool hasOpenAi, bool hasOllama)
{
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║           Surgewave Agent Framework - Examples Demo              ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  Available Agents:                                           ║");
    Console.WriteLine("║    1. echo-agent           - Echoes your messages            ║");
    Console.WriteLine("║    2. weather-agent        - Provides weather info           ║");
    Console.WriteLine("║    3. conversational-agent - Remembers your name             ║");
    if (hasOpenAi)
    {
        Console.WriteLine("║    4. openai-chat-agent    - AI chat (GPT-4o-mini)           ║");
    }
    else
    {
        Console.WriteLine("║    (Set OPENAI_API_KEY to enable OpenAI agent)               ║");
    }
    if (hasOllama)
    {
        Console.WriteLine("║    5. ollama-chat-agent    - Local AI chat (Ollama)          ║");
    }
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  Commands:                                                   ║");
    Console.WriteLine("║    /switch <agent-id>   - Switch to a different agent        ║");
    Console.WriteLine("║    /list                - List all agents                    ║");
    Console.WriteLine("║    /stream              - Toggle streaming mode              ║");
    Console.WriteLine("║    /session             - Show current session ID            ║");
    Console.WriteLine("║    /resume <session-id> - Resume a previous conversation     ║");
    Console.WriteLine("║    /new                 - Start a new conversation           ║");
    Console.WriteLine("║    /history             - Show message count in session      ║");
    Console.WriteLine("║    /clear               - Clear conversation history         ║");
    Console.WriteLine("║    /help                - Show this help message             ║");
    Console.WriteLine("║    /quit                - Exit the demo                      ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
}
