# Surgewave Samples

28 ready-to-run sample applications demonstrating Surgewave's capabilities -- from basic produce/consume to real-time dashboards, AI pipelines, and distributed patterns.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- A running [Surgewave Broker](https://github.com/Kuestenlogik/Surgewave) (most samples use an embedded broker)
- Surgewave NuGet packages (resolved automatically from nuget.org / GitHub Packages via the bundled `nuget.config`)

## Building & Running

```bash
# Build all samples
dotnet build Kuestenlogik.Surgewave.Samples.slnx

# Run a specific sample
dotnet run --project src/NativeClient

# Run with arguments (where supported)
dotnet run --project src/ConfluentKafkaMigration -- surgewave 10000
```

## NuGet Feed Setup

The bundled `nuget.config` already points at `nuget.org` for the stable Surgewave
release and at the [Kuestenlogik GitHub Packages feed](https://github.com/orgs/Kuestenlogik/packages)
for pre-release builds. No further configuration is required for the typical
build-and-run workflow.

---

## Sample Catalog

### Core

Fundamental Surgewave client usage and protocol demonstrations.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [NativeClient](src/NativeClient) | Produce and consume messages using Surgewave's native protocol | `SurgewaveClient`, native protocol, `Spectre.Console` |
| [SurgewaveChat](src/SurgewaveChat) | Real-time multi-room CLI chat application | Pub/sub, per-user consumer groups, broadcast delivery |
| [KafkaCompatibility](src/KafkaCompatibility) | Run original Confluent.Kafka code against Surgewave's Kafka endpoint | `Confluent.Kafka`, protocol compatibility, performance measurement |
| [ConfluentKafkaMigration](src/ConfluentKafkaMigration) | Zero-code migration from Confluent.Kafka to Surgewave | `Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka`, protocol switching, benchmarking |
| [MultiProtocol](src/MultiProtocol) | Access the same data via Kafka, native, and gRPC protocols | `Confluent.Kafka`, `Kuestenlogik.Surgewave.Client`, `Kuestenlogik.Surgewave.Api.Grpc.Client` |

### Streams

Real-time stream processing with Surgewave Streams.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [ECommerceAnalytics](src/ECommerceAnalytics) | Revenue dashboard with product catalog join and top-seller ranking | `Kuestenlogik.Surgewave.Streams`, embedded broker, windowed aggregations |

### Consumer

Consumer group patterns and parallel processing.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [LogAggregation](src/LogAggregation) | Parallel log processing with consumer group rebalancing | Consumer groups, partition rebalancing, embedded broker |

### Schema

Schema evolution and registry integration.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [IotSchemaEvolution](src/IotSchemaEvolution) | IoT sensor telemetry with firmware-versioned schema evolution | `Kuestenlogik.Surgewave.Client.SchemaRegistry`, JSON Schema, backward/forward compatibility |

### Cluster

Multi-broker clustering and failover.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [PaymentGateway](src/PaymentGateway) | 3-broker cluster with crash failover during payment processing | `Kuestenlogik.Surgewave.Testing.Chaos`, leader election, zero data loss |

### Security

Access control and multi-team data governance.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [DataPlatform](src/DataPlatform) | Multi-team ACL enforcement across shared topics | ACL rules, per-team access control, permission management |

### Connectors

Data integration with Surgewave Connect.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [ConnectorPipeline](src/ConnectorPipeline) | CSV source -> Surgewave topic -> SQLite sink pipeline | `Kuestenlogik.Surgewave.Connect`, `Kuestenlogik.Surgewave.Connector.Csv`, `Kuestenlogik.Surgewave.Connector.Database` |

### AI

AI and LLM integration using Surgewave.AI libraries.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [Agents](src/Agents) | Multi-agent interactive demo with crash recovery | `Kuestenlogik.Surgewave.AI.Agents`, agent hosting runtime, OpenAI/Ollama support |
| [RagPipeline](src/RagPipeline) | Documents -> embeddings -> Qdrant vector DB -> semantic search | `OpenAI`, `Qdrant.Client`, embeddings, retrieval-augmented generation |
| [RagWithSurgewaveAI](src/RagWithSurgewaveAI) | Full RAG workflow using Surgewave.AI libraries (no API keys needed) | `Kuestenlogik.Surgewave.AI.Rag`, `Kuestenlogik.Surgewave.AI.Retrieval`, `Kuestenlogik.Surgewave.AI.Documents`, local processing |
| [GuardrailsDemo](src/GuardrailsDemo) | PII detection, toxicity filtering, and prompt injection detection | `Kuestenlogik.Surgewave.AI.Guardrails`, PiiDetector, ToxicityFilter, PromptInjectionDetector |
| [PipelineChat](src/PipelineChat) | Interactive chat with Surgewave AI pipelines via the Pipeline Chat API | `Kuestenlogik.Surgewave.Connect`, SSE streaming, HTTP API |
| [AgentMemoryDemo](src/AgentMemoryDemo) | Persistent agent memory, recall, summarization, and tool caching | `Kuestenlogik.Surgewave.AI.Agents`, InMemoryAgentMemoryStore, CachedAgentTool |
| [MultimodalDemo](src/MultimodalDemo) | Multimodal content processing with document pipeline | `Kuestenlogik.Surgewave.AI.Multimodal`, `Kuestenlogik.Surgewave.AI.Documents`, content modalities |

### Dashboards

Real-time Blazor dashboards with MudBlazor.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [IotDashboard](src/IotDashboard) | IoT sensor monitoring with live charts (Dashboard + Generator) | `MudBlazor`, `Kuestenlogik.Surgewave.Streams`, Blazor Server, real-time UI |
| [FleetTracker](src/FleetTracker) | GPS vehicle tracking for 20 vehicles around Berlin (Dashboard + Generator) | `MudBlazor`, `Kuestenlogik.Surgewave.Client`, 1Hz position updates |
| [MassFleetTracker](src/MassFleetTracker) | Large-scale fleet tracking for 100,000+ vehicles (Dashboard + Generator) | `MudBlazor`, `Kuestenlogik.Surgewave.Client`, high-throughput partitioned streaming |
| [DigitalTwin](src/DigitalTwin) | Equipment digital twin with telemetry simulation (Dashboard + Generator) | `MudBlazor`, `Kuestenlogik.Surgewave.Client`, equipment simulators |

### Patterns

Distributed system patterns implemented with Surgewave.

| Sample | Description | Key Features |
|--------|-------------|--------------|
| [EventSourcing](src/EventSourcing) | Bank account demo with event replay and projections | `Kuestenlogik.Surgewave.Client`, event store, append-only topics, replay |
| [OrderSaga](src/OrderSaga) | Distributed transaction with compensation across 5 services | Saga pattern, rollback on failure, embedded broker |
| [FraudDetection](src/FraudDetection) | Real-time credit card fraud detection with 4 rule engines | `Kuestenlogik.Surgewave.Streams`, velocity checks, geo-velocity, anomaly detection |
| [NotificationHub](src/NotificationHub) | Multi-channel fan-out (email, SMS, push) with rate limiting | Fan-out pattern, priority routing, per-user rate limiting |
| [SupplyChainTracker](src/SupplyChainTracker) | Order tracking state machine from factory to customer | Compacted topics, state machines, failure paths, ETA calculation |
| [SmartFactory](src/SmartFactory) | Predictive maintenance with anomaly detection and AI guardrails | `Kuestenlogik.Surgewave.Streams`, `Kuestenlogik.Surgewave.AI.Guardrails`, tumbling windows |

---

## Multi-Project Samples

Some samples consist of multiple projects (Dashboard + Generator + Shared):

- **IotDashboard**: `Dashboard` (Blazor web app), `Generator` (data simulator), `Shared` (models)
- **FleetTracker**: `Dashboard`, `Generator`, `Shared`
- **MassFleetTracker**: `MassFleetTracker.Dashboard`, `MassFleetTracker.Generator`, `MassFleetTracker.Shared`
- **DigitalTwin**: `Dashboard`, `Generator`, `Shared`

To run these, start the Generator and Dashboard in separate terminals:

```bash
# Terminal 1: Start the data generator
dotnet run --project src/FleetTracker/Generator

# Terminal 2: Start the dashboard
dotnet run --project src/FleetTracker/Dashboard
```

## Related Repositories

- [Surgewave](https://github.com/kuestenlogik/Surgewave) -- Core broker, client, protocols, storage
- [Surgewave.Connectors](https://github.com/kuestenlogik/Surgewave.Connectors) -- 113 connector plugins
- [Surgewave.AI](https://github.com/kuestenlogik/Surgewave.Ai) -- AI/ML pipeline libraries
- [Surgewave.Bootcamp](https://github.com/kuestenlogik/Surgewave.Bootcamp) -- Interactive learning curriculum
- [Surgewave.Templates](https://github.com/kuestenlogik/Surgewave.Templates) -- dotnet-new templates, Helm charts
