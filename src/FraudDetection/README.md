# Fraud Detection -- Real-Time Credit Card Monitoring

Demonstrates **real-time fraud detection** using Surgewave event streaming. Four independent rules evaluate every credit card transaction in sub-second latency.

## Use Case

Payment processors must evaluate every transaction for fraud in real-time. This sample shows how Surgewave's event streaming enables multiple independent fraud detection rules (velocity, amount anomaly, impossible travel, card testing) to evaluate transactions in sub-second latency, with stateful per-card tracking using sliding windows.

## Architecture

```
  +-------------------+       +-------------------+       +------------------+
  |  Transaction      | ----> |  Fraud Detection  | ----> |  Alert Monitor   |
  |  Generator        |       |  Engine           |       |  (Live Display)  |
  +-------------------+       +-------------------+       +------------------+
         |                           |                           |
    transactions              fraud-alerts                 Console output
    (Surgewave topic)             (Surgewave topic)                (real-time)
```

## Fraud Detection Rules

### Rule 1: Velocity Check
- **Window**: Sliding, 5 minutes
- **Trigger**: More than 3 transactions per card within the window
- **Severity**: HIGH
- **Use Case**: Rapid consecutive purchases indicate compromised card

### Rule 2: Amount Anomaly
- **Method**: Running average per card (KTable-style tracking)
- **Trigger**: Transaction exceeds 10x the card's historical average
- **Severity**: CRITICAL
- **Use Case**: Sudden high-value purchase on normally low-spend card

### Rule 3: Geo-Velocity (Impossible Travel)
- **Method**: Haversine distance between consecutive transaction locations
- **Trigger**: Travel speed exceeds 500 km/h between transactions
- **Severity**: CRITICAL
- **Use Case**: Card used in Berlin, then 30 seconds later in Tokyo

### Rule 4: Card Testing Pattern
- **Window**: 10 minutes
- **Trigger**: More than 5 micro-transactions (< 5 EUR) in the window
- **Severity**: HIGH
- **Use Case**: Stolen card tested with small amounts before large fraud

## Injected Fraud Patterns

| Pattern | Card | Description |
|---------|------|-------------|
| Velocity | Card-5 | 4+ transactions within 2 minutes |
| Amount Anomaly | Card-8 | 4,999.99 EUR (normal avg ~80 EUR) |
| Geo-Velocity | Card-12 | Berlin to Tokyo in 1 second |
| Card Testing | Card-15 | 10x micro-transactions (0.50-3.50 EUR) |

## Sample Output

```
  FRAUD ALERT | Card: Card-5  | Rule: Velocity Check | 4 transactions in 5 minutes
  FRAUD ALERT | Card: Card-8  | Rule: Amount Anomaly | Transaction 4,999.99 EUR is 62.5x average
  FRAUD ALERT | Card: Card-12 | Rule: Geo-Velocity   | Impossible travel: 8,920 km in 1.0 min
  FRAUD ALERT | Card: Card-15 | Rule: Card Testing   | 6 micro-transactions in 10 minutes
```

## How to Run

```bash
dotnet run --project src/FraudDetection
```

No external dependencies required -- uses an embedded Surgewave broker.

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Embedded Broker | `SurgewaveRuntime.CreateBuilder()` with in-memory storage | Self-contained demo with zero external dependencies |
| Multiple Topics | `transactions` and `fraud-alerts` topics | Separate concerns: raw events vs. detected alerts |
| Consumer Groups | `fraud-detector` and `alert-monitor` groups | Independent processing pipelines on same data |
| Sliding Windows | Per-card transaction tracking with time-based eviction | Velocity and card testing rules need windowed state |
| Partitioned State | `ConcurrentDictionary` per card for profiles/locations | Stateful stream processing with per-key aggregation |
| JSON Serialization | `Serializers.Json<Transaction>()` typed producers | Strongly-typed event schemas with automatic serialization |
| Fan-Out Processing | Transaction -> Detection -> Alert pipeline | One event triggers multiple independent downstream consumers |

## Key Code Highlights

### Sliding Window for Velocity Detection

```csharp
// Track recent transaction times per card in a sliding 5-minute window
var times = cardRecentTimes.GetOrAdd(tx.CardId, _ => new ConcurrentQueue<DateTimeOffset>());
times.Enqueue(tx.Timestamp);
while (times.TryPeek(out var oldest) && tx.Timestamp - oldest > TimeSpan.FromMinutes(5))
    times.TryDequeue(out _);
if (times.Count > 3) { /* VELOCITY ALERT */ }
```

### Geo-Velocity with Haversine Distance

```csharp
var distanceKm = HaversineDistance(lastLoc.Lat, lastLoc.Lon, tx.Lat, tx.Lon);
var speedKmh = distanceKm / timeDiff.TotalHours;
if (speedKmh > 500 && distanceKm > 100) { /* IMPOSSIBLE TRAVEL ALERT */ }
```

## Key Takeaway

**Surgewave's event streaming enables sub-second fraud detection with multiple independent rules evaluated per transaction, using stateful per-card tracking backed by topic-based event flow.**
