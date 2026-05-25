# Pipeline Chat Demo

Interactive CLI client for Surgewave's Pipeline Chat REST API, demonstrating synchronous chat, SSE streaming, async fire-and-forget messaging, and session management.

## Use Case

AI pipelines need a conversational interface. This sample shows how to interact with Surgewave AI pipelines via the REST Chat API -- sending messages, receiving streamed responses, managing chat sessions, and viewing conversation history. It demonstrates the client side of Surgewave's Pipeline Chat feature.

## How to Run

```bash
# 1. Start Surgewave broker with Connect enabled
dotnet run --project src/Kuestenlogik.Surgewave.Broker -- --Surgewave:Connect:Enabled=true

# 2. Run the chat client
dotnet run --project src/PipelineChat
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `Surgewave_BROKER_URL` | `http://localhost:5000` | Surgewave broker HTTP address |
| `Surgewave_PIPELINE_ID` | `echo-pipeline` | Pipeline to chat with |

## Architecture

```
  CLI Client (this sample)
        |
        | HTTP REST / SSE
        v
+-------------------+
| Surgewave Broker      |
| Pipeline Chat API |
|                   |
| POST /chat        | <-- Synchronous request/response
| POST /chat/stream | <-- SSE streaming tokens
| POST /chat/async  | <-- Fire-and-forget
| GET  /sessions    | <-- List active sessions
| GET  /history     | <-- Session message history
| DELETE /session   | <-- Clean up session
+-------------------+
        |
        v
+-------------------+
| AI Pipeline       |
| (configurable)    |
| - Echo pipeline   |
| - LLM pipeline    |
| - RAG pipeline    |
+-------------------+
```

## What to Expect

1. Connection test to Surgewave broker and pipeline topic discovery
2. Interactive prompt for sending messages to the pipeline
3. Synchronous mode: full response returned at once
4. Streaming mode (`/stream`): tokens arrive via Server-Sent Events
5. Async mode (`/async`): fire-and-forget with later history retrieval
6. Session management: list, switch, create, delete sessions

## Prerequisites

- .NET 10 SDK
- Surgewave broker running with Connect enabled

## Commands

| Command | Description |
|---------|-------------|
| `/stream` | Toggle streaming mode (SSE) |
| `/async <msg>` | Send fire-and-forget message |
| `/sessions` | List active chat sessions |
| `/history` | Show current session history |
| `/session` | Show current session ID |
| `/new` | Start a new session |
| `/delete` | Delete current session |
| `/pipeline <id>` | Switch to a different pipeline |
| `/help` | Show help |
| `/quit` | Exit |

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Pipeline Chat API | REST endpoints for sync, streaming, and async chat | Standard HTTP interface for any client language |
| SSE Streaming | `POST /chat/stream` returns `data:` events with tokens | Real-time token-by-token display like ChatGPT |
| Session Management | Session IDs track conversation state across requests | Multi-turn conversations with persistent context |
| Async Messaging | `POST /chat/async` queues message, returns immediately | Non-blocking for long-running pipeline operations |
| Topic Discovery | `GET /chat/topics` returns signal and response topics | Clients can inspect pipeline topology |
| Pipeline Switching | `/pipeline <id>` changes target pipeline at runtime | Single client can interact with multiple AI pipelines |

## Key Code Highlights

### Synchronous Chat Request

```csharp
var request = new ChatRequest { Message = message, SessionId = sessionId };
var response = await httpClient.PostAsJsonAsync(
    $"/api/pipelines/{pipelineId}/chat", request, jsonOptions);
var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(jsonOptions);
```

### SSE Streaming

```csharp
using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
using var stream = await response.Content.ReadAsStreamAsync();
using var reader = new StreamReader(stream);

while (!reader.EndOfStream)
{
    var line = await reader.ReadLineAsync();
    if (line.StartsWith("data: "))
    {
        var evt = JsonSerializer.Deserialize<ChatStreamEventDto>(line[6..]);
        if (evt?.Type == "token") Console.Write(evt.Content);
    }
}
```

### Session History

```csharp
var response = await httpClient.GetAsync(
    $"/api/pipelines/{pipelineId}/chat/sessions/{sessionId}/history");
var history = await response.Content.ReadFromJsonAsync<ChatHistoryResponse>(jsonOptions);
```

## Key Takeaway

**Surgewave's Pipeline Chat API provides a standard REST/SSE interface for conversational AI -- supporting sync, streaming, and async patterns with built-in session management.**
