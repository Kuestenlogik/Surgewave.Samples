# Order Saga -- Distributed Transaction Pattern

Demonstrates the **Saga Orchestrator Pattern** for distributed transactions using Surgewave topics as the communication backbone between microservices.

## Use Case

Microservice architectures cannot use traditional database transactions across service boundaries. This sample demonstrates how Surgewave topics enable the Saga pattern -- coordinating multi-step business processes (payment, inventory, shipping, notification) with automatic compensation on failure.

## Architecture

```
                        +-------------------+
                        |  Saga Orchestrator |
                        |  (Coordinator)     |
                        +--------+----------+
                                 |
          Commands (down)        |        Events (up)
          +----------+-----------+-----------+----------+
          |          |                       |          |
          v          v                       ^          ^
    +---------+ +-----------+          +---------+ +-----------+
    | Payment | | Inventory |          | Shipping| |Notification|
    | Service | | Service   |          | Service | | Service    |
    +---------+ +-----------+          +---------+ +-----------+
```

## Saga Flow (Happy Path)

```
Customer places order
       |
       v
[OrderPlaced] --> payment-commands
       |
       v
  Payment Service: ReservePayment
       |
       v
[PaymentReserved] --> inventory-commands
       |
       v
  Inventory Service: ReserveInventory
       |
       v
[InventoryReserved] --> shipping-commands
       |
       v
  Shipping Service: CreateShipment
       |
       v
[ShipmentCreated] --> notification-commands
       |
       v
  Notification Service: SendNotification
       |
       v
[NotificationSent] --> SAGA COMPLETED
```

## Compensation Flow (on failure)

```
Payment Failed:
  PaymentFailed --> Cancel Order --> Notify Customer
                    (no compensation needed, nothing reserved yet)

Inventory Insufficient:
  InventoryInsufficient --> ReleasePayment --> Notify Customer
                           ^                  ^
                           |                  |
                    Compensation         Final notification
                    (reverse payment)    (order cancelled)
```

## Topics Used

| Topic | Purpose |
|-------|---------|
| `payment-commands` | Commands to the payment service |
| `payment-events` | Events emitted by the payment service |
| `inventory-commands` | Commands to the inventory service |
| `inventory-events` | Events emitted by the inventory service |
| `shipping-commands` | Commands to the shipping service |
| `shipping-events` | Events emitted by the shipping service |
| `notification-commands` | Commands to the notification service |
| `notification-events` | Events emitted by the notification service |

## Demo Scenarios

1. **Happy Path**: Order #1 completes all steps successfully
2. **Payment Failure**: Order #2 -- card declined (insufficient funds)
3. **Inventory Failure + Compensation**: Order #3 -- payment succeeds but inventory is empty, triggers payment rollback
4. **Concurrent Orders**: Orders #4-#8 processed in parallel

## How to Run

```bash
dotnet run --project src/OrderSaga
```

No external dependencies required -- uses an embedded Surgewave broker.

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Embedded Broker | `SurgewaveRuntime.CreateBuilder()` with in-memory storage | Self-contained distributed transaction demo |
| Command/Event Topics | 8 topics: commands and events per service | Clean separation of intent (commands) and facts (events) |
| Consumer Groups | Each service has its own consumer group | Services process independently and scale separately |
| Compensation Events | `ReleasePayment` reverses a successful payment step | Saga pattern requires explicit undo for each completed step |
| Parallel Processing | Orders 4-8 processed concurrently | Multiple sagas execute simultaneously without conflict |
| JSON Serialization | Typed producers/consumers with polymorphic events | Type-safe command and event schemas across services |
| Orchestrator Pattern | Central coordinator drives the saga state machine | Clear ownership of cross-service transaction flow |

## Key Code Highlights

### Saga Step Execution

```csharp
// Orchestrator sends command to payment service
await commandProducer.ProduceAsync("payment-commands", orderId, reservePayment);

// Payment service processes and emits event
await eventProducer.ProduceAsync("payment-events", orderId, paymentReserved);

// Orchestrator receives event and sends next command
await commandProducer.ProduceAsync("inventory-commands", orderId, reserveInventory);
```

### Compensation on Failure

```csharp
// Inventory insufficient -- reverse the payment reservation
await commandProducer.ProduceAsync("payment-commands", orderId, releasePayment);
await commandProducer.ProduceAsync("notification-commands", orderId, notifyFailure);
```

## Key Takeaway

**Surgewave topics provide a natural backbone for the Saga pattern -- commands and events flow between services with automatic recovery, enabling distributed transactions without two-phase commit.**
