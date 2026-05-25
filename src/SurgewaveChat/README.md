# Surgewave Chat Sample

Real-time CLI chat application with multiple rooms, demonstrating pub/sub messaging patterns.

## Use Case

Real-time communication systems (chat rooms, notifications, collaborative editing) need instant message delivery with message history. Surgewave topics provide natural chat channels -- each room is a topic, and unique consumer group IDs per user enable broadcast delivery without custom fan-out logic.

## What It Does

- **Multi-Room Chat**: Join, leave, and switch between chat rooms
- **Real-Time Messaging**: Instant message delivery across all participants
- **System Notifications**: Join/leave announcements
- **Commands**: `/join`, `/leave`, `/switch`, `/rooms`, `/help`, `/quit`

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Terminal 1: Start first chat client
dotnet run --project samples/SurgewaveChat
# Enter username: alice
# /join general

# Terminal 2: Start second chat client
dotnet run --project samples/SurgewaveChat
# Enter username: bob
# /join general
# Type a message - alice sees it instantly!
```

## Why Surgewave for Real-Time Chat?

### Ultra-Low Latency

| Metric | Typical WebSocket | Surgewave Native |
|--------|-------------------|--------------|
| Message Latency | 5-20 ms | **< 1 ms** |
| Throughput | 10K msg/s | **1M+ msg/s** |
| Connection Overhead | Per-client state | Stateless consumers |

### Scalable Pub/Sub Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Alice     │     │    Bob      │     │   Charlie   │
│  (Client)   │     │  (Client)   │     │  (Client)   │
└──────┬──────┘     └──────┬──────┘     └──────┬──────┘
       │                   │                   │
       │    Publish        │    Subscribe      │
       ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────┐
│                    Surgewave Broker                      │
│  ┌──────────────────────────────────────────────┐  │
│  │              Topic: chat-general              │  │
│  │  [msg1] [msg2] [msg3] [msg4] [msg5] ...      │  │
│  └──────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────┐  │
│  │              Topic: chat-random               │  │
│  │  [msg1] [msg2] ...                           │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Key Benefits

| Feature | How Surgewave Enables It |
|---------|---------------------|
| **Instant Delivery** | Sub-millisecond pub/sub |
| **Room Isolation** | Each room is a separate topic |
| **Message History** | New joiners can see recent messages |
| **Horizontal Scale** | Add brokers for more capacity |
| **Persistence** | Messages survive broker restarts |

### Comparison with Alternatives

| Solution | Latency | Scale | History | Complexity |
|----------|---------|-------|---------|------------|
| WebSocket Server | Good | Limited | None | Medium |
| Redis Pub/Sub | Good | Medium | None | Low |
| Firebase | Medium | Good | Yes | Low |
| **Surgewave** | **Excellent** | **Massive** | **Yes** | **Low** |

### Broadcast Pattern

Surgewave's consumer group model enables true broadcast:

```csharp
// Each user gets their own consumer group = broadcast to all
options.GroupId = $"chat-{username}-{Guid.NewGuid()}";
```

- **Unique Group ID**: Every client receives every message
- **No Fan-Out Logic**: Surgewave handles distribution
- **Automatic Cleanup**: Disconnected clients don't block

### Advanced Features Possible

| Feature | Surgewave Capability |
|---------|-----------------|
| Message Search | Consume from beginning, filter |
| Read Receipts | Separate topic for receipts |
| Typing Indicators | Ephemeral topic with short retention |
| User Presence | Compacted topic for online status |
| Message Reactions | Events referencing original offset |

### Production Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Load Balancer                      │
└─────────────────────┬───────────────────────────────┘
                      │
    ┌─────────────────┼─────────────────┐
    ▼                 ▼                 ▼
┌─────────┐     ┌─────────┐     ┌─────────┐
│ Gateway │     │ Gateway │     │ Gateway │
│   (WS)  │     │   (WS)  │     │   (WS)  │
└────┬────┘     └────┬────┘     └────┬────┘
     │               │               │
     └───────────────┼───────────────┘
                     ▼
┌─────────────────────────────────────────────────────┐
│              Surgewave Cluster (3 brokers)              │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐            │
│  │Broker 1 │  │Broker 2 │  │Broker 3 │            │
│  └─────────┘  └─────────┘  └─────────┘            │
└─────────────────────────────────────────────────────┘
```

## Key Takeaway

**Surgewave's pub/sub model provides instant message delivery with built-in history, scaling effortlessly from chat rooms to enterprise messaging platforms.**
