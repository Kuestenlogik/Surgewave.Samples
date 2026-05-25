# Surgewave Samples

This directory contains sample applications demonstrating Surgewave's capabilities as a high-performance, Kafka-compatible message broker.

## Quick Start

```bash
# 1. Start the Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# 2. Run any sample
dotnet run --project samples/<SampleName>
```

## Sample Overview

| Sample | Description | Key Feature |
|--------|-------------|-------------|
| [KafkaCompatibility](KafkaCompatibility/) | Drop-in Kafka replacement | Zero migration effort |
| [ConfluentKafkaMigration](ConfluentKafkaMigration/) | Migration wrapper with protocol switching | **345x faster** with same API |
| [NativeClient](NativeClient/) | Ultra-low latency protocol | 45µs latency, 1.25M msg/s |
| [MultiProtocol](MultiProtocol/) | Kafka + Native + gRPC | Protocol interoperability |
| [EventSourcing](EventSourcing/) | Bank account event store | Time-travel queries |
| [ConnectorPipeline](ConnectorPipeline/) | CSV → Surgewave → SQLite | 50+ built-in connectors |
| [IotDashboard](IotDashboard/) | Real-time sensor dashboard | Stream processing |
| [FleetTracker](FleetTracker/) | GPS vehicle tracking | Per-client time-travel |
| [SurgewaveChat](SurgewaveChat/) | Multi-room chat system | Pub/sub broadcast |
| [Agents](Agents/) | Durable AI agent workers | Checkpoint persistence |
| [RagPipeline](RagPipeline/) | Vector search RAG | OpenAI + Qdrant |

## Samples by Category

### Getting Started
Start here if you're new to Surgewave.

| Sample | What You'll Learn |
|--------|-------------------|
| **[KafkaCompatibility](KafkaCompatibility/)** | Use existing Kafka clients with Surgewave |
| **[ConfluentKafkaMigration](ConfluentKafkaMigration/)** | Migrate with zero code changes + 345x performance |
| **[NativeClient](NativeClient/)** | Surgewave's high-performance native protocol |

### Messaging Patterns

| Sample | Pattern | Use Case |
|--------|---------|----------|
| **[SurgewaveChat](SurgewaveChat/)** | Pub/Sub Broadcast | Real-time notifications |
| **[MultiProtocol](MultiProtocol/)** | Protocol Bridge | Polyglot microservices |
| **[IotDashboard](IotDashboard/)** | Fan-out | Device telemetry |

### Data Integration

| Sample | Source | Sink | Pipeline |
|--------|--------|------|----------|
| **[ConnectorPipeline](ConnectorPipeline/)** | CSV | SQLite | ETL replacement |
| **[RagPipeline](RagPipeline/)** | Documents | Qdrant | AI/ML embeddings |

### Event-Driven Architecture

| Sample | Pattern | Capability |
|--------|---------|------------|
| **[EventSourcing](EventSourcing/)** | Event Store | Replay, time-travel |
| **[FleetTracker](FleetTracker/)** | CQRS + Replay | Per-client state |
| **[Agents](Agents/)** | Task Queue | Durable workflows |

## Performance Comparison

| Protocol | Latency | Throughput | Best For |
|----------|---------|------------|----------|
| Kafka | 15ms | 68K msg/s | Compatibility |
| Native | **45µs** | **1.25M msg/s** | Performance |
| gRPC | 1ms | 500K msg/s | Polyglot |

## Prerequisites by Sample

| Sample | Surgewave Broker | Docker | External Services |
|--------|:------------:|:------:|-------------------|
| KafkaCompatibility | ✓ | | |
| ConfluentKafkaMigration | ✓ | | |
| NativeClient | ✓ | | |
| MultiProtocol | ✓ | | |
| EventSourcing | ✓ | | |
| ConnectorPipeline | ✓ | | |
| IotDashboard | ✓ | | |
| FleetTracker | ✓ | | |
| SurgewaveChat | ✓ | | |
| Agents | ✓ | | |
| RagPipeline | ✓ | ✓ | Qdrant, OpenAI API |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Your Applications                         │
│                                                                  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│  │  Kafka   │ │  Native  │ │   gRPC   │ │   REST   │           │
│  │ Clients  │ │ Clients  │ │ Clients  │ │ Clients  │           │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘           │
└───────┼────────────┼────────────┼────────────┼──────────────────┘
        │            │            │            │
        ▼            ▼            ▼            ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Surgewave Broker                              │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Protocol Handlers                     │   │
│  │  Kafka (9092)  │  Native (9092)  │  gRPC (5000)         │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              │                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Unified Storage                       │   │
│  │  Topics │ Partitions │ Consumer Groups │ Transactions   │   │
│  └─────────────────────────────────────────────────────────┘   │
│                              │                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                   Tiered Storage                         │   │
│  │  Memory (Hot) │ SSD (Warm) │ S3/Azure/GCS (Cold)        │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Why Surgewave?

| Feature | Apache Kafka | Surgewave |
|---------|--------------|-------|
| Latency | 15ms | **45µs** (333x faster) |
| Throughput | 68K msg/s | **1.25M msg/s** (18x faster) |
| Setup | JVM + ZooKeeper | **Single binary** |
| Protocols | Kafka only | **Kafka + Native + gRPC** |
| .NET Support | Third-party | **First-class** |

## Running Multiple Samples

You can run multiple samples simultaneously against the same Surgewave broker:

```bash
# Terminal 1: Start broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Terminal 2: Chat user 1
dotnet run --project samples/SurgewaveChat

# Terminal 3: Chat user 2
dotnet run --project samples/SurgewaveChat

# Terminal 4: IoT generator
dotnet run --project samples/IotDashboard/Generator

# Terminal 5: IoT dashboard
dotnet run --project samples/IotDashboard/Dashboard
```

## Building All Samples

```bash
# Build entire solution including all samples
dotnet build Kuestenlogik.Surgewave.slnx

# Run tests
dotnet test Kuestenlogik.Surgewave.slnx
```

## Learn More

- [Surgewave Documentation](../docs/)
- [Roadmap](../Roadmap.md)
- [GitHub Repository](https://github.com/Kuestenlogik/Surgewave)
