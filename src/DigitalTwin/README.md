# Digital Twin Sample

Industrial equipment simulation demonstrating real-time state synchronization, event sourcing, anomaly detection, and 3D visualization using Babylon.js. This sample showcases Surgewave's capabilities for high-frequency telemetry streaming and event-driven architectures in industrial IoT scenarios.

## Use Case

Manufacturing plants need digital twins of physical equipment for real-time monitoring, anomaly detection, and historical replay. This sample simulates 20 industrial machines (pumps, motors, conveyors, compressors) streaming telemetry at 500ms intervals through Surgewave, with a 3D dashboard for visualization and time-travel replay.

## Overview

This sample simulates a factory floor with 20 pieces of industrial equipment distributed across three production zones. Each piece of equipment generates continuous telemetry data and discrete operational events, which are streamed through Surgewave topics and visualized in real-time on a 3D dashboard.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           FACTORY FLOOR                                  │
├─────────────────────┬─────────────────────┬─────────────────────────────┤
│      ZONE A         │      ZONE B         │         ZONE C              │
│                     │                     │                             │
│  ┌───┐ ┌───┐       │  ┌───┐ ┌───┐       │  ┌───┐ ┌───┐ ┌───┐ ┌───┐   │
│  │P-1│ │P-2│       │  │P-3│ │P-4│       │  │P-5│ │P-6│ │K-3│ │K-4│   │
│  └───┘ └───┘       │  └───┘ └───┘       │  └───┘ └───┘ └───┘ └───┘   │
│  ┌───┐ ┌───┐       │  ┌───┐ ┌───┐       │  ┌───┐ ┌───┐               │
│  │M-1│ │M-2│       │  │M-3│ │M-4│       │  │M-5│ │M-6│               │
│  └───┘ └───┘       │  └───┘ └───┘       │  └───┘ └───┘               │
│  ┌───┐ ┌───┐       │  ┌───┐ ┌───┐       │                             │
│  │C-1│ │C-2│       │  │C-3│ │C-4│       │                             │
│  └───┘ └───┘       │  └───┘ └───┘       │                             │
│  ┌───┐ ┌───┐       │                     │                             │
│  │K-1│ │K-2│       │                     │                             │
│  └───┘ └───┘       │                     │                             │
└─────────────────────┴─────────────────────┴─────────────────────────────┘
  P = Pump  M = Motor  C = Conveyor  K = Compressor
```

## Features

### Equipment Simulation
- **20 Equipment Simulators**: 6 pumps, 6 motors, 4 conveyors, 4 compressors
- **Realistic Behavior**: Random walk with mean reversion, periodic maintenance cycles, random fault injection
- **500ms Update Interval**: High-frequency telemetry for real-time monitoring

### Dual State Model
- **Continuous Telemetry**: Temperature, vibration, pressure, flow rate, power consumption, RPM, current draw
- **Discrete Events**: Equipment started/stopped, maintenance started/completed, fault detected/cleared, mode changes

### Anomaly Detection
- **Threshold-Based**: Values exceeding configurable warning/critical limits
- **Trend-Based**: Sustained drift detection via linear regression over 30-second windows
- **Rapid Change**: Sudden value jumps between consecutive readings

### 3D Visualization
- **Interactive Factory Floor**: Navigate the 3D scene with mouse controls
- **Color-Coded Status**: Green (running), red (fault), yellow (warning), blue (maintenance), gray (off)
- **Equipment Selection**: Click to view detailed telemetry and event history

### Time-Travel Replay
- **Snapshot-Based**: Efficient state reconstruction from periodic snapshots
- **Variable Speed**: 1x, 2x, 5x, 10x playback speeds
- **Timeline Navigation**: Seek to any point in recorded history

## Architecture

```
┌──────────────┐     ┌─────────────────┐     ┌──────────────────┐
│   Generator  │     │   Surgewave Broker  │     │    Dashboard     │
│              │     │                 │     │                  │
│ ┌──────────┐ │     │ ┌─────────────┐ │     │ ┌──────────────┐ │
│ │ Pump     │─┼────►│ │ telemetry   │─┼────►│ │ Telemetry    │ │
│ │ Simulator│ │     │ │ topic       │ │     │ │ Buffer       │ │
│ └──────────┘ │     │ └─────────────┘ │     │ └──────────────┘ │
│ ┌──────────┐ │     │ ┌─────────────┐ │     │ ┌──────────────┐ │
│ │ Motor    │─┼────►│ │ events      │─┼────►│ │ Event        │ │
│ │ Simulator│ │     │ │ topic       │ │     │ │ Buffer       │ │
│ └──────────┘ │     │ └─────────────┘ │     │ └──────────────┘ │
│ ┌──────────┐ │     │ ┌─────────────┐ │     │ ┌──────────────┐ │
│ │ Conveyor │ │     │ │ anomalies   │◄┼─────┼─│ Anomaly      │ │
│ │ Simulator│ │     │ │ topic       │ │     │ │ Detection    │ │
│ └──────────┘ │     │ └─────────────┘ │     │ └──────────────┘ │
│ ┌──────────┐ │     │                 │     │ ┌──────────────┐ │
│ │Compressor│ │     │                 │     │ │ Babylon.js   │ │
│ │ Simulator│ │     │                 │     │ │ 3D View      │ │
│ └──────────┘ │     │                 │     │ └──────────────┘ │
└──────────────┘     └─────────────────┘     └──────────────────┘
```

## Surgewave Topics

| Topic | Purpose | Message Type | Frequency |
|-------|---------|--------------|-----------|
| `digitaltwin-telemetry` | High-frequency sensor data | `TelemetryReading` | 500ms per equipment |
| `digitaltwin-events` | State change events | `EquipmentEvent` (polymorphic) | On state change |
| `digitaltwin-anomalies` | Detected anomalies | `Anomaly` | On detection/clearing |

## Running the Sample

### Prerequisites

- .NET 10 SDK
- Surgewave broker running on `localhost:9092`

### Quick Start

```bash
# Terminal 1: Start Surgewave Broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Terminal 2: Start Generator (20 equipment simulators)
dotnet run --project samples/DigitalTwin/Generator

