# Supply Chain Tracker Sample

Tracks orders from factory to customer through a multi-step state machine with failure paths, ETA prediction, and real-time alerting.

## Use Case

Supply chain and logistics systems need to track entities through complex state machines with branching paths (QA rejection, customs holds, delivery failures). Surgewave's compacted topics provide a natural "latest state" view per entity (like a DHL tracking page), while the events topic maintains full audit history.

## What It Does

- **State Machine**: Orders follow defined transitions with branching failure paths
- **Compacted Topic**: Latest order state per order ID (like a DHL tracking page)
- **Event Sourcing**: Full history of all state transitions in events topic
- **ETA Prediction**: Remaining delivery time estimated from current stage
- **Alert Generation**: Anomalous transitions (QA fail, customs hold) trigger alerts

## How to Run

```bash
dotnet run --project src/SupplyChainTracker
```

Self-contained with embedded broker -- no external dependencies needed.

## State Machine Diagram

```
                        OrderPlaced
                             |
                             v
                       InProduction
                             |
                             v
                       QualityCheck <---------+
                        /        \            |
                       v          v           |
              QualityApproved   QualityRejected
                    |                |
                    v                v
                  Packed          Rework ------+
                    |
                    v
                 PickedUp
                    |
                    v
                 InTransit
                    |
                    v
             CustomsClearance <----+
                /        \        |
               v          v       |
       OutForDelivery  CustomsHeld
          /      \          |
         v        v         v
     Delivered  DeliveryFailed  DocumentsSubmitted --+
                    |
                    v
               Rescheduled
                    |
                    v
             OutForDelivery (retry)
```

## 10 Order Scenarios

| Order   | Scenario                        | Path         |
|---------|---------------------------------|--------------|
| ORD-001 | Happy path (standard)           | Direct       |
| ORD-002 | Happy path (quick)              | Direct       |
| ORD-003 | Happy path (slow)               | Direct       |
| ORD-004 | Happy path (medium)             | Direct       |
| ORD-005 | Happy path (standard)           | Direct       |
| ORD-006 | QA rejected, rework             | +3 steps     |
| ORD-007 | Customs hold, resubmit          | +3 steps     |
| ORD-008 | Delivery failed, rescheduled    | +3 steps     |
| ORD-009 | Express order (fast)            | Direct (40%) |
| ORD-010 | International (extra customs)   | +1 step      |

## ETA Calculation

| Stage             | Expected Duration |
|-------------------|-------------------|
| OrderPlaced       | 2 min             |
| InProduction      | 5 min             |
| QualityCheck      | 2 min             |
| QualityApproved   | 1 min             |
| Packed            | 2 min             |
| PickedUp          | 1 min             |
| InTransit         | 8 min             |
| CustomsClearance  | 3 min             |
| OutForDelivery    | 4 min             |

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Embedded Broker | `SurgewaveRuntime.CreateBuilder()` with in-memory storage | Self-contained demo with zero dependencies |
| Compacted Topic | Latest order state per order ID (like DHL tracking) | Efficient "current state" view without scanning full history |
| Event Sourcing | Full history of all state transitions in events topic | Complete audit trail for regulatory compliance |
| State Machine | Defined transitions with branching failure paths | Complex workflows modeled as event sequences |
| ETA Prediction | Remaining time estimated from current stage durations | Real-time delivery estimates based on pipeline position |
| Alert Generation | Anomalous transitions trigger alerts (QA fail, customs hold) | Proactive issue detection in supply chain flow |
| JSON Serialization | Typed producers with order state records | Strongly-typed event schemas across the pipeline |

## Key Code Highlights

### Compacted Topic for Latest State

```csharp
// Each order's latest state is kept per key -- old states compacted away
await stateProducer.ProduceAsync("order-state", orderId, currentState);
// Reading "order-state" always shows the latest status per order
```

### State Machine Transitions

```csharp
// Valid transitions define the supply chain flow
var validTransitions = new Dictionary<OrderStage, OrderStage[]>
{
    [OrderStage.OrderPlaced] = [OrderStage.InProduction],
    [OrderStage.QualityCheck] = [OrderStage.QualityApproved, OrderStage.QualityRejected],
    [OrderStage.CustomsClearance] = [OrderStage.OutForDelivery, OrderStage.CustomsHeld],
};
```

## Key Takeaway

**Surgewave's compacted topics provide a natural "latest state" view per entity, while the events topic maintains full audit history -- ideal for supply chain tracking with complex state machines.**
