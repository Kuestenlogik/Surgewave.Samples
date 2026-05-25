# Smart Factory Sample

Predictive maintenance system monitoring 5 CNC machines with telemetry analysis, anomaly detection using tumbling windows, and AI Guardrails for ticket validation.

## Use Case

Manufacturing facilities need to detect equipment anomalies and predict failures before they cause downtime. This sample demonstrates how Surgewave's streaming capabilities (tumbling windows, multi-topic pipelines) combined with AI Guardrails enable a complete predictive maintenance system -- from sensor ingestion to validated maintenance tickets.

## What It Does

- **Machine Telemetry**: 5 CNC machines produce vibration, temperature, RPM, and power data
- **Tumbling Windows**: 10-second aggregation windows compute statistics per machine
- **Anomaly Detection**: Rules-based detection for vibration, temperature, power, and degradation patterns
- **Predictive Maintenance**: Trend analysis predicts bearing failure ~48h in advance
- **AI Guardrails**: ContentPolicyGuardrail validates maintenance ticket content before creation
- **Ticket Generation**: Automatic maintenance tickets with priority based on severity

## How to Run

```bash
dotnet run --project src/SmartFactory
```

Self-contained with embedded broker -- no external dependencies needed.

## Factory Architecture

```
  CNC-01    CNC-02    CNC-03    CNC-04    CNC-05
  Normal    Bearing   Overheat  Power     Normal
    |         |         |         |         |
    +---------+---------+---------+---------+
                        |
                        v
               machine-telemetry (topic)
                        |
                        v
              +-------------------+
              | Anomaly Detector  |
              | (10s windows)     |
              | avg/stddev/min/max|
              +--------+----------+
                       |
            +----------+----------+
            |                     |
            v                     v
    anomaly-events         +-------------+
      (topic)              | Guardrails  |
                           | ContentPolicy|
                           +------+------+
                                  |
                                  v
                        maintenance-tickets
                            (topic)
```

## Machine Profiles

| Machine | Name                  | Mode                 | Behavior                              |
|---------|-----------------------|----------------------|---------------------------------------|
| CNC-01  | Milling Center Alpha  | Normal               | Stable operation, baseline readings   |
| CNC-02  | Milling Center Beta   | Bearing Degradation  | Vibration increases over time         |
| CNC-03  | Lathe Gamma           | Overheating          | Temperature spikes after 20 seconds   |
| CNC-04  | Grinder Delta         | Power Fluctuation    | Intermittent power drops              |
| CNC-05  | Milling Center Epsilon| Normal               | Stable operation, baseline readings   |

## Anomaly Detection Rules

| Rule | Metric              | Threshold       | Severity     | Action                              |
|------|---------------------|-----------------|--------------|-------------------------------------|
| a    | Vibration           | > 8 mm/s        | WARNING      | Schedule maintenance next shift     |
| b    | Vibration           | > 12 mm/s       | CRITICAL     | Immediate maintenance ticket        |
| c    | Temperature         | > 80 C          | WARNING      | Schedule maintenance next shift     |
| d    | Temperature         | > 95 C          | CRITICAL     | Emergency shutdown                  |
| e    | StdDev(vibration)   | > 3x baseline   | DEGRADATION  | Predictive: replace bearing in 48h  |
| f    | Power               | < 80% nominal   | POWER_ISSUE  | Check electrical supply             |

## AI Guardrails Integration

Maintenance tickets pass through `ContentPolicyGuardrail` before creation:

| Check             | Rule                                          |
|-------------------|-----------------------------------------------|
| Min length        | >= 20 characters                              |
| Max length        | <= 500 characters                             |
| Required patterns | Must contain machine ID and severity level    |
| Forbidden patterns| Must not contain TODO, FIXME, or HACK         |

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Embedded Broker | `SurgewaveRuntime.CreateBuilder()` with in-memory storage | Self-contained predictive maintenance demo |
| Multiple Topics | `machine-telemetry`, `anomaly-events`, `maintenance-tickets`, `machine-state` | Separate concerns across the detection pipeline |
| Tumbling Windows | 10-second windows accumulate telemetry for statistical analysis | Smooths noise and detects trends in sensor data |
| Consumer Groups | `anomaly-detector` group processes telemetry stream | Dedicated processing pipeline with independent scaling |
| AI Guardrails | `ContentPolicyGuardrail` validates ticket descriptions | Ensures generated tickets meet quality standards before creation |
| JSON Serialization | Typed `MachineTelemetry`, `AnomalyEvent`, `MaintenanceTicket` | Strongly-typed event schemas across the pipeline |
| Fan-Out Processing | Telemetry -> Anomaly Detection -> Ticket Generation pipeline | Multi-stage processing with independent consumers at each stage |
| Rules Engine | 6 anomaly rules (vibration, temperature, power, degradation) | Configurable threshold and pattern detection per metric |

## Key Code Highlights

### Tumbling Window Aggregation

```csharp
// Accumulate telemetry readings into 10-second windows
var window = windowData.GetOrAdd(machineId, _ => new WindowAccumulator());
window.Add(telemetry);

// When window is complete, compute statistics and check rules
var stats = window.ComputeStats();
if (stats.AvgVibration > vibrationCritical) { /* CRITICAL anomaly */ }
if (stats.StdDevVibration > baselineStdDev * 3.0) { /* DEGRADATION pattern */ }
window.Reset();
```

### AI Guardrail Validation

```csharp
var ticketPolicy = new ContentPolicyGuardrail(new ContentPolicyOptions
{
    MinContentLength = 20,
    MaxContentLength = 500,
    RequiredPatterns = [@"CNC-\d{2}", @"(WARNING|CRITICAL|DEGRADATION|POWER_ISSUE)"],
    ForbiddenPatterns = [@"(?i)\b(todo|fixme|hack)\b"],
});

var guardrailResult = await ticketPolicy.EvaluateAsync(ticket.Description);
if (guardrailResult.Passed) { await ticketProducer.ProduceAsync(ticketTopic, ...); }
```

## Key Takeaway

**Surgewave streams with tumbling windows enable real-time anomaly detection on sensor data, while AI Guardrails ensure generated maintenance tickets meet quality standards before entering the ticket system.**
