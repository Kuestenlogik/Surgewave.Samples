# Event Sourcing Sample

Bank account demonstration with event store, projections (balance, transaction history), replay, and time travel.

## Use Case

Financial systems require complete audit trails and the ability to reconstruct state at any point in time. This sample demonstrates how Surgewave topics serve as a natural event store -- append-only, ordered, and replayable -- enabling event sourcing patterns without a separate event store database.

## What It Does

- **Event Store**: Append-only event log backed by Surgewave topic
- **Aggregates**: Bank accounts with business logic
- **Projections**: Account state and transaction history views
- **Event Replay**: Rebuild state from events
- **Time Travel**: Query state at any point in history

## How to Run

```bash
# Start Surgewave broker
dotnet run --project src/Kuestenlogik.Surgewave.Broker

# Run the sample
dotnet run --project samples/EventSourcing
```

The demo will:
1. Create two bank accounts (Alice and Bob)
2. Perform deposits and withdrawals
3. Show current state projections
4. Replay all events to rebuild state
5. Demonstrate time-travel queries

## Why Surgewave for Event Sourcing?

### Perfect Event Store Properties

| Property | Requirement | Surgewave |
|----------|-------------|-------|
| Append-Only | Events never modified | **Topics are immutable** |
| Ordered | Events in sequence | **Partition ordering** |
| Durable | Never lose events | **Replication + Tiering** |
| Replayable | Read from any point | **Offset-based seeks** |
| Scalable | Handle high volume | **1M+ events/sec** |

### Event Sourcing Architecture

```
┌─────────────────────────────────────────────────────┐
│                     Commands                         │
│  OpenAccount, Deposit, Withdraw, CloseAccount       │
└─────────────────────┬───────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│                   Aggregate                          │
│               (Business Logic)                       │
│  • Validate commands                                │
│  • Generate events                                  │
└─────────────────────┬───────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────┐
│                  Surgewave Topic                         │
│              (Event Store)                          │
│  ┌─────────────────────────────────────────────┐   │
│  │ AccountOpened │ MoneyDeposited │ MoneyWith… │   │
│  │    (seq=1)    │    (seq=2)     │   (seq=3)  │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────┬───────────────────────────────┘
                      │
        ┌─────────────┴─────────────┐
        ▼                           ▼
┌───────────────┐           ┌───────────────┐
│  Projection   │           │  Projection   │
│ AccountState  │           │  TxHistory    │
│  (Balance)    │           │  (Ledger)     │
└───────────────┘           └───────────────┘
```

### Key Benefits

| Feature | How Surgewave Enables It |
|---------|---------------------|
| **Immutable Log** | Topics are append-only by design |
| **Event Ordering** | Partition key ensures aggregate ordering |
| **Infinite Retention** | Tiered storage (S3, Azure, GCS) |
| **Parallel Projections** | Multiple consumers read same events |
| **Exactly-Once** | Transactional producers for consistency |

### Comparison with Event Store Options

| Solution | Performance | Scalability | Operations | Cost |
|----------|-------------|-------------|------------|------|
| EventStoreDB | Good | Medium | Separate | $$ |
| PostgreSQL | Medium | Limited | Complex | $ |
| DynamoDB | Good | High | AWS only | $$$ |
| MongoDB | Medium | High | Separate | $$ |
| **Surgewave** | **Excellent** | **Very High** | **Simple** | **$** |

### Time Travel Queries

Surgewave's offset-based storage enables powerful temporal queries:

```csharp
// Query balance at specific point
var events = await eventStore.LoadEventsAsync(accountId);
var stateAtTime = events
    .Where(e => e.Timestamp <= targetTime)
    .Aggregate(new AccountState(), (s, e) => s.Apply(e));

// Or seek to specific offset
consumer.Seek(new TopicPartitionOffset(topic, partition, targetOffset));
```

### Event Schema (Polymorphic JSON)

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "eventType")]
[JsonDerivedType(typeof(AccountOpened), "AccountOpened")]
[JsonDerivedType(typeof(MoneyDeposited), "MoneyDeposited")]
[JsonDerivedType(typeof(MoneyWithdrawn), "MoneyWithdrawn")]
[JsonDerivedType(typeof(AccountClosed), "AccountClosed")]
public abstract record AccountEvent
{
    public required string EventId { get; init; }
    public required string AccountId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required long SequenceNumber { get; init; }
}
```

### Production Patterns

| Pattern | Implementation |
|---------|----------------|
| **Snapshotting** | Store periodic snapshots, replay from snapshot |
| **Projections** | Separate consumer groups for each view |
| **Upcasting** | Transform old event versions on read |
| **Tombstones** | Special "deleted" events for GDPR |
| **Saga/Process Manager** | Coordinate across aggregates |

### Scaling Strategy

```
┌─────────────────────────────────────────────────────┐
│            Topic: account-events                     │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐            │
│  │Partition 0│ │Partition 1│ │Partition 2│           │
│  │ Accts A-H │ │ Accts I-P │ │ Accts Q-Z │           │
│  └──────────┘ └──────────┘ └──────────┘            │
└─────────────────────────────────────────────────────┘

• Partition by account ID (aggregate ID)
• Events for same account always in same partition
• Ordering guaranteed within partition
• Scale by adding partitions
```

### Audit & Compliance

| Requirement | How Surgewave Helps |
|-------------|-----------------|
| Complete History | All events retained |
| Tamper-Proof | Immutable log |
| Point-in-Time | Time travel queries |
| Regulatory | Tiered storage with retention policies |

## Key Takeaway

**Surgewave provides a natural event store with built-in ordering, immutability, replay, and time travel - no separate event sourcing infrastructure needed.**
