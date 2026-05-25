# MassFleetTracker - 100k Vehicle Simulation

High-throughput stress test for Surgewave with 100,000 simulated vehicles.

## Use Case

Fleet management systems tracking tens of thousands of vehicles need extreme throughput. This sample stress-tests Surgewave's ability to ingest 100,000 messages per second, partition across 100 partitions, and visualize results in real-time with adaptive zoom levels (heatmap, clusters, individual markers).

## Overview

This sample demonstrates Surgewave's ability to handle massive message volumes with:
- **100,000 vehicles** sending position updates
- **100,000 msg/s** sustained throughput
- **100 partitions** for parallel processing
- **Real-time visualization** with heatmap, clusters, and individual markers

## Architecture

```
Generator (100k vehicles)
    │
    ├─ Parallel.ForEachAsync (32 threads)
    ├─ 100 partitions (vehicle i → partition i % 100)
    └─ 1 Hz update per vehicle
    │
    ▼
Surgewave Broker
    │
    ▼
Dashboard
    ├─ 100 parallel partition consumers
    ├─ Grid aggregation (100x100 cells)
    └─ MapLibre visualization
        ├─ Zoom < 14: Heatmap
        ├─ Zoom 12-15: Clusters
        └─ Zoom > 14: Individual vehicles
```

## Running the Sample

```bash
# 1. Start the broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# 2. Start the generator (in new terminal)
dotnet run --project samples/MassFleetTracker/MassFleetTracker.Generator

# 3. Start the dashboard (in new terminal)
dotnet run --project samples/MassFleetTracker/MassFleetTracker.Dashboard

# 4. Open http://localhost:5000 in browser
```

## Data Volume

### Current Configuration

| Metric | Value |
|--------|-------|
| Vehicles | 100,000 |
| Update frequency | 1 Hz (per vehicle) |
| Messages/second | 100,000 |
| Message size (JSON) | ~150 Bytes |
| **Throughput** | **~15 MB/s** |

### Storage Requirements

| Period | Messages | Data Volume |
|--------|----------|-------------|
| Per minute | 6 million | ~900 MB |
| Per hour | 360 million | **~54 GB** |
| Per day | 8.6 billion | ~1.3 TB |

## Optimization Strategies

### 1. Compression (Easiest)

Enable compression on the producer to reduce data by 50-70%:

```csharp
options.CompressionType = CompressionType.Lz4; // or Snappy, Zstd
```

| Compression | Ratio | Throughput | Data/hour |
|-------------|-------|------------|-----------|
| None | 1.0x | 15 MB/s | 54 GB |
| LZ4 | 2-3x | 5-7 MB/s | 18-27 GB |
| Zstd | 3-4x | 4-5 MB/s | 14-18 GB |

### 2. Binary Serialization

Replace JSON with binary formats for significant size reduction:

| Format | Size/msg | Reduction | Data/hour |
|--------|----------|-----------|-----------|
| JSON | 150 Bytes | - | 54 GB |
| MessagePack | ~60 Bytes | 60% | 22 GB |
| Protobuf | ~45 Bytes | 70% | 16 GB |

### 3. Reduced Update Frequency

Not all use cases require 1 Hz updates:

| Interval | Messages/s | Data/hour |
|----------|------------|-----------|
| 1s | 100,000 | 54 GB |
| 5s | 20,000 | 11 GB |
| 10s | 10,000 | 5.4 GB |
| 30s | 3,333 | 1.8 GB |

### 4. Delta Encoding

Only send updates when position changes significantly:

```csharp
// Only send if moved > 10m or speed changed > 5 km/h
if (DistanceMoved > 10 || Math.Abs(SpeedDelta) > 5)
    SendUpdate();
```

Typical reduction: **60-80%** (especially for parked/slow vehicles)

### 5. Compact Payload

Reduce field sizes and precision:

```csharp
// Before: ~150 Bytes
{"VehicleId":"V00001","Latitude":52.512345,"Longitude":13.456789,"Speed":48.5,"Heading":180.0,"Status":0}

// After: ~50 Bytes (int ID, scaled coordinates, compact field names)
{"i":1,"a":525123,"o":134568,"s":48,"h":180,"t":0}
```

### Combined Optimization

Combining multiple strategies yields the best results:

| Strategy | Data/hour |
|----------|-----------|
| Baseline (JSON, 1 Hz) | 54 GB |
| + LZ4 compression | 22 GB |
| + Protobuf | 10 GB |
| + 5s interval | 2 GB |
| + Delta encoding | **< 1 GB** |

## Performance Metrics

Typical performance on modern hardware:

| Metric | Value |
|--------|-------|
| Producer throughput | 100,000 msg/s |
| Producer latency P50 | ~0.5 ms |
| Producer latency P99 | ~15 ms |
| Consumer throughput | 100,000+ msg/s |
| Dashboard update rate | 2 Hz |

## Prerequisites

- .NET 10 SDK
- Surgewave broker running on `localhost:9092`

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| High-Throughput Producing | 100,000 msg/s sustained with `Parallel.ForEachAsync` (32 threads) | Proves Surgewave handles massive ingestion workloads |
| Partitioned Topics | 100 partitions (vehicle i -> partition i % 100) | Parallel write and read paths for maximum throughput |
| Parallel Consumption | 100 concurrent partition consumers in dashboard | Read throughput matches write throughput |
| Grid Aggregation | 100x100 spatial grid for heatmap visualization | Efficient client-side aggregation of massive datasets |
| Adaptive Visualization | Heatmap / Cluster / Individual markers by zoom level | Practical UX for massive fleet display |
| Surgewave Native Protocol | `UseSurgewaveProtocol()` for maximum performance | Sub-millisecond latency even at 100K msg/s |

## Configuration

Key parameters in `Program.cs`:

```csharp
// Generator
const int vehicleCount = 100_000;      // Number of vehicles
const int partitionCount = 100;        // Topic partitions
const int updateIntervalMs = 1000;     // Update frequency (ms)
const int batchSize = 1000;            // Vehicles per batch

// Dashboard
const int AggregationIntervalMs = 500; // UI refresh rate
const int GridRows = 100;              // Aggregation grid
const int GridCols = 100;
```
