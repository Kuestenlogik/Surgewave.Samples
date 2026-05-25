# Kafka Compatibility Sample

Demonstrates Surgewave's wire-level compatibility with Apache Kafka using the standard Confluent.Kafka .NET client.

## Use Case

Teams with existing Kafka applications want to switch to Surgewave without rewriting code. This sample proves that existing Confluent.Kafka client code works unchanged against a Surgewave broker -- zero migration cost with massive performance improvements.

## Prerequisites

Before running this sample, you need a Kafka-compatible broker running on `localhost:9092`.

### Option 1: Surgewave Broker (Recommended)

```bash
# From the repository root
dotnet run --project src/Kuestenlogik.Surgewave.Broker
```

### Option 2: Apache Kafka

```bash
# Using Docker
docker run -d --name kafka \
  -p 9092:9092 \
  -e KAFKA_CFG_NODE_ID=0 \
  -e KAFKA_CFG_PROCESS_ROLES=controller,broker \
  -e KAFKA_CFG_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093 \
  -e KAFKA_CFG_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT \
  -e KAFKA_CFG_CONTROLLER_QUORUM_VOTERS=0@localhost:9093 \
  -e KAFKA_CFG_CONTROLLER_LISTENER_NAMES=CONTROLLER \
  bitnami/kafka:latest
```

### Option 3: Redpanda

```bash
docker run -d --name redpanda \
  -p 9092:9092 \
  docker.redpanda.com/redpandadata/redpanda:latest \
  redpanda start --smp 1 --memory 512M --overprovisioned
```

## What It Does

- Connects to a broker using the standard Confluent.Kafka library
- Produces messages to a topic with performance measurement
- Consumes messages with consumer groups
- Shows that existing Kafka applications work without code changes

## How to Run

```bash
# Basic run (100 messages)
dotnet run --project samples/KafkaCompatibility

# Performance test with more messages
dotnet run --project samples/KafkaCompatibility -- 10000
```

### Command Line Options

| Argument | Description | Default |
|----------|-------------|---------|
| `messageCount` | Number of messages to produce/consume | 100 |

### Examples

```bash
# Quick test
dotnet run --project samples/KafkaCompatibility

# Benchmark with 10,000 messages
dotnet run --project samples/KafkaCompatibility -- 10000

# Large scale test
dotnet run --project samples/KafkaCompatibility -- 100000
```

## Expected Output

```
=== Original Confluent.Kafka Performance Demo ===

Configuration:
  Package:      Confluent.Kafka (original)
  Protocol:     Kafka
  Messages:     1,000

Producer Test: Producing 1,000 messages...
  Completed in 523 ms
  Throughput: 1,912 msg/sec (56.2 KB/sec)

Consumer Test: Consuming 1,000 messages...
  Completed in 312 ms (1,000 messages)
  Throughput: 3,205 msg/sec

╔════════════════════════════════════════════════════════════════╗
║                    PERFORMANCE SUMMARY                          ║
╚════════════════════════════════════════════════════════════════╝

Package:    Confluent.Kafka (original)
Protocol:   Kafka
Messages:   1,000

Producer:        523 ms  (     1,912 msg/sec)
Consumer:        312 ms  (     3,205 msg/sec)
```

## Troubleshooting

### Error: "Connection refused" or "Broker not available"

**Cause**: No broker is running on `localhost:9092`.

**Solution**: Start a broker using one of the options above:
```bash
# Surgewave
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Or check if Docker container is running
docker ps | grep -E "kafka|redpanda"
```

### Error: "Local: Message timed out"

**Cause**: The broker is running but the connection is timing out.

**Solutions**:
1. Check broker logs for errors
2. Verify the broker is listening on port 9092:
   ```bash
   # Windows
   netstat -an | findstr 9092

   # Linux/macOS
   netstat -an | grep 9092
   ```
3. If using Docker, ensure the port mapping is correct

### Error: "Unknown topic or partition"

**Cause**: Topic auto-creation may be disabled on the broker.

**Solution**: The sample creates topics automatically. If using Kafka, ensure `auto.create.topics.enable=true` or create topics manually:
```bash
kafka-topics.sh --create --topic test --partitions 1 --bootstrap-server localhost:9092
```

### Slow Performance

**Causes and solutions**:
1. **Broker on different machine**: Network latency adds overhead. Use localhost for benchmarks.
2. **Disk I/O bottleneck**: Use SSD storage for the broker data directory.
3. **Insufficient resources**: Ensure adequate CPU and memory for the broker.

### Consumer Not Receiving Messages

**Causes**:
1. **Consumer started before producer**: The sample handles this with `AutoOffsetReset.Earliest`
2. **Consumer group already committed**: Each run uses a unique group ID to avoid this
3. **Topic has no messages**: Check producer completed successfully

## Why Surgewave?

### Zero Migration Cost

| Benefit | Description |
|---------|-------------|
| **Drop-in Replacement** | Existing Kafka applications connect to Surgewave without any code changes |
| **Same Client Libraries** | Use Confluent.Kafka, librdkafka, or any Kafka client |
| **No Retraining** | Teams familiar with Kafka are immediately productive |

### Operational Simplicity

| Kafka | Surgewave |
|-------|-------|
| Requires ZooKeeper (or KRaft migration) | Single binary, no dependencies |
| Complex multi-node setup | Start with `dotnet run` |
| JVM tuning required | .NET runtime, minimal configuration |
| Separate Schema Registry deployment | Built-in Schema Registry |

### Performance Gains

| Metric | Kafka | Surgewave | Improvement |
|--------|-------|-------|-------------|
| Producer Throughput | 68K msg/s | 1.25M msg/s | **+1,732%** |
| Consumer Throughput | 138K msg/s | 1.28M msg/s | **+826%** |
| P50 Latency | 15.6 ms | 45 us | **345x faster** |

### When to Use Kafka Protocol

- Migrating existing Kafka applications to Surgewave
- Using Kafka client libraries in any language (Java, Python, Go, etc.)
- Integrating with tools that expect Kafka (Debezium, Kafka Connect ecosystem)
- Gradual migration where some apps still target Kafka

## Next Steps

After running this sample, try the **ConfluentKafkaMigration** sample to see how to switch to the Surgewave wrapper with protocol selection:

```bash
# Compare performance with Surgewave native protocol
dotnet run --project samples/ConfluentKafkaMigration -- surgewave 10000
dotnet run --project samples/ConfluentKafkaMigration -- kafka 10000
```

## Architecture

```
+---------------------+     +---------------------+
|   Your Application  |     |   Your Application  |
|  (Confluent.Kafka)  |     |  (Confluent.Kafka)  |
+----------+----------+     +----------+----------+
           | Kafka Protocol            | Kafka Protocol
           v                           v
+-------------------------------------------------+
|                    Surgewave Broker                  |
|  +-------------+  +-------------+  +---------+  |
|  |Kafka Protocol|  |Native Proto |  |  gRPC   |  |
|  |   Handler   |  |   Handler   |  | Handler |  |
|  +-------------+  +-------------+  +---------+  |
|                         |                       |
|              +----------v----------+            |
|              |   Storage Engine    |            |
|              |  (Arrow Columnar)   |            |
|              +---------------------+            |
+-------------------------------------------------+
```

## Key Takeaway

**Surgewave lets you keep your existing Kafka code while gaining massive performance improvements and operational simplicity.**
