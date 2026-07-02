#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using System.Diagnostics;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Core.Storage;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// ═══════════════════════════════════════════════════════════════════════
// E-Commerce Real-Time Analytics
// ═══════════════════════════════════════════════════════════════════════
// Demonstrates Streams-style processing: Join orders with product catalog,
// calculate revenue per category, top-seller ranking, orders per minute.
// Uses an embedded Surgewave broker -- no external dependencies.
// ═══════════════════════════════════════════════════════════════════════

AnsiConsole.Write(new FigletText("E-Commerce Analytics").Color(Color.Gold1));
AnsiConsole.MarkupLine("[grey]Real-Time Revenue Dashboard with Product Catalog Join[/]\n");

// ── Records ──────────────────────────────────────────────────────────

var products = new List<Product>
{
    new("P001", "MacBook Pro 14\"",   "Electronics",  2499.00m),
    new("P002", "iPhone 16 Pro",      "Electronics",  1199.00m),
    new("P003", "AirPods Max",        "Electronics",   549.00m),
    new("P004", "Levi's 501 Jeans",   "Clothing",       79.99m),
    new("P005", "Nike Air Max 90",    "Clothing",      129.99m),
    new("P006", "Patagonia Jacket",   "Clothing",      249.00m),
    new("P007", "KitchenAid Mixer",   "Home",          399.99m),
    new("P008", "Dyson V15 Vacuum",   "Home",          749.99m),
    new("P009", "LEGO Technic Set",   "Toys",          179.99m),
    new("P010", "Sony WH-1000XM5",    "Electronics",   349.99m),
    new("P011", "Adidas Ultraboost",  "Clothing",      189.99m),
    new("P012", "Instant Pot Duo",    "Home",           89.99m),
};

var productLookup = products.ToDictionary(p => p.ProductId);
var categories = products.Select(p => p.Category).Distinct().ToList();

// ── Start embedded broker ────────────────────────────────────────────

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageEngine(StorageEngines.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]\n", surgewave.Port);

// ── Connect client ───────────────────────────────────────────────────

await using var client = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

// ── Seed product catalog ─────────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 1: Seeding Product Catalog ==[/]\n");

await using var catalogProducer = client.CreateProducer<string, Product>(options =>
{
    options.ValueSerializer = Serializers.Json<Product>();
});

var catalogTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("ID")
    .AddColumn("Name")
    .AddColumn("Category")
    .AddColumn("Price");

foreach (var product in products)
{
    await catalogProducer.ProduceAsync("products", product.ProductId, product);
    catalogTable.AddRow(
        product.ProductId,
        product.Name,
        $"[cyan]{product.Category}[/]",
        $"[green]{product.Price:C}[/]");
}

AnsiConsole.Write(catalogTable);
AnsiConsole.MarkupLine("[green]Seeded {0} products into KTable[/]\n", products.Count);

// ── Analytics state ──────────────────────────────────────────────────

var revenueByCategory = new ConcurrentDictionary<string, decimal>();
var ordersByCategory = new ConcurrentDictionary<string, int>();
var productSalesCount = new ConcurrentDictionary<string, int>();
var windowStart = Stopwatch.GetTimestamp();
var totalOrdersInWindow = 0;
var totalOrders = 0;
var totalRevenue = 0m;

// ── Generate orders and process ──────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 2: Processing Order Stream ==[/]\n");
AnsiConsole.MarkupLine("[grey]Generating 200 orders over ~30 seconds with real-time analytics...[/]\n");

await using var orderProducer = client.CreateProducer<string, Order>(options =>
{
    options.ValueSerializer = Serializers.Json<Order>();
});

await using var orderConsumer = client.CreateConsumer<string, Order>(options =>
{
    options.GroupId = "analytics-processor";
    options.AutoOffsetReset = AutoOffsetReset.Earliest;
    options.ValueDeserializer = Serializers.JsonDeserializer<Order>();
});

orderConsumer.Subscribe("orders");

// Start consumer in background
var enrichedOrders = new ConcurrentBag<EnrichedOrder>();
using var cts = new CancellationTokenSource();

var consumerTask = Task.Run(async () =>
{
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var result = await orderConsumer.ConsumeAsync(
                TimeSpan.FromMilliseconds(100),
                cts.Token);

            if (result?.Value is null) continue;

            var order = result.Value;

            // Join with product catalog (KTable lookup)
            if (!productLookup.TryGetValue(order.ProductId, out var product)) continue;

            var revenue = order.Quantity * product.Price;
            var enriched = new EnrichedOrder(order, product, revenue);
            enrichedOrders.Add(enriched);

            // Update aggregations
            revenueByCategory.AddOrUpdate(product.Category, revenue, (_, v) => v + revenue);
            ordersByCategory.AddOrUpdate(product.Category, 1, (_, v) => v + 1);
            productSalesCount.AddOrUpdate(product.ProductId, order.Quantity, (_, v) => v + order.Quantity);
            Interlocked.Add(ref totalOrdersInWindow, 1);
            Interlocked.Add(ref totalOrders, 1);
            Interlocked.Exchange(ref totalRevenue, totalRevenue + revenue);
        }
    }
    catch (OperationCanceledException)
    {
        // Expected
    }
}, cts.Token);

// Give consumer time to subscribe
await Task.Delay(500);

