# E-Commerce Real-Time Analytics

Online shop real-time analytics: revenue per category, top-seller ranking, and order stream joined with a product catalog.

## Use Case

E-commerce platforms need real-time revenue dashboards that update as orders flow in. This sample demonstrates stream-table joins (enriching orders with product data), real-time aggregation (revenue per category), and running top-seller rankings -- all powered by Surgewave's producer/consumer model with an embedded broker.

## What It Does

- **Product Catalog KTable**: 12 products seeded as a lookup table
- **Order Stream**: 200 randomized orders produced over ~30 seconds
- **Stream-Table Join**: Each order joined with its product by ProductId
- **Windowed Aggregation**: Revenue and order counts aggregated per category
- **Top Sellers**: Ranked by units sold, updated in real time
- **Live Dashboard**: Periodic snapshots showing revenue, rankings, recent orders

## Architecture

```
 ┌──────────────┐      ┌──────────────────┐
 │  Product     │      │  Order           │
 │  Catalog     │      │  Generator       │
 │  (KTable)    │      │  (200 orders)    │
 └──────┬───────┘      └───────┬──────────┘
        │                      │
        │    ┌─────────────────┘
        │    │
        ▼    ▼
 ┌──────────────────────────────────────┐
 │         Join + Aggregate             │
 │  order.ProductId -> product          │
 │  revenue = qty * price              │
 │  group by category                  │
 └──────────────────┬───────────────────┘
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
 ┌───────────┐ ┌──────────┐ ┌───────────┐
 │ Revenue   │ │ Top 5    │ │ Recent    │
 │ per Cat.  │ │ Sellers  │ │ Orders    │
 └───────────┘ └──────────┘ └───────────┘
```

## How to Run

```bash
# Self-contained -- no external broker needed
dotnet run --project src/ECommerceAnalytics
```

## What to Expect

1. Embedded broker starts automatically
2. Product catalog is seeded (12 products across 4 categories)
3. Orders stream in; dashboard updates every 50 orders
4. Final dashboard shows revenue per category, top sellers, recent orders

## Key Surgewave Features Demonstrated

| Feature | Usage |
|---------|-------|
| **Embedded Broker** | `SurgewaveRuntime.CreateBuilder()` for zero-setup |
| **Producer / Consumer** | Typed JSON serialization |
| **Stream-Table Join** | Product lookup by key |
| **Real-Time Aggregation** | Revenue and count per category |
| **Spectre.Console** | Rich tables, panels, progress bars |
