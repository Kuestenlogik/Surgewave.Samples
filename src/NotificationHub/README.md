# Notification Hub Sample

Multi-channel notification system that routes user events to Email, SMS, and Push channels with priority-based routing and per-user rate limiting.

## Use Case

SaaS platforms must send notifications through multiple channels (email, SMS, push) with different rate limits per channel. This sample demonstrates Surgewave's fan-out pattern -- one user event triggers independent processing on separate channel topics, each with its own consumer and rate limiting policy.

## What It Does

- **Fan-Out Routing**: One user event produces notifications on multiple channel topics
- **Priority Rules**: Configurable rules determine which channels and priorities per event type
- **Rate Limiting**: Per-user SMS (3/hour) and Push (10/hour) limits prevent notification spam
- **Parallel Processors**: Each channel has its own consumer processing independently
- **Delivery Tracking**: Statistics per channel, per user, per priority level

## How to Run

```bash
dotnet run --project src/NotificationHub
```

Self-contained with embedded broker -- no external dependencies needed.

## Fan-Out Architecture

```
                         User Events
                     (login, purchase, ...)
                             |
                             v
                    +------------------+
                    | Routing Engine   |
                    | (rules per type) |
                    +--------+---------+
                             |
              +--------------+--------------+
              |              |              |
              v              v              v
     +--------+--+   +------+----+   +-----+------+
     | Email     |   | SMS       |   | Push       |
     | Topic     |   | Topic     |   | Topic      |
     +--------+--+   +------+----+   +-----+------+
              |              |              |
              v              v              v
     +--------+--+   +------+----+   +-----+------+
     | Email     |   | SMS       |   | Push       |
     | Processor |   | Processor |   | Processor  |
     | 100ms     |   | 200ms     |   | 50ms       |
     | No limit  |   | 3/user/h  |   | 10/user/h  |
     +--------+--+   +------+----+   +-----+------+
              |              |              |
              +--------------+--------------+
                             |
                             v
                    +------------------+
                    | Delivery Status  |
                    | (tracking topic) |
                    +------------------+
```

## Notification Rules

| Event Type         | Email       | SMS         | Push       |
|--------------------|-------------|-------------|------------|
| Login (new device) | MEDIUM      | -           | HIGH       |
| Purchase completed | LOW         | -           | LOW        |
| Password reset     | HIGH        | CRITICAL    | -          |
| Account locked     | CRITICAL    | CRITICAL    | HIGH       |
| Weekly digest      | LOW         | -           | -          |

## Rate Limiting

| Channel | Limit          | Behavior         |
|---------|----------------|------------------|
| Email   | Unlimited      | Always delivered  |
| SMS     | 3 per user/h   | Dropped if exceeded |
| Push    | 10 per user/h  | Dropped if exceeded |

## Prerequisites

- .NET 10 SDK

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Embedded Broker | `SurgewaveRuntime.CreateBuilder()` with in-memory storage | Self-contained demo with zero dependencies |
| Fan-Out Topics | One event routes to Email, SMS, and Push topics | Each channel processes independently at its own rate |
| Consumer Groups | Separate consumer per channel type | Channels scale and fail independently |
| Rate Limiting | Per-user SMS (3/h) and Push (10/h) limits | Prevent notification spam without central coordination |
| Priority Routing | Rules map event types to channels with priority levels | Critical alerts go to SMS; low-priority goes to email only |
| Delivery Tracking | Status topic records delivery outcomes | End-to-end visibility for notification audit |

## Key Code Highlights

### Fan-Out Routing

```csharp
// One user event produces notifications on multiple channel topics
await emailProducer.ProduceAsync("notifications-email", userId, notification);
await smsProducer.ProduceAsync("notifications-sms", userId, notification);
await pushProducer.ProduceAsync("notifications-push", userId, notification);
```

### Per-User Rate Limiting

```csharp
// SMS limited to 3 per user per hour -- excess notifications dropped
var recentCount = userSmsHistory.Count(t => t > oneHourAgo);
if (recentCount >= 3) { /* DROP -- rate limit exceeded */ }
```

## Key Takeaway

**Surgewave topics enable natural fan-out patterns -- one event triggers independent processing across multiple channel topics with per-consumer rate limiting.**