// Generate orders with periodic dashboard updates
var random = new Random(42);
var customerNames = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank" };
const int totalOrderCount = 200;
const int dashboardIntervalOrders = 50;
var orderNumber = 0;

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Producing orders...", async ctx =>
    {
        for (var i = 0; i < totalOrderCount; i++)
        {
            orderNumber++;
            var productIdx = random.Next(products.Count);
            var product = products[productIdx];
            var customerId = $"CUST-{customerNames[random.Next(customerNames.Length)]}-{random.Next(1, 20):D3}";

            var order = new Order(
                $"ORD-{orderNumber:D6}",
                product.ProductId,
                random.Next(1, 5),
                customerId,
                DateTimeOffset.UtcNow);

            await orderProducer.ProduceAsync("orders", order.CustomerId, order);
            ctx.Status($"Produced order {orderNumber}/{totalOrderCount}...");

            // Simulate realistic order arrival rate
            await Task.Delay(random.Next(30, 150));

            // Show dashboard every N orders
            if (orderNumber % dashboardIntervalOrders == 0)
            {
                // Wait briefly for consumer to catch up
                await Task.Delay(300);
                AnsiConsole.WriteLine();
                ShowDashboard(orderNumber);
            }
        }
    });

// Wait for consumer to process remaining
await Task.Delay(1000);
await cts.CancelAsync();

try { await consumerTask; }
catch (OperationCanceledException) { }

// ── Final dashboard ──────────────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Final Analytics Dashboard ==[/]\n");

ShowDashboard(totalOrders);
ShowTopSellers();
ShowRecentOrders();

AnsiConsole.MarkupLine("\n[green]E-Commerce Analytics demo completed![/]");
AnsiConsole.MarkupLine("[grey]Processed {0} orders with real-time category aggregation and product join.[/]", totalOrders);
return 0;

// ═══════════════════════════════════════════════════════════════════════
// Helper methods
// ═══════════════════════════════════════════════════════════════════════

void ShowDashboard(int processedOrders)
{
    // Revenue per category
    var revenueTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Category")
        .AddColumn(new TableColumn("Revenue").RightAligned())
        .AddColumn(new TableColumn("Orders").RightAligned())
        .AddColumn(new TableColumn("Avg Order").RightAligned());

    foreach (var category in categories.OrderByDescending(c =>
        revenueByCategory.GetValueOrDefault(c, 0)))
    {
        var rev = revenueByCategory.GetValueOrDefault(category, 0);
        var orders = ordersByCategory.GetValueOrDefault(category, 0);
        var avg = orders > 0 ? rev / orders : 0;

        var color = category switch
        {
            "Electronics" => "cyan",
            "Clothing" => "magenta",
            "Home" => "yellow",
            "Toys" => "green",
            _ => "white"
        };

        revenueTable.AddRow(
            $"[{color}]{category}[/]",
            $"[green]{rev:C}[/]",
            orders.ToString(),
            $"{avg:C}");
    }

    var totalRev = revenueByCategory.Values.Sum();
    revenueTable.AddEmptyRow();
    revenueTable.AddRow("[bold]TOTAL[/]", $"[bold green]{totalRev:C}[/]",
        $"[bold]{processedOrders}[/]",
        processedOrders > 0 ? $"[bold]{totalRev / processedOrders:C}[/]" : "-");

    AnsiConsole.Write(new Panel(revenueTable)
        .Header($"[blue]Revenue Dashboard (after {processedOrders} orders)[/]")
        .BorderColor(Color.Blue));
}

void ShowTopSellers()
{
    var topSellers = productSalesCount
        .OrderByDescending(kv => kv.Value)
        .Take(5)
        .ToList();

    var topTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Rank")
        .AddColumn("Product")
        .AddColumn("Category")
        .AddColumn(new TableColumn("Units Sold").RightAligned())
        .AddColumn(new TableColumn("Revenue").RightAligned());

    var rank = 1;
    foreach (var (productId, unitsSold) in topSellers)
    {
        if (!productLookup.TryGetValue(productId, out var product)) continue;

        var revenue = unitsSold * product.Price;
        var medal = rank switch
        {
            1 => "[gold1]#1[/]",
            2 => "[silver]#2[/]",
            3 => "[orange3]#3[/]",
            _ => $"#{rank}"
        };

        topTable.AddRow(medal, product.Name, product.Category,
            unitsSold.ToString(), $"[green]{revenue:C}[/]");
        rank++;
    }

    AnsiConsole.Write(new Panel(topTable)
        .Header("[yellow]Top 5 Sellers[/]")
        .BorderColor(Color.Yellow));
}

void ShowRecentOrders()
{
    var recentTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Order ID")
        .AddColumn("Customer")
        .AddColumn("Product")
        .AddColumn("Qty")
        .AddColumn(new TableColumn("Revenue").RightAligned());

    foreach (var enriched in enrichedOrders.Take(10))
    {
        recentTable.AddRow(
            enriched.Order.OrderId,
            enriched.Order.CustomerId,
            enriched.Product.Name,
            enriched.Order.Quantity.ToString(),
            $"[green]{enriched.Revenue:C}[/]");
    }

    AnsiConsole.Write(new Panel(recentTable)
        .Header("[cyan]Recent Orders (last 10)[/]")
        .BorderColor(Color.Cyan1));
}

// ═══════════════════════════════════════════════════════════════════════
// Domain records
// ═══════════════════════════════════════════════════════════════════════

sealed record Order(string OrderId, string ProductId, int Quantity, string CustomerId, DateTimeOffset Timestamp);
sealed record Product(string ProductId, string Name, string Category, decimal Price);
sealed record EnrichedOrder(Order Order, Product Product, decimal Revenue);
