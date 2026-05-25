# Agent Memory Demo

Demonstrates Surgewave.AI's agent memory subsystem: storing facts, preferences, and episodic memories, recalling by text query or type, conversation summarization, and tool result caching.

## Use Case

AI agents need persistent memory to maintain context across conversations. This sample shows how to give agents the ability to remember user preferences, learned facts, and past interactions -- enabling personalized, context-aware responses without an external database.

## How to Run

```bash
dotnet run --project src/AgentMemoryDemo
```

No external dependencies required. No API keys needed.

## Architecture

```
  User Interaction
        |
        v
+-------------------+       +---------------------+
| AgentMemoryContext | ----> | InMemoryAgentMemory |
|                   |       | Store               |
| - SaveFact()      |       | - Facts             |
| - SavePreference()|       | - Preferences       |
| - SaveEpisode()   |       | - Episodes          |
| - RecallAsync()   |       +---------------------+
+-------------------+
        |
        v
+-------------------+       +---------------------+
| Conversation      | ----> | ConversationSummary |
| History           |       | (extractive, no LLM)|
+-------------------+       +---------------------+
        |
        v
+-------------------+       +---------------------+
| CachedAgentTool   | ----> | InMemoryToolResult  |
| (decorator)       |       | Cache               |
+-------------------+       +---------------------+
```

## What to Expect

1. Memory store created with configurable limits and auto-summarization
2. Facts, preferences, and episodic memories saved with importance scores
3. Text-based recall returns relevant memories ranked by content match
4. Type-based recall filters by LearnedFact, UserPreference, or Episodic
5. Memory summary shows statistics (total, by type, oldest/newest)
6. Conversation summarization extracts key points without requiring an LLM
7. Tool result caching demonstrates cache hits/misses with TTL
8. Final cache statistics show hit rate

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Agent Memory Store | `InMemoryAgentMemoryStore` with `AgentMemoryContext` | Persistent agent memory without external databases |
| Memory Types | Facts, Preferences, Episodes with importance scoring | Structured memory enables targeted recall |
| Text-Based Recall | `RecallAsync(query)` searches by content relevance | Agents retrieve relevant context from past interactions |
| Type-Based Recall | `RecallByTypeAsync(MemoryType)` filters by classification | Separate user preferences from learned facts |
| Conversation Summarization | `ConversationSummarizer.Summarize()` -- extractive, no LLM | Compress long conversations without API costs |
| Tool Result Caching | `CachedAgentTool` decorator with TTL and hit tracking | Avoid redundant API calls, reduce latency and cost |
| Auto-Summarization | `AutoSummarizeConversations` option triggers at threshold | Keep memory compact as conversations grow |

## Key Code Highlights

### Memory Setup and Storage

```csharp
var memoryStore = new InMemoryAgentMemoryStore();
var memoryContext = new AgentMemoryContext(memoryStore, agentId, memoryOptions);

// Store different memory types with importance scores
await memoryContext.SaveFactAsync("Surgewave supports Kafka-compatible protocols", 0.8f);
await memoryContext.SavePreferenceAsync("User prefers concise answers");
await memoryContext.SaveEpisodeAsync("User deployed a 3-node Surgewave cluster", sessionId);
```

### Text-Based Recall

```csharp
// Search memories by content relevance -- returns ranked results
var results = await memoryContext.RecallAsync("Surgewave performance", maxResults: 3);
```

### Tool Result Caching

```csharp
// Wrap any IAgentTool with transparent caching
var cachedTool = new CachedAgentTool(innerTool, cache, TimeSpan.FromMinutes(5));
var result = await cachedTool.InvokeAsync(args); // Cache miss on first call, hit on repeat
```

## Key Takeaway

**Surgewave.AI provides a complete agent memory system -- facts, preferences, episodes, summarization, and tool caching -- all running locally without external services.**
