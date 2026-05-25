# IoT Schema Evolution -- Schema Registry

IoT sensors with different firmware versions produce telemetry using different schema versions. Demonstrates schema registration, backward/forward compatibility, and version management.

## Use Case

IoT fleets with staggered firmware updates produce data using different schema versions simultaneously. This sample shows how Surgewave's built-in Schema Registry manages schema evolution -- ensuring backward and forward compatibility so consumers can read messages from any firmware version without breakage.

## What It Does

- **Schema v1**: Basic telemetry (deviceId, temperature, humidity, timestamp)
- **Schema v2**: Adds `batteryLevel` with default value (firmware 2.0 rollout)
- **Schema v3**: Adds nested `location` object (GPS-enabled firmware 3.0)
- **Mixed Producers**: Devices on different firmware versions coexist
- **Unified Consumer**: Reads all versions regardless of schema
- **Breaking Change**: Shows what happens when a required field is removed
- **Version Timeline**: Displays all schema versions and their evolution

## Architecture

```
 ┌────────────┐ ┌────────────┐ ┌────────────┐
 │ SENSOR-001 │ │ SENSOR-005 │ │ SENSOR-010 │
 │ FW 3.0     │ │ FW 2.0     │ │ FW 1.0     │
 │ (schema v3)│ │ (schema v2)│ │ (schema v1)│
 └─────┬──────┘ └─────┬──────┘ └─────┬──────┘
       │               │               │
       └───────┬───────┴───────┬───────┘
               ▼               │
 ┌─────────────────────────────▼──────┐
 │    Surgewave Schema Registry           │
 │  ┌─────┐  ┌─────┐  ┌─────┐       │
 │  │ v1  │→ │ v2  │→ │ v3  │       │
 │  └─────┘  └─────┘  └─────┘       │
 └─────────────────────┬──────────────┘
                       │
                       ▼
 ┌────────────────────────────────────┐
 │   Topic: device-telemetry          │
 │   (mixed v1, v2, v3 messages)      │
 └─────────────────────┬──────────────┘
                       │
                       ▼
 ┌────────────────────────────────────┐
 │   Unified Consumer                 │
 │   (reads all schema versions)      │
 └────────────────────────────────────┘
```

## How to Run

```bash
dotnet run --project src/IotSchemaEvolution
```

## What to Expect

1. Broker starts with built-in Schema Registry
2. Schema v1 registered and 10 devices produce basic telemetry
3. Schema v2 registered; mixed v1/v2 production (firmware rollout)
4. Consumer reads all versions transparently
5. Schema v3 registered with nested location object
6. Breaking change attempted (removing temperature field)
7. Full schema evolution timeline displayed

## Key Surgewave Features Demonstrated

| Feature | Usage |
|---------|-------|
| **Schema Registry** | Built into Surgewave native protocol |
| **JSON Schema** | Register, version, and validate schemas |
| **Backward Compatibility** | New consumers read old messages |
| **Forward Compatibility** | Old consumers tolerate new fields |
| **Default Values** | Missing fields get sensible defaults |
| **Version Management** | Track schema evolution per subject |
