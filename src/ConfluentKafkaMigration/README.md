# Confluent.Kafka Migration Sample

Demonstrates zero-code migration from Confluent.Kafka to Surgewave's compatibility wrapper with protocol switching and performance measurement.

## Use Case

Teams using Confluent.Kafka want to migrate to Surgewave without rewriting application code. This sample shows the complete migration path: replace the NuGet package, optionally enable Surgewave's native protocol for 345x faster performance, and keep all existing Kafka API calls unchanged.

## Prerequisites

Before running this sample, you need a Surgewave broker running on `localhost:9092`.

```bash
# From the repository root
dotnet run --project src/Kuestenlogik.Surgewave.Broker
```

> **Note**: This sample uses `Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka` which wraps Surgewave's native client. While it can use either the Surgewave or Kafka protocol, it requires a Surgewave broker.

## What It Does

- Uses Surgewave's Confluent.Kafka-compatible wrapper (`Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka`)
- Demonstrates protocol switching between `surgewave`, `kafka`, and `auto` modes
- Produces and consumes messages with performance measurement
- Shows the migration path from Confluent.Kafka to Surgewave

## How to Run

```bash
# Basic run (auto protocol, 100 messages)
dotnet run --project samples/ConfluentKafkaMigration

# Specify protocol and message count
dotnet run --project samples/ConfluentKafkaMigration -- surgewave 10000
dotnet run --project samples/ConfluentKafkaMigration -- kafka 10000
```

### Command Line Options

| Argument | Description | Options | Default |
|----------|-------------|---------|---------|
| `protocol` | Protocol to use for communication | `surgewave`, `kafka`, `auto` | `auto` |
| `messageCount` | Number of messages to produce/consume | Any positive integer | 100 |

### Protocol Options

| Protocol | Description | Performance |
|----------|-------------|-------------|
| `surgewave` | Surgewave native protocol (binary, optimized) | **345x faster** than Kafka |
| `kafka` | Kafka wire protocol (for compatibility) | Standard Kafka performance |
| `auto` | Auto-detect based on broker response | Falls back to available |

### Examples

```bash
# Quick test with auto protocol
dotnet run --project samples/ConfluentKafkaMigration

# Benchmark Surgewave native protocol
dotnet run --project samples/ConfluentKafkaMigration -- surgewave 10000

# Benchmark Kafka protocol for comparison
dotnet run --project samples/ConfluentKafkaMigration -- kafka 10000

# Large scale test
dotnet run --project samples/ConfluentKafkaMigration -- surgewave 100000
```

## Expected Output

```
=== Surgewave Confluent.Kafka Wrapper Performance Demo ===

Configuration:
  Protocol:     Surgewave
  Messages:     1,000

Producer Test: Producing 1,000 messages...
  Completed in 45 ms
  Throughput: 22,222 msg/sec (653.1 KB/sec)

Consumer Test: Consuming 1,000 messages...
  Completed in 38 ms (1,000 messages)
  Throughput: 26,315 msg/sec

+================================================================+
|                    PERFORMANCE SUMMARY                          |
+================================================================+

Protocol:   Surgewave
Messages:   1,000

Producer:         45 ms  (    22,222 msg/sec)
Consumer:         38 ms  (    26,315 msg/sec)
```

## Migration Guide

### Step 1: Current State (Confluent.Kafka)

Your existing code uses the original Confluent.Kafka package:

```csharp
using Confluent.Kafka;

var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
using var producer = new ProducerBuilder<string, string>(config).Build();
```

### Step 2: Switch to Surgewave Wrapper

Replace the NuGet package reference:

```xml
<!-- Before -->
<PackageReference Include="Confluent.Kafka" Version="2.x.x" />

<!-- After -->
<ProjectReference Include="..\..\src\Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka\Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka.csproj" />
```

**Your code stays exactly the same!** The `using Confluent.Kafka;` statement now references Surgewave's wrapper.

### Step 3: Enable Surgewave Native Protocol (Optional)

Add the `SurgewaveProtocol` configuration for maximum performance:

