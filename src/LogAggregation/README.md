# Log Aggregation -- Consumer Groups & Rebalancing

DevOps log aggregation with 3 parallel workers. Demonstrates consumer group partition assignment, worker crash handling, and elastic scaling.

## Use Case

Centralized log processing systems must handle worker failures gracefully and scale elastically. This sample demonstrates Surgewave's consumer group rebalancing -- when a worker crashes, its partitions are automatically redistributed; when a new worker joins, partitions rebalance to spread the load.

## What It Does

- **6-Partition Topic**: Simulates high-throughput application logs
- **3 Consumer Workers**: Same group `log-processors`, partitions distributed
- **Log Producer**: 3 microservices emit DEBUG/INFO/WARN/ERROR logs
- **Worker Crash**: Worker 3 stops; its partitions redistribute to workers 1 & 2
- **Scale Up**: Worker 4 joins; partitions rebalance across 3 workers
- **Per-Worker Stats**: Message counts, severity breakdown, partition ownership

## Architecture

```
 ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
 │ api-gateway │  │ user-svc    │  │ order-svc   │
 │ (Producer)  │  │ (Producer)  │  │ (Producer)  │
 └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
        │                │                │
        └───────┬────────┴────────┬───────┘
                ▼                 ▼
 ┌──────────────────────────────────────────┐
 │   Topic: application-logs (6 partitions) │
 │  ┌─P0─┐ ┌─P1─┐ ┌─P2─┐ ┌─P3─┐ ┌─P4─┐ ┌─P5─┐│
 └──┴────┴─┴────┴─┴────┴─┴────┴─┴────┴─┴────┴┘
        │         │         │
        ▼         ▼         ▼
 ┌──────────┐ ┌──────────┐ ┌──────────┐
 │ Worker 1 │ │ Worker 2 │ │ Worker 3 │
 │ (P0, P1) │ │ (P2, P3) │ │ (P4, P5) │  <- crash!
 └──────────┘ └──────────┘ └──────────┘
                                ↓ rebalance
 ┌──────────┐ ┌──────────┐ ┌──────────┐
 │ Worker 1 │ │ Worker 2 │ │ Worker 4 │  <- new
 │ (P0,P1)  │ │ (P2,P3)  │ │ (P4,P5)  │
 └──────────┘ └──────────┘ └──────────┘
```

## How to Run

```bash
dotnet run --project src/LogAggregation
```

## What to Expect

1. Embedded broker starts with a 6-partition topic
2. Workers 1-3 start and display their partition assignments
3. After ~8 seconds, Worker 3 crashes and partitions rebalance
4. Worker 4 joins and another rebalance occurs
5. Final summary shows total logs, distribution, and severity counts

## Key Surgewave Features Demonstrated

| Feature | Usage |
|---------|-------|
| **Consumer Groups** | Multiple consumers share partitions |
| **Rebalancing** | Automatic partition redistribution |
| **Fault Tolerance** | Worker crash handled transparently |
| **Elastic Scaling** | Add/remove workers dynamically |
| **Partition Affinity** | Logs partitioned by service name |