# Terminal 3: Start Dashboard
dotnet run --project samples/DigitalTwin/Dashboard
```

Open http://localhost:5000 in your browser.

### Generator Output

```
╔═══════════════════════════════════════════════════════════════════╗
║               DIGITAL TWIN - EQUIPMENT GENERATOR                  ║
╠═══════════════════════════════════════════════════════════════════╣
║  Equipment: 20 (6 Pumps, 6 Motors, 4 Conveyors, 4 Compressors)   ║
║  Telemetry Interval: 500ms                                        ║
║  Topics: digitaltwin-telemetry, digitaltwin-events               ║
╚═══════════════════════════════════════════════════════════════════╝

[12:00:00] Publishing telemetry for 20 equipment...
[12:00:00] P-001: Temperature=65.2°C, Pressure=4.8bar, FlowRate=125.3L/min
[12:00:00] M-001: Temperature=58.1°C, RPM=1485, Power=45.2kW
...
```

## Dashboard Features

### 3D Factory View
- **Pan**: Right-click and drag
- **Rotate**: Left-click and drag
- **Zoom**: Mouse wheel
- **Select**: Left-click on equipment

### Equipment Panel
- Real-time telemetry values with trend indicators
- Active anomalies with severity badges
- Recent event history (last 10 events)
- Operating mode indicator

### Time Controls
- **Live/Replay Toggle**: Switch between real-time and historical data
- **Speed Selector**: 1x, 2x, 5x, 10x playback speeds
- **Timeline Slider**: Navigate through recorded history
- **Jump Buttons**: Skip forward/backward by time intervals

## Project Structure

```
samples/DigitalTwin/
├── Shared/                              # Shared models library
│   ├── Equipment/
│   │   ├── Equipment.cs                 # Equipment entity record
│   │   ├── EquipmentType.cs             # Pump, Motor, Conveyor, Compressor
│   │   └── OperatingMode.cs             # Off, Starting, Running, Faulted, etc.
│   ├── Telemetry/
│   │   ├── TelemetryReading.cs          # Sensor reading with timestamp
│   │   └── TelemetryType.cs             # Temperature, Vibration, Pressure, etc.
│   ├── Events/
│   │   ├── EquipmentEvent.cs            # Base event with polymorphic JSON
│   │   ├── EquipmentStarted.cs          # Equipment started event
│   │   ├── EquipmentStopped.cs          # Equipment stopped event
│   │   ├── FaultDetected.cs             # Fault detection with details
│   │   ├── FaultCleared.cs              # Fault resolution event
│   │   ├── MaintenanceStarted.cs        # Maintenance cycle start
│   │   ├── MaintenanceCompleted.cs      # Maintenance cycle end
│   │   └── ModeChanged.cs               # Operating mode transition
│   ├── Anomalies/
│   │   ├── Anomaly.cs                   # Anomaly record with thresholds
│   │   ├── AnomalyType.cs               # Threshold, Trend, RapidChange
│   │   └── AnomalySeverity.cs           # Info, Warning, Critical
│   └── Thresholds/
│       └── EquipmentThresholds.cs       # Per-type threshold configuration
├── Generator/                           # Telemetry generator console app
│   ├── Program.cs                       # Entry point, creates 20 equipment
│   └── Simulators/
│       ├── EquipmentSimulator.cs        # Base simulator with random walk
│       ├── PumpSimulator.cs             # Pump-specific telemetry
│       ├── MotorSimulator.cs            # Motor-specific telemetry
│       ├── ConveyorSimulator.cs         # Conveyor-specific telemetry
│       └── CompressorSimulator.cs       # Compressor-specific telemetry
└── Dashboard/                           # Blazor Web dashboard
    ├── Program.cs                       # DI setup, service registration
    ├── Services/
    │   ├── TwinSnapshot.cs              # Snapshot and EquipmentDigitalTwin
    │   ├── TelemetryBuffer.cs           # Snapshot-based telemetry storage
    │   ├── EventBuffer.cs               # Event storage with mode tracking
    │   ├── EquipmentDataService.cs      # BackgroundService Surgewave consumer
    │   ├── AnomalyDetectionService.cs   # Detection algorithms
    │   └── ClientTwinState.cs           # Per-client playback state
    ├── Components/
    │   └── Pages/
    │       └── DigitalTwinView.razor    # Main dashboard UI
    └── wwwroot/
        └── js/
            └── babylonjs-interop.js     # 3D rendering with Babylon.js
