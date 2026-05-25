# Payment Gateway -- Multi-Broker Clustering & Failover

Payment processing with a 3-broker cluster. One broker crashes mid-stream; the cluster continues without data loss. All payments are accounted for at the end.

## Use Case

Payment systems require zero data loss and continuous availability even during broker failures. This sample demonstrates Surgewave's multi-broker clustering with automatic failover -- a broker crashes mid-stream, the cluster continues processing, and every payment is accounted for in the final summary.

## What It Does

- **3-Broker Cluster**: Three embedded Surgewave brokers
- **Payment Stream**: 100 payments produced continuously
- **Payment Processor**: Consumes payments, produces results (92% approval rate)
- **Broker Crash**: Broker 2 crashes at payment 30 (ChaosEngine fault injection)
- **Automatic Failover**: Remaining brokers handle all traffic
- **Broker Recovery**: Broker 2 rejoins at payment 60
- **Zero Data Loss**: Every payment ID accounted for in final summary

## Architecture

```
 ┌──────────────────────────────────────────────┐
 │            3-Broker Surgewave Cluster             │
 │                                              │
 │  ┌──────────┐ ┌──────────┐ ┌──────────┐    │
 │  │ Broker 0 │ │ Broker 1 │ │ Broker 2 │    │
 │  │ (Leader) │ │          │ │  CRASH!  │    │
 │  └──────────┘ └──────────┘ └──────────┘    │
 │       │             │            X          │
 │       └─────┬───────┘                      │
 └─────────────┼──────────────────────────────┘
               │
    ┌──────────┴──────────┐
    ▼                     ▼
 ┌───────────┐    ┌─────────────┐
 │ payments  │    │ payment-    │
 │ (topic)   │    │ results     │
 └─────┬─────┘    └──────▲──────┘
       │                 │
       ▼                 │
 ┌────────────────────────────────┐
 │      Payment Processor         │
 │  consume -> process -> produce │
 └────────────────────────────────┘
```

## How to Run

```bash
dotnet run --project src/PaymentGateway
```

## What to Expect

1. Three brokers start on random ports
2. Payment processor subscribes to `payments` topic
3. 100 payments produced; at payment 30, Broker 2 crashes
4. Payments continue without interruption
5. Broker 2 recovers at payment 60
6. Final summary: all payments accounted for, chaos timeline displayed

## Key Surgewave Features Demonstrated

| Feature | Usage |
|---------|-------|
| **Multi-Broker** | 3 independent embedded brokers |
| **ChaosEngine** | Fault injection for resilience testing |
| **Automatic Failover** | Traffic shifted on broker crash |
| **Leader Election** | Orphaned partitions get new leaders |
| **Broker Recovery** | Crashed broker rejoins cluster |
| **Zero Data Loss** | All payment IDs tracked and verified |
