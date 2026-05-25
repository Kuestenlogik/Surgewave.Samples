# AI Agents Sample

Demonstrates Surgewave's AI agent hosting runtime with multiple agent types including OpenAI and Ollama integration.

## Use Case

Build stateful, scalable AI agents that maintain conversation history, checkpoint their state for crash recovery, and support multiple LLM providers. This sample shows how to run an interactive multi-agent system where users can switch between agents, resume conversations, and toggle streaming mode.

## What It Does

- **EchoAgent**: Simple agent demonstrating basic structure
- **WeatherAgent**: Agent with skills, metadata, and artifacts
- **ConversationalAgent**: Multi-turn conversation with checkpointing
- **OpenAIChatAgent**: GPT-4o-mini powered chat via Microsoft.Extensions.AI
- **OllamaChatAgent**: Local LLM chat using Ollama (privacy-first)

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Set OpenAI key (for OpenAI agent)
set OPENAI_API_KEY=sk-...

# Run the sample
dotnet run --project samples/Agents
```

## Why Surgewave for AI Agents?

### Durable Agent State

| Benefit | Description |
|---------|-------------|
| **Checkpoint Persistence** | Agent state survives restarts via Surgewave topics |
| **Conversation History** | Multi-turn conversations stored durably |
| **Workflow State** | Complex multi-step agent processes tracked |
| **Audit Trail** | Complete history of agent interactions |

### Scalable Agent Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Agent 1   │     │   Agent 2   │     │   Agent N   │
│  (OpenAI)   │     │  (Ollama)   │     │  (Custom)   │
└──────┬──────┘     └──────┬──────┘     └──────┬──────┘
       │                   │                   │
       ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────┐
│                    Surgewave Broker                      │
│  ┌─────────────────────────────────────────────┐   │
│  │              Agent State Topics              │   │
│  │  • __agent_checkpoints (state snapshots)    │   │
│  │  • __agent_workflows (workflow state)       │   │
│  │  • __agent_cards (agent discovery)          │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### Event-Driven Agent Communication

| Feature | Benefit |
|---------|---------|
| **Agent-to-Agent (A2A)** | Agents communicate via Surgewave topics |
| **Decoupled Scaling** | Add/remove agents independently |
| **Load Balancing** | Consumer groups distribute agent workload |
| **Replay Capability** | Re-process agent interactions for debugging |

### Comparison with Other Approaches

| Approach | State Management | Scaling | Recovery |
|----------|-----------------|---------|----------|
| In-Memory Agents | Lost on restart | Manual | None |
| Database-backed | Extra infra | Complex | Manual |
| **Surgewave Agents** | **Built-in** | **Automatic** | **Replay** |

### Multi-LLM Support

Surgewave's agent framework supports multiple LLM providers:

| Provider | Use Case | Cost |
|----------|----------|------|
| OpenAI | Production, high quality | Per-token |
| Ollama | Local development, privacy | Free |
| Azure OpenAI | Enterprise, compliance | Per-token |
| Custom | Specialized models | Varies |

### Key Benefits

1. **Stateful Agents**: Checkpointing enables long-running conversations
2. **Event Sourcing**: All agent interactions recorded for replay
3. **Horizontal Scaling**: Add agent instances via consumer groups
4. **Provider Flexibility**: Switch LLMs without changing agent code
5. **Operational Simplicity**: No separate state store needed

## Prerequisites

- .NET 10 SDK
- Surgewave broker running on `localhost:9092`
- (Optional) `OPENAI_API_KEY` environment variable for OpenAI agent
- (Optional) Ollama running locally for Ollama agent

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Agent Hosting Runtime | `AddSurgewaveAgentHosting()` with DI registration | Managed agent lifecycle with dependency injection |
| Agent Registration | `AddSurgewaveAgent<T>()` for each agent type | Plug-in architecture for custom agent implementations |
| Checkpointing | `CheckpointIntervalMs = 5000` | Agent state survives restarts -- no separate database needed |
| Conversation History | `ConversationHistoryLimit = 100` | Multi-turn conversations stored durably via Surgewave topics |
| Streaming Responses | `ProcessStreamingMessageAsync()` with `IAsyncEnumerable` | Token-by-token output for responsive UX |
| Session Management | `conversationId` tracks each conversation | Resume conversations across restarts with `/resume` |
| Multi-LLM Support | OpenAI and Ollama agents via `IChatClient` | Switch LLM providers without changing agent code |

## Key Code Highlights

### Agent Registration with DI

```csharp
builder.Services.AddSurgewaveAgentHosting(options =>
{
    options.CheckpointIntervalMs = 5000;
    options.ConversationHistoryLimit = 100;
});
builder.Services.AddSurgewaveAgent<EchoAgent>();
builder.Services.AddSurgewaveAgent<WeatherAgent>();
```

### Streaming Agent Responses

```csharp
await foreach (var chunk in runtime.ProcessStreamingMessageAsync(
    currentAgentId, message, conversationId))
{
    Console.Write(chunk.Content);
}
```

## Key Takeaway

**Surgewave provides durable, scalable agent infrastructure without the complexity of managing separate databases or state stores.**
