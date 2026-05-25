# IoT Dashboard Sample

Real-time IoT sensor monitoring with 20 virtual sensors, Blazor dashboard, MudChart visualization, and threshold alerts.

## Use Case

Industrial and building monitoring systems need to ingest high-frequency sensor data, detect threshold violations in real-time, and display live dashboards. Surgewave provides a unified platform for IoT data ingestion, real-time alerting, and long-term storage -- replacing multiple specialized tools.

## What It Does

- **Generator**: Simulates 20 sensors (temperature, humidity, pressure, CO2, noise)
- **Dashboard**: Real-time Blazor web UI with live charts
- **Alerts**: Threshold-based alerting when values exceed limits
- **Statistics**: Running aggregations (min, max, avg) per sensor

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Start sensor generator
dotnet run --project samples/IotDashboard/Generator

# Start dashboard (opens browser)
dotnet run --project samples/IotDashboard/Dashboard
```

## Why Surgewave for IoT?

### Scale Requirements

| Metric | Typical IoT | Surgewave Capability |
|--------|-------------|------------------|
| Devices | 10K-1M | **Millions** |
| Data Rate | 1-100 Hz/device | **1M+ msg/s** |
| Latency | < 1 second | **< 1 ms** |
| Retention | Days-Months | **Configurable** |

### IoT Data Pipeline

```
┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐
│Sensor 1 │ │Sensor 2 │ │Sensor 3 │ │Sensor N │
│ (Temp)  │ │(Humidity│ │(Pressure│ │  (...)  │
└────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘
     │           │           │           │
     └───────────┴─────┬─────┴───────────┘
                       ▼
┌─────────────────────────────────────────────────────┐
│                    Surgewave Broker                      │
│  ┌──────────────────────────────────────────────┐  │
│  │           Topic: sensor-readings              │  │
│  │  Partitioned by sensor_id for parallelism    │  │
│  └──────────────────────────────────────────────┘  │
│                        │                           │
│    ┌───────────────────┼───────────────────┐      │
│    ▼                   ▼                   ▼      │
│ ┌──────┐         ┌──────────┐        ┌────────┐  │
│ │Alerts│         │Aggregator│        │Dashboard│  │
│ │Engine│         │(Streams) │        │Consumer │  │
│ └──────┘         └──────────┘        └────────┘  │
└─────────────────────────────────────────────────────┘
```

### Key Benefits

| Feature | How Surgewave Enables It |
|---------|---------------------|
| **Real-Time Ingestion** | Sub-millisecond message handling |
| **Stream Processing** | Built-in Kafka Streams for aggregations |
| **Scalable Storage** | Tiered storage (hot/warm/cold) |
| **Replay & Debug** | Reprocess historical data anytime |
| **Multiple Consumers** | Dashboard, alerts, ML all read same data |

### Comparison with IoT Platforms

| Platform | Latency | Scale | Cost | Flexibility |
|----------|---------|-------|------|-------------|
| AWS IoT Core | 100ms+ | High | $$$ | Limited |
| Azure IoT Hub | 100ms+ | High | $$$ | Limited |
| InfluxDB | 10ms | Medium | $$ | Time-series only |
| **Surgewave** | **<1ms** | **Very High** | **Low** | **Full** |

### Stream Processing for IoT

Surgewave's built-in Streams library enables real-time analytics:

```csharp
// Windowed aggregations
stream
    .GroupByKey()
    .WindowedBy(TumblingWindow.Of(TimeSpan.FromMinutes(1)))
    .Aggregate(
        () => new SensorStats(),
        (key, value, stats) => stats.Update(value))
    .ToStream()
    .To("sensor-stats");

// Threshold alerting
stream
    .Filter((key, reading) => reading.Value > threshold)
    .To("sensor-alerts");
```

### Data Retention Strategies

| Tier | Duration | Storage | Use Case |
|------|----------|---------|----------|
| Hot | 1 hour | Memory | Real-time dashboards |
| Warm | 7 days | Local SSD | Recent analysis |
| Cold | 1 year | S3/Azure/GCS | Compliance, ML training |

### Production Architecture

```
┌─────────────────────────────────────────────────────┐
│                   Edge Gateway                       │
│  (Protocol translation, local buffering)            │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│              Surgewave Cluster (3+ brokers)              │
│  ┌──────────────────────────────────────────────┐  │
│  │  sensor-raw (partitioned by device_id)       │  │
│  │  sensor-aggregated (windowed stats)          │  │
│  │  sensor-alerts (threshold violations)        │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────┬───────────────────────────────┘
                      │
    ┌─────────────────┼─────────────────┐
    ▼                 ▼                 ▼
┌─────────┐     ┌──────────┐     ┌──────────┐
│Dashboard│     │ Alert    │     │   ML     │
│  (Live) │     │ Service  │     │ Pipeline │
└─────────┘     └──────────┘     └──────────┘
```

### Unique IoT Capabilities

| Capability | Description |
|------------|-------------|
| **Backpressure** | Sensors can burst; Surgewave buffers gracefully |
| **Exactly-Once** | No duplicate readings in aggregations |
| **Late Data** | Windowed joins handle out-of-order events |
| **Schema Evolution** | Add new sensor fields without downtime |

## Key Takeaway

**Surgewave provides a unified platform for IoT data ingestion, real-time processing, and long-term storage - replacing multiple specialized tools.**
