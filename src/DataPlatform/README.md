# Data Platform -- ACL & Security (Multi-Team)

Shared data platform for 3 teams (Frontend, Backend, Analytics) with different access rights. Demonstrates ACL rules, access enforcement, and adding new teams.

## Use Case

Enterprise data platforms must enforce access control when multiple teams share the same messaging infrastructure. This sample demonstrates how Surgewave's ACL system provides per-topic, per-team read/write permissions with least-privilege access, dynamic team provisioning, and audit logging.

## What It Does

- **4 Topics**: user-events, order-events, analytics-results, internal-metrics
- **3 Teams**: Frontend, Backend, Analytics with specific permissions
- **ACL Rules**: Per-topic read/write permissions per team
- **Access Testing**: Each operation tested and reported (ALLOWED / DENIED)
- **Permission Matrix**: Visual table of who can access what
- **Dynamic Teams**: DataScience team added at runtime with read-only access

## Architecture

```
 ┌─────────────────────────────────────────────────┐
 │                Surgewave Broker (ACL)                 │
 │                                                 │
 │  ┌───────────────┐  ┌───────────────┐          │
 │  │ user-events   │  │ order-events  │          │
 │  │ FE:W BE:R AN:R│  │ BE:W AN:R     │          │
 │  └───────────────┘  └───────────────┘          │
 │  ┌───────────────┐  ┌───────────────┐          │
 │  │ analytics-    │  │ internal-     │          │
 │  │ results       │  │ metrics       │          │
 │  │ AN:W FE:R     │  │ BE:RW         │          │
 │  └───────────────┘  └───────────────┘          │
 └─────────────────────────────────────────────────┘
        │           │           │
        ▼           ▼           ▼
 ┌──────────┐ ┌──────────┐ ┌──────────┐
 │ Frontend │ │ Backend  │ │Analytics │
 │ Team     │ │ Team     │ │ Team     │
 └──────────┘ └──────────┘ └──────────┘
```

## Permission Matrix

| Topic | Frontend | Backend | Analytics | DataScience |
|-------|----------|---------|-----------|-------------|
| user-events | W | R | R | R |
| order-events | . | W | R | R |
| analytics-results | R | . | W | R |
| internal-metrics | . | RW | . | . |

## How to Run

```bash
dotnet run --project src/DataPlatform
```

## What to Expect

1. Broker starts with ACL enabled
2. Topic structure and ACL rules displayed
3. Each team's access tested topic by topic (ALLOWED / ACCESS DENIED)
4. Permission matrix shows complete access overview
5. DataScience team added with read-only permissions
6. Updated matrix with all 4 teams

## Key Surgewave Features Demonstrated

| Feature | Usage |
|---------|-------|
| **ACL Authorization** | `WithAcl(true)` on broker builder |
| **Per-Topic Permissions** | Read/Write rules per team per topic |
| **Access Enforcement** | Unauthorized operations denied |
| **Least Privilege** | Each team gets minimum needed access |
| **Dynamic Teams** | Add teams without restart |
| **Audit Logging** | All access attempts logged and reported |
