# Fleet Tracker Sample

Real-time vehicle tracking with MapLibre visualization, per-client playback controls, and time-travel capabilities.

## Use Case

Fleet management, logistics, and ride-sharing applications need to track vehicles in real-time and replay historical routes. Surgewave's topic-based architecture makes time-travel trivial -- each client independently scrubs through the same topic at any speed, while live clients see positions in real-time.

## What It Does

- **Generator**: Simulates 50 vehicles with realistic GPS movement
- **Dashboard**: Web UI with live map, vehicle markers, and trails
- **Time Travel**: Scrub through historical positions at variable speeds (1x-50x)
- **Per-Client Playback**: Each user controls their own playback independently

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Start the vehicle generator
dotnet run --project samples/FleetTracker/Generator

# Start the dashboard (opens browser)
dotnet run --project samples/FleetTracker/Dashboard
```

## Why Surgewave for Fleet Tracking?

### Real-Time Performance

| Metric | Requirement | Surgewave Capability |
|--------|-------------|------------------|
| Update Frequency | 1-10 Hz per vehicle | **1M+ msg/s** throughput |
| Latency | < 100ms end-to-end | **45µs** P50 latency |
| Concurrent Vehicles | 10,000+ | Easily handled |

### Time-Travel Architecture

```
                    Live Feed (Latest)
                           │
    ┌──────────────────────┼──────────────────────┐
    │                      ▼                      │
    │  ┌─────────────────────────────────────┐   │
    │  │         Surgewave Topic: fleet-gps       │   │
    │  │  [t=0] [t=1] [t=2] ... [t=N] [LIVE]  │   │
    │  └─────────────────────────────────────┘   │
    │       │       │       │                    │
    │       ▼       ▼       ▼                    │
    │    Client   Client  Client                 │
    │   (Live)   (t=100) (t=500)                │
    │                                            │
    │  Each client independently scrubs          │
    │  through the same topic at any speed      │
    └────────────────────────────────────────────┘
```

### Key Benefits

| Feature | How Surgewave Enables It |
|---------|---------------------|
| **Historical Replay** | Topics retain all messages; seek to any offset |
| **Independent Playback** | Each consumer has its own offset position |
| **Variable Speed** | Consumer controls consumption rate (1x-50x) |
| **Live Mode** | Switch to tail of topic for real-time |
| **Snapshot State** | Reconstruct vehicle positions at any point |

### Comparison with Alternatives

| Solution | Real-Time | History | Time-Travel | Scale |
|----------|-----------|---------|-------------|-------|
| REST Polling | Poor | Database | Complex | Limited |
| WebSocket + DB | Good | Separate | Complex | Medium |
| Redis Streams | Good | Limited | Manual | Medium |
| **Surgewave** | **Excellent** | **Built-in** | **Native** | **Massive** |

### Operational Benefits

| Aspect | Traditional | Surgewave |
|--------|-------------|-------|
| Storage | GPS DB + Cache + Queue | Single Surgewave topic |
| Replay | Custom implementation | Seek to offset |
| Scaling | Complex sharding | Partitions + Consumer Groups |
| Retention | Manual cleanup | Configurable policies |

### Use Cases

- **Fleet Management**: Track delivery vehicles, field service
- **Asset Tracking**: Monitor equipment, containers, inventory
- **Ride-Sharing**: Real-time driver/passenger matching
- **Logistics**: Supply chain visibility, route optimization
- **Sports Analytics**: Player/ball tracking with replay

### Architecture for 10K+ Vehicles

```
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│  Vehicle 1  │  │  Vehicle 2  │  │ Vehicle 10K │
│ GPS Device  │  │ GPS Device  │  │ GPS Device  │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘
       │                │                │
       └────────────────┼────────────────┘
                        ▼
              ┌─────────────────┐
              │   Surgewave Broker  │
              │  (Partitioned)  │
              │  P0  P1  P2  P3 │
              └────────┬────────┘
                       │
       ┌───────────────┼───────────────┐
       ▼               ▼               ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│ Dashboard 1 │ │ Dashboard 2 │ │ Dashboard N │
│ (Region A)  │ │ (Region B)  │ │ (Replay)    │
└─────────────┘ └─────────────┘ └─────────────┘
```

## Key Takeaway

**Surgewave's topic-based architecture provides built-in time-travel, making historical replay and per-client playback trivial to implement.**