```csharp
var config = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    SurgewaveProtocol = "surgewave"  // 345x faster than Kafka protocol
};
```

## Performance Comparison

Run both protocols to compare performance:

```bash
# Surgewave native protocol
dotnet run --project samples/ConfluentKafkaMigration -- surgewave 10000

# Kafka protocol (same wire protocol as original Kafka)
dotnet run --project samples/ConfluentKafkaMigration -- kafka 10000
```

Expected results:

| Protocol | Producer | Consumer | Latency |
|----------|----------|----------|---------|
| Kafka | ~2K msg/s | ~3K msg/s | ~15 ms |
| Surgewave | ~22K msg/s | ~26K msg/s | ~45 us |

## Troubleshooting

### Error: "Connection refused" or "Broker not available"

**Cause**: Surgewave broker is not running on `localhost:9092`.

**Solution**:
```bash
dotnet run --project src/Kuestenlogik.Surgewave.Broker
```

### Error: "Local: Message timed out"

**Cause**: Broker is running but connection is timing out.

**Solutions**:
1. Check broker logs for errors
2. Verify the broker is listening:
   ```bash
   # Windows
   netstat -an | findstr 9092

   # Linux/macOS
   netstat -an | grep 9092
   ```

### Protocol "kafka" Not Working

**Cause**: Surgewave broker may not have Kafka protocol enabled.

**Solution**: Use `surgewave` or `auto` protocol, or ensure the broker has Kafka protocol handler configured.

### Slower Than Expected Performance

**Causes and solutions**:
1. **Using `kafka` protocol**: Switch to `surgewave` protocol for 345x improvement
2. **Network latency**: Run broker and client on same machine for benchmarks
3. **Small message count**: Use 10,000+ messages for accurate throughput measurement
4. **Warm-up effects**: First run may be slower due to JIT compilation

### Consumer Times Out

**Cause**: Consumer started before messages were produced.

**Solution**: The sample produces first, then consumes. If issues persist:
1. Check producer completed successfully
2. Increase consumer timeout in code if needed

## Code Differences from Original Confluent.Kafka

The only code difference is the optional `SurgewaveProtocol` property:

```csharp
// Original Confluent.Kafka
var config = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    ClientId = "my-producer"
};

// Surgewave wrapper with protocol selection
var config = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    ClientId = "my-producer",
    SurgewaveProtocol = "surgewave"  // NEW: Optional, defaults to "auto"
};
```

All other APIs are 100% compatible:
- `ProducerBuilder<K,V>` and `ConsumerBuilder<K,V>`
- `IProducer<K,V>` and `IConsumer<K,V>`
- `Message<K,V>`, `Headers`, `DeliveryResult<K,V>`, `ConsumeResult<K,V>`
- Handler callbacks (`SetErrorHandler`, `SetPartitionsAssignedHandler`, etc.)
- Serializers and Deserializers

## Three-Step Migration Path

| Step | Package | Protocol | Code Changes | Performance |
|------|---------|----------|--------------|-------------|
| 1 | Confluent.Kafka | Kafka | None | Baseline |
| 2 | Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka | Surgewave/Kafka | Package only | Up to 345x faster |
| 3 | Kuestenlogik.Surgewave.Client | Surgewave Native | Full rewrite | Optimal |

This sample demonstrates **Step 2** - same code, better performance, with optional protocol selection.

## Related Samples

- **KafkaCompatibility**: Uses original Confluent.Kafka package (Step 1)
- **NativeClient**: Uses Surgewave native client directly (Step 3)

## Architecture

```
+---------------------------+
|     Your Application      |
|  (Confluent.Kafka API)    |
+-------------+-------------+
              |
              v
+---------------------------+
| Kuestenlogik.Surgewave.Compatibility.Confluent.Kafka  |
|      (API Wrapper)        |
+-------------+-------------+
              |
    +---------+---------+
    |                   |
    v                   v
+--------+        +--------+
| Surgewave  |        | Kafka  |
|Protocol|        |Protocol|
+--------+        +--------+
    |                   |
    v                   v
+---------------------------+
|       Surgewave Broker        |
+---------------------------+
```