```

## Equipment Types

| Type | Count | Telemetry Sensors | Typical Values |
|------|-------|-------------------|----------------|
| Pump | 6 | Temperature, Vibration, Pressure, FlowRate, Power, Current | 60°C, 2mm/s, 5bar, 120L/min, 15kW, 25A |
| Motor | 6 | Temperature, Vibration, RPM, Power, Current | 55°C, 1.5mm/s, 1500rpm, 45kW, 80A |
| Conveyor | 4 | Temperature, Vibration, RPM, Power, Current | 40°C, 1mm/s, 60rpm, 5kW, 10A |
| Compressor | 4 | Temperature, Vibration, Pressure, RPM, Power, OilLevel | 70°C, 3mm/s, 8bar, 3000rpm, 75kW, 85% |

## Anomaly Detection Details

### Threshold Detection
Configurable warning and critical limits per equipment type and telemetry sensor:

| Equipment | Sensor | Warning | Critical |
|-----------|--------|---------|----------|
| Pump | Temperature | 75°C | 85°C |
| Pump | Vibration | 4mm/s | 6mm/s |
| Motor | Temperature | 70°C | 80°C |
| Motor | Vibration | 3mm/s | 5mm/s |
| Compressor | Pressure | 9bar | 10bar |

### Trend Detection
Linear regression over a 30-second sliding window detects:
- Sustained temperature rise (overheating)
- Increasing vibration (bearing wear)
- Pressure drift (seal degradation)

### Rapid Change Detection
Flags sudden value changes exceeding thresholds:
- Temperature: >10°C in one reading
- Vibration: >2mm/s in one reading
- Pressure: >1bar in one reading

Anomalies automatically clear when values return to normal operating range.

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| High-Throughput Publishing | 40 msg/s (20 equipment x 2 readings/s) across 3 topics | Continuous telemetry ingestion without backpressure |
| Consumer Groups | Dashboard uses unique group IDs per client | Independent consumption for multiple browser sessions |
| Topic Auto-Creation | Topics created automatically on first publish | Zero setup -- just produce and topics appear |
| Polymorphic Serialization | `[JsonDerivedType]` for event hierarchy with type discriminators | Multiple event types flow through single topic |
| Offset-Based Replay | Snapshot-based time travel using message offsets | Scrub through historical telemetry at variable speed |
| JSON Typed Producers | `Serializers.Json<TelemetryReading>()` and `Serializers.Json<EquipmentEvent>()` | Strongly-typed events with automatic serialization |
| Surgewave Native Protocol | `UseSurgewaveProtocol()` for low-latency sensor data | Sub-millisecond delivery for real-time dashboard updates |

## Configuration

### Generator (`appsettings.json`)
```json
{
  "Surgewave": {
    "BootstrapServers": "localhost:9092"
  },
  "Generator": {
    "TelemetryIntervalMs": 500,
    "FaultProbability": 0.001,
    "MaintenanceIntervalMinutes": 30
  }
}
```

### Dashboard (`appsettings.json`)
```json
{
  "Surgewave": {
    "BootstrapServers": "localhost:9092"
  },
  "Dashboard": {
    "SnapshotInterval": 100,
    "MaxHistorySize": 10000
  }
}
```

## Extending the Sample

### Adding New Equipment Types
1. Add enum value to `EquipmentType.cs`
2. Create new simulator class extending `EquipmentSimulator`
3. Add threshold configuration in `EquipmentThresholds.cs`
4. Update 3D mesh creation in `babylonjs-interop.js`

### Adding New Telemetry Sensors
1. Add enum value to `TelemetryType.cs`
2. Update simulator to generate values
3. Add threshold limits if anomaly detection needed

### Adding New Event Types
1. Create new class extending `EquipmentEvent`
2. Add `[JsonDerivedType]` attribute to base class
3. Update event handling in dashboard services

## Related Samples

- **FleetTracker**: Real-time GPS tracking with MapLibre visualization
- **IoTDashboard**: Sensor monitoring with threshold alerts
- **EventSourcing**: Event-driven architecture patterns
