# Multi-Protocol Sample

Stock quote demonstration showing the same data produced and consumed over Kafka, Native, and gRPC protocols.

## Use Case

Organizations with polyglot tech stacks need a messaging broker that supports multiple protocols. This sample demonstrates that Surgewave supports Kafka, Native, and gRPC protocols simultaneously -- all reading and writing the same topics. Teams can choose the optimal protocol per use case without creating data silos.

## What It Does

- **Kafka Protocol**: Standard Confluent.Kafka client
- **Native Protocol**: Surgewave's optimized binary protocol
- **gRPC Protocol**: Language-agnostic Protocol Buffers
- **Protocol Comparison**: Benchmark roundtrip times
- **Unified CLI**: Interactive menu for testing

## How to Run

```bash
# Start Surgewave broker (handles all protocols)
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Run the sample
dotnet run --project samples/MultiProtocol
```

## Why Multi-Protocol Support?

### Protocol Options

| Protocol | Best For | Latency | Compatibility |
|----------|----------|---------|---------------|
| **Kafka** | Existing apps, ecosystem | 15ms | All Kafka tools |
| **Native** | New .NET apps, performance | **45µs** | Surgewave only |
| **gRPC** | Polyglot, microservices | 1ms | Any language |

### Architecture

```
┌─────────────────────────────────────────────────────┐
│                  Your Applications                   │
│                                                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│  │ Java/Python │  │   .NET      │  │   Go/Rust   │ │
│  │   (Kafka)   │  │  (Native)   │  │   (gRPC)    │ │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘ │
└─────────┼────────────────┼────────────────┼─────────┘
          │                │                │
          │  Port 9092     │  Port 9092     │  Port 5000
          ▼                ▼                ▼
┌─────────────────────────────────────────────────────┐
│                    Surgewave Broker                      │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐ │
│  │   Kafka     │  │   Native    │  │    gRPC     │ │
│  │  Protocol   │  │  Protocol   │  │   Service   │ │
│  │  Handler    │  │  Handler    │  │  (11 svcs)  │ │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘ │
│         │                │                │        │
│         └────────────────┼────────────────┘        │
│                          ▼                         │
│              ┌───────────────────┐                 │
│              │   Unified Storage │                 │
│              │  (Same Topics!)   │                 │
│              └───────────────────┘                 │
└─────────────────────────────────────────────────────┘
```

### Key Benefits

| Feature | Benefit |
|---------|---------|
| **Unified Storage** | All protocols read/write same topics |
| **Mix & Match** | Kafka producer → gRPC consumer works |
| **Gradual Migration** | Move apps to faster protocol incrementally |
| **Best Tool for Job** | Use optimal protocol per use case |

### Protocol Comparison

| Aspect | Kafka | Native | gRPC |
|--------|-------|--------|------|
| Latency | 15ms | **45µs** | 1ms |
| Throughput | 68K/s | **1.25M/s** | 500K/s |
| Languages | All | .NET | All |
| Streaming | Poll | Push | Bidirectional |
| Schema | Wire format | Binary | Protobuf |

### Use Case Matrix

| Scenario | Recommended | Why |
|----------|-------------|-----|
| Existing Kafka app | Kafka | Zero changes needed |
| New .NET microservice | Native | Maximum performance |
| Python ML pipeline | Kafka | Standard client |
| Go high-frequency trading | gRPC | Low latency + Go support |
| Browser real-time | gRPC/REST | HTTP-based |
| Mobile app | gRPC | Efficient + typed |

### Interoperability Example

```csharp
// Producer using Kafka protocol (Java team)
await kafkaProducer.ProduceAsync("quotes", new Message {
    Key = "AAPL",
    Value = quoteJson
});

// Consumer using Native protocol (.NET team)
var result = await nativeConsumer.ConsumeAsync();
// Same message received!

// Analytics using gRPC (Python team)
for response in grpc_client.Consume(topic="quotes"):
    process(response.records)
# Same message again!
```

### gRPC Services (11 Available)

| Service | Operations |
|---------|------------|
| ProducerService | Send, SendBatch |
| ConsumerService | Consume, Subscribe, Commit |
| TopicService | Create, Delete, List, Describe |
| ClusterService | Metadata, BrokerInfo |
| ConsumerGroupService | Join, Leave, List, Describe |
| TransactionService | Init, Commit, Abort |
| ... and more | |

### Migration Path

```
Phase 1: Connect existing Kafka apps
         ┌─────────────┐
         │ Kafka Apps  │ ──▶ Surgewave (Kafka Protocol)
         └─────────────┘

Phase 2: New apps use Native
         ┌─────────────┐
         │ Kafka Apps  │ ──▶ Surgewave
         └─────────────┘      ▲
         ┌─────────────┐      │
         │ New .NET    │ ─────┘ (Native Protocol)
         └─────────────┘

Phase 3: Cross-language via gRPC
         ┌─────────────┐
         │ Python/Go   │ ──▶ Surgewave (gRPC)
         └─────────────┘
```

### Performance Test (This Sample)

```
Protocol Comparison (100 messages roundtrip):

┌──────────┬──────────┬──────────┬─────────────┐
│ Protocol │ Produced │ Consumed │ Time        │
├──────────┼──────────┼──────────┼─────────────┤
│ Native   │ 100      │ 100      │ 85 ms    ★  │
│ gRPC     │ 100      │ 100      │ 245 ms      │
│ Kafka    │ 100      │ 100      │ 1,250 ms    │
└──────────┴──────────┴──────────┴─────────────┘

★ Native was the fastest protocol!
```

## Key Takeaway

**Surgewave's multi-protocol support lets teams choose the best protocol for their needs while sharing the same data - no protocol silos.**
