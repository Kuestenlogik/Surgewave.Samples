# Connector Pipeline Sample

Demonstrates Surgewave's Connect framework with a complete data pipeline: CSV source -> Surgewave topic -> SQLite sink.

## Use Case

Data integration pipelines need to move data between systems reliably. This sample shows how Surgewave Connect replaces complex ETL tools with simple, low-latency connector configurations -- reading product data from CSV, flowing through a Surgewave topic, and landing in a SQLite database.

## What It Does

- **CSV Source**: Reads product data from a CSV file
- **Surgewave Topic**: Messages flow through Surgewave for processing
- **SQLite Sink**: Writes transformed data to a SQLite database
- **End-to-End Pipeline**: Shows the complete connector lifecycle

## Prerequisites

No external dependencies required - SQLite is embedded.

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Run the pipeline
dotnet run --project samples/ConnectorPipeline
```

The sample will:
1. Read 20 products from `Data/products.csv`
2. Publish them to Surgewave topic `products-raw`
3. Consume and write to `products.db` SQLite database

## Why Surgewave Connect?

### Unified Data Integration

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│   Sources   │     │                 │     │    Sinks    │
├─────────────┤     │                 │     ├─────────────┤
│ CSV Files   │────▶│                 │────▶│ SQLite      │
│ Databases   │────▶│  Surgewave Topics   │────▶│ PostgreSQL  │
│ APIs        │────▶│                 │────▶│ S3/Azure    │
│ Queues      │────▶│  (Partitioned)  │────▶│ Elasticsearch│
│ CDC Streams │────▶│                 │────▶│ Vector DBs  │
└─────────────┘     └─────────────────┘     └─────────────┘
```

### Key Benefits

| Feature | How Surgewave Enables It |
|---------|---------------------|
| **Decoupled Sources/Sinks** | Sources and sinks scale independently |
| **Exactly-Once Delivery** | Transactional offset management |
| **Automatic Recovery** | Connectors resume from last offset |
| **Parallel Processing** | Multiple tasks per connector |
| **Schema Evolution** | Handle format changes gracefully |

### Comparison with Alternatives

| Solution | Connectors | Management | Scaling | Latency |
|----------|------------|------------|---------|---------|
| Apache Kafka Connect | 200+ | Complex | Manual | Medium |
| Debezium | CDC only | Separate | Manual | Medium |
| Fivetran | 150+ | SaaS | Auto | High |
| Airbyte | 300+ | Separate | Manual | High |
| **Surgewave Connect** | **50+** | **Built-in** | **Auto** | **Low** |

### Available Connectors (50+)

| Category | Connectors |
|----------|------------|
| **Databases** | PostgreSQL CDC, MySQL CDC, SQL Server CDC, Oracle CDC, MongoDB, SQLite |
| **Cloud Storage** | S3, Azure Blob, GCS, SFTP |
| **Message Queues** | RabbitMQ, NATS, MQTT, Redis, SQS, Azure Service Bus, GCP Pub/Sub |
| **Search/Analytics** | Elasticsearch, InfluxDB, Snowflake, BigQuery |
| **AI/Vector** | OpenAI, Ollama, Qdrant, pgvector |
| **Files** | CSV, Parquet, Excel, JSON |
| **APIs** | HTTP Webhook, GraphQL, REST |

### Pipeline Architecture

```csharp
// Source Connector Configuration
var sourceConfig = new Dictionary<string, string>
{
    ["connector.class"] = "CsvSourceConnector",
    ["file.path"] = "products.csv",
    ["topic"] = "products-raw",
    ["tasks.max"] = "1"
};

// Sink Connector Configuration
var sinkConfig = new Dictionary<string, string>
{
    ["connector.class"] = "SqliteSinkConnector",
    ["topics"] = "products-raw",
    ["database.path"] = "products.db",
    ["table.name"] = "products"
};
```

### Exactly-Once Semantics

```
┌─────────────────────────────────────────────────────┐
│                   Surgewave Connect                      │
│  ┌─────────────────────────────────────────────┐   │
│  │           Offset Management                  │   │
│  │  • Committed only after sink confirms       │   │
│  │  • Automatic retry on failure               │   │
│  │  • Resume from last committed offset        │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  Source ──▶ Topic ──▶ Sink                         │
│    │                    │                          │
│    └── Offset ◀─────────┘                          │
│        Committed                                    │
└─────────────────────────────────────────────────────┘
```

### Real-World Use Cases

| Use Case | Source | Sink | Benefit |
|----------|--------|------|---------|
| Data Lake Ingestion | PostgreSQL CDC | S3 Parquet | Real-time data lake |
| Search Indexing | MongoDB | Elasticsearch | Sub-second search |
| Analytics Pipeline | HTTP API | Snowflake | Streaming analytics |
| ML Feature Store | Kafka | pgvector | Real-time features |
| Audit Logging | Any | SQLite/S3 | Compliance records |

### Operational Benefits

| Aspect | Traditional ETL | Surgewave Connect |
|--------|-----------------|---------------|
| Latency | Minutes-Hours | **Seconds** |
| Recovery | Manual restart | **Automatic** |
| Monitoring | Separate tools | **Built-in** |
| Scaling | Rewrite jobs | **Add tasks** |

### CLI Management

```bash
# List connectors
surgewave connect list

# Create connector
surgewave connect create --config connector.json

# Check status
surgewave connect status my-connector

# Pause/Resume
surgewave connect pause my-connector
surgewave connect resume my-connector
```

## Key Takeaway

**Surgewave Connect provides a unified, low-latency data integration platform with 50+ connectors, replacing complex ETL pipelines with simple configuration.**
