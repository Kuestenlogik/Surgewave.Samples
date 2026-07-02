#pragma warning disable CA5394 // Random is fine for sample data generation

using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Core.Storage;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// =====================================================================
// Distributed Transaction (Saga Pattern) -- E-Commerce Order Processing
// =====================================================================
// Orchestrates: Order -> Payment -> Inventory -> Shipping -> Notification
// On failure at any step, compensation (rollback) is triggered in reverse
// order to undo all previously completed steps.
// =====================================================================

AnsiConsole.Write(new FigletText("Order Saga").Color(Color.Green));
AnsiConsole.MarkupLine("[grey]Distributed Transaction with Compensation Demo[/]\n");

// -- Configuration ----------------------------------------------------

const int partitionCount = 3;
var sagaStates = new ConcurrentDictionary<string, SagaState>();
var orderResults = new ConcurrentDictionary<string, string>();

// Simulated service state
var accountBalances = new ConcurrentDictionary<string, decimal>();
accountBalances["CUST-001"] = 5000.00m;
accountBalances["CUST-002"] = 15.00m;   // Will fail payment for expensive order
accountBalances["CUST-003"] = 5000.00m;

var inventoryStock = new ConcurrentDictionary<string, int>();
inventoryStock["PROD-LAPTOP"] = 5;
inventoryStock["PROD-PHONE"] = 0;  // Out of stock -- will trigger compensation
inventoryStock["PROD-TABLET"] = 10;
inventoryStock["PROD-WATCH"] = 20;
inventoryStock["PROD-HEADPHONES"] = 15;

// -- Start embedded broker --------------------------------------------

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageEngine(StorageEngines.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(partitionCount)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0}[/]\n", surgewave.Port);

// -- Create clients ---------------------------------------------------

await using var orchestratorClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var paymentClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var inventoryClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var shippingClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var notificationClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();
await using var producerClient = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol().BuildAsync();

// -- Shared producer for all services ---------------------------------

await using var commandProducer = producerClient.CreateProducer<string, SagaEvent>(options =>
{
    options.ValueSerializer = Serializers.Json<SagaEvent>();
});

// -- Payment Service --------------------------------------------------

using var paymentCts = new CancellationTokenSource();

var paymentTask = Task.Run(async () =>
{
    await using var consumer = paymentClient.CreateConsumer<string, SagaEvent>(options =>
    {
        options.GroupId = "payment-service";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<SagaEvent>();
    });
    consumer.Subscribe("payment-commands");

    try
    {
        while (!paymentCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), paymentCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            await Task.Delay(Random.Shared.Next(50, 150), paymentCts.Token);

            if (evt.EventType == "ReservePayment")
            {
                var balance = accountBalances.GetValueOrDefault(evt.CustomerId, 0m);
                if (balance >= evt.Amount)
                {
                    accountBalances.AddOrUpdate(evt.CustomerId, 0, (_, b) => b - evt.Amount);
                    await commandProducer.ProduceAsync("payment-events", evt.OrderId,
                        evt with { EventType = "PaymentReserved", Description = $"Reserved {evt.Amount:C} from {evt.CustomerId}" });
                }
                else
                {
                    await commandProducer.ProduceAsync("payment-events", evt.OrderId,
                        evt with { EventType = "PaymentFailed", Description = $"Insufficient funds: {balance:C} < {evt.Amount:C}" });
                }
            }
            else if (evt.EventType == "ReleasePayment")
            {
                accountBalances.AddOrUpdate(evt.CustomerId, evt.Amount, (_, b) => b + evt.Amount);
                await commandProducer.ProduceAsync("payment-events", evt.OrderId,
                    evt with { EventType = "PaymentReleased", Description = $"Refunded {evt.Amount:C} to {evt.CustomerId}" });
            }
        }
    }
    catch (OperationCanceledException) { }
}, paymentCts.Token);

// -- Inventory Service ------------------------------------------------

using var inventoryCts = new CancellationTokenSource();

var inventoryTask = Task.Run(async () =>
{
    await using var consumer = inventoryClient.CreateConsumer<string, SagaEvent>(options =>
    {
        options.GroupId = "inventory-service";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<SagaEvent>();
    });
    consumer.Subscribe("inventory-commands");

    try
    {
        while (!inventoryCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), inventoryCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            await Task.Delay(Random.Shared.Next(50, 150), inventoryCts.Token);

            if (evt.EventType == "ReserveInventory")
            {
                var stock = inventoryStock.GetValueOrDefault(evt.ProductId, 0);
                if (stock >= evt.Quantity)
                {
                    inventoryStock.AddOrUpdate(evt.ProductId, 0, (_, s) => s - evt.Quantity);
                    await commandProducer.ProduceAsync("inventory-events", evt.OrderId,
                        evt with { EventType = "InventoryReserved", Description = $"Reserved {evt.Quantity}x {evt.ProductId} (remaining: {stock - evt.Quantity})" });
                }
                else
                {
                    await commandProducer.ProduceAsync("inventory-events", evt.OrderId,
                        evt with { EventType = "InventoryInsufficient", Description = $"Out of stock: {evt.ProductId} has {stock}, need {evt.Quantity}" });
                }
            }
            else if (evt.EventType == "ReleaseInventory")
            {
                inventoryStock.AddOrUpdate(evt.ProductId, evt.Quantity, (_, s) => s + evt.Quantity);
                await commandProducer.ProduceAsync("inventory-events", evt.OrderId,
                    evt with { EventType = "InventoryReleased", Description = $"Returned {evt.Quantity}x {evt.ProductId} to stock" });
            }
        }
    }
    catch (OperationCanceledException) { }
}, inventoryCts.Token);

// -- Shipping Service -------------------------------------------------

using var shippingCts = new CancellationTokenSource();

var shippingTask = Task.Run(async () =>
{
    await using var consumer = shippingClient.CreateConsumer<string, SagaEvent>(options =>
    {
        options.GroupId = "shipping-service";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<SagaEvent>();
    });
    consumer.Subscribe("shipping-commands");

    try
    {
        while (!shippingCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), shippingCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            await Task.Delay(Random.Shared.Next(50, 150), shippingCts.Token);

            if (evt.EventType == "CreateShipment")
            {
                var trackingNumber = $"TRACK-{Random.Shared.Next(100000, 999999)}";
                await commandProducer.ProduceAsync("shipping-events", evt.OrderId,
                    evt with { EventType = "ShipmentCreated", Description = $"Shipment created: {trackingNumber}" });
            }
        }
    }
    catch (OperationCanceledException) { }
}, shippingCts.Token);

// -- Notification Service ---------------------------------------------

using var notificationCts = new CancellationTokenSource();

var notificationTask = Task.Run(async () =>
{
    await using var consumer = notificationClient.CreateConsumer<string, SagaEvent>(options =>
    {
        options.GroupId = "notification-service";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<SagaEvent>();
    });
    consumer.Subscribe("notification-commands");

    try
    {
        while (!notificationCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), notificationCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            await Task.Delay(Random.Shared.Next(30, 80), notificationCts.Token);

            await commandProducer.ProduceAsync("notification-events", evt.OrderId,
                evt with { EventType = "NotificationSent", Description = $"Email sent to {evt.CustomerId}: {evt.Description}" });
        }
    }
    catch (OperationCanceledException) { }
}, notificationCts.Token);

// -- Saga Orchestrator ------------------------------------------------

using var orchestratorCts = new CancellationTokenSource();

var orchestratorTask = Task.Run(async () =>
{
    await using var consumer = orchestratorClient.CreateConsumer<string, SagaEvent>(options =>
    {
        options.GroupId = "saga-orchestrator";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.ValueDeserializer = Serializers.JsonDeserializer<SagaEvent>();
    });
    consumer.Subscribe(["payment-events", "inventory-events", "shipping-events", "notification-events"]);

    try
    {
        while (!orchestratorCts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(TimeSpan.FromMilliseconds(100), orchestratorCts.Token);
            if (result?.Value is null) continue;

            var evt = result.Value;
            var state = sagaStates.GetOrAdd(evt.OrderId, _ => new SagaState(evt.OrderId, "Started", "Started", false));

            switch (evt.EventType)
            {
                // -- Happy path progression --
                case "PaymentReserved":
                    UpdateSaga(evt.OrderId, "Inventory", "PaymentReserved");
                    LogStep(evt.OrderId, "Payment", true, evt.Description);
                    await commandProducer.ProduceAsync("inventory-commands", evt.OrderId,
                        evt with { EventType = "ReserveInventory" });
                    break;

                case "InventoryReserved":
                    UpdateSaga(evt.OrderId, "Shipping", "InventoryReserved");
                    LogStep(evt.OrderId, "Inventory", true, evt.Description);
                    await commandProducer.ProduceAsync("shipping-commands", evt.OrderId,
                        evt with { EventType = "CreateShipment" });
                    break;

                case "ShipmentCreated":
                    UpdateSaga(evt.OrderId, "Notification", "ShipmentCreated");
                    LogStep(evt.OrderId, "Shipping", true, evt.Description);
                    await commandProducer.ProduceAsync("notification-commands", evt.OrderId,
                        evt with { EventType = "SendNotification", Description = $"Order {evt.OrderId} shipped!" });
                    break;

                case "NotificationSent":
                    UpdateSaga(evt.OrderId, "Completed", "COMPLETED");
                    LogStep(evt.OrderId, "Notification", true, evt.Description);
                    orderResults[evt.OrderId] = "COMPLETED";
                    break;

                // -- Failure & compensation --
                case "PaymentFailed":
                    UpdateSaga(evt.OrderId, "Compensating", "CANCELLED", compensating: true);
                    LogStep(evt.OrderId, "Payment", false, evt.Description);
                    await commandProducer.ProduceAsync("notification-commands", evt.OrderId,
                        evt with { EventType = "SendNotification", Description = $"Order {evt.OrderId} cancelled: payment failed" });
                    orderResults[evt.OrderId] = "CANCELLED (Payment Failed)";
                    break;

                case "InventoryInsufficient":
                    UpdateSaga(evt.OrderId, "Compensating", "COMPENSATING", compensating: true);
                    LogStep(evt.OrderId, "Inventory", false, evt.Description);
                    LogCompensation(evt.OrderId, "Releasing payment...");
                    await commandProducer.ProduceAsync("payment-commands", evt.OrderId,
                        evt with { EventType = "ReleasePayment" });
                    break;

                case "PaymentReleased":
                    if (state.CompensationRequired)
                    {
                        LogCompensation(evt.OrderId, evt.Description);
                        await commandProducer.ProduceAsync("notification-commands", evt.OrderId,
                            evt with { EventType = "SendNotification", Description = $"Order {evt.OrderId} cancelled: insufficient inventory, payment refunded" });
                        orderResults[evt.OrderId] = "COMPENSATED (Inventory + Payment Rollback)";
                        UpdateSaga(evt.OrderId, "Compensated", "COMPENSATED");
                    }
                    break;
            }
        }
    }
    catch (OperationCanceledException) { }
}, orchestratorCts.Token);

// Give services time to start and subscribe
await Task.Delay(1000);

// -- Demo Orders ------------------------------------------------------

AnsiConsole.MarkupLine("[blue]== Demo 1: Happy Path (Order #1) ==[/]\n");
AnsiConsole.MarkupLine("[grey]Order #1: Customer CUST-001 orders 1x PROD-LAPTOP for $1,299.99[/]");

await commandProducer.ProduceAsync("payment-commands", "ORD-001",
    new SagaEvent("ORD-001", "ReservePayment", "CUST-001", "PROD-LAPTOP", 1, 1299.99m,
        "Reserve payment for laptop order", DateTimeOffset.UtcNow));
sagaStates["ORD-001"] = new SagaState("ORD-001", "Payment", "Started", false);

await Task.Delay(3000);

AnsiConsole.MarkupLine("\n[blue]== Demo 2: Payment Failure (Order #2) ==[/]\n");
AnsiConsole.MarkupLine("[grey]Order #2: Customer CUST-002 (balance: $15.00) orders 1x PROD-TABLET for $899.99[/]");

await commandProducer.ProduceAsync("payment-commands", "ORD-002",
    new SagaEvent("ORD-002", "ReservePayment", "CUST-002", "PROD-TABLET", 1, 899.99m,
        "Reserve payment for tablet order", DateTimeOffset.UtcNow));
sagaStates["ORD-002"] = new SagaState("ORD-002", "Payment", "Started", false);

await Task.Delay(2000);

AnsiConsole.MarkupLine("\n[blue]== Demo 3: Inventory Failure with Compensation (Order #3) ==[/]\n");
AnsiConsole.MarkupLine("[grey]Order #3: Customer CUST-003 orders 2x PROD-PHONE (out of stock!) for $699.99 each[/]");

await commandProducer.ProduceAsync("payment-commands", "ORD-003",
    new SagaEvent("ORD-003", "ReservePayment", "CUST-003", "PROD-PHONE", 2, 1399.98m,
        "Reserve payment for phone order", DateTimeOffset.UtcNow));
sagaStates["ORD-003"] = new SagaState("ORD-003", "Payment", "Started", false);

await Task.Delay(4000);

AnsiConsole.MarkupLine("\n[blue]== Demo 4: Concurrent Orders (#4 - #8) ==[/]\n");
AnsiConsole.MarkupLine("[grey]Processing 5 orders in parallel...[/]\n");

var concurrentOrders = new[]
{
    new SagaEvent("ORD-004", "ReservePayment", "CUST-001", "PROD-WATCH", 1, 299.99m, "Smartwatch order", DateTimeOffset.UtcNow),
    new SagaEvent("ORD-005", "ReservePayment", "CUST-001", "PROD-HEADPHONES", 2, 179.98m, "Headphones order", DateTimeOffset.UtcNow),
    new SagaEvent("ORD-006", "ReservePayment", "CUST-003", "PROD-TABLET", 1, 899.99m, "Tablet order", DateTimeOffset.UtcNow),
    new SagaEvent("ORD-007", "ReservePayment", "CUST-001", "PROD-LAPTOP", 1, 1299.99m, "Second laptop order", DateTimeOffset.UtcNow),
    new SagaEvent("ORD-008", "ReservePayment", "CUST-003", "PROD-WATCH", 3, 899.97m, "Bulk watch order", DateTimeOffset.UtcNow),
};

foreach (var order in concurrentOrders)
{
    sagaStates[order.OrderId] = new SagaState(order.OrderId, "Payment", "Started", false);
    await commandProducer.ProduceAsync("payment-commands", order.OrderId, order);
}

AnsiConsole.MarkupLine("[grey]All 5 orders submitted, processing...[/]\n");
await Task.Delay(6000);

// -- Shutdown services ------------------------------------------------

await orchestratorCts.CancelAsync();
await paymentCts.CancelAsync();
await inventoryCts.CancelAsync();
await shippingCts.CancelAsync();
await notificationCts.CancelAsync();

try { await orchestratorTask; } catch (OperationCanceledException) { }
try { await paymentTask; } catch (OperationCanceledException) { }
try { await inventoryTask; } catch (OperationCanceledException) { }
try { await shippingTask; } catch (OperationCanceledException) { }
try { await notificationTask; } catch (OperationCanceledException) { }

// -- Final Summary ----------------------------------------------------

AnsiConsole.MarkupLine("\n[blue]== Final Summary ==[/]\n");

// Order results table
var resultsTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Order ID")
    .AddColumn("Customer")
    .AddColumn("Product")
    .AddColumn("Qty")
    .AddColumn(new TableColumn("Amount").RightAligned())
    .AddColumn("Final Status");

var allOrders = new (string OrderId, string CustomerId, string ProductId, int Qty, decimal Amount)[]
{
    ("ORD-001", "CUST-001", "PROD-LAPTOP", 1, 1299.99m),
    ("ORD-002", "CUST-002", "PROD-TABLET", 1, 899.99m),
    ("ORD-003", "CUST-003", "PROD-PHONE", 2, 1399.98m),
    ("ORD-004", "CUST-001", "PROD-WATCH", 1, 299.99m),
    ("ORD-005", "CUST-001", "PROD-HEADPHONES", 2, 179.98m),
    ("ORD-006", "CUST-003", "PROD-TABLET", 1, 899.99m),
    ("ORD-007", "CUST-001", "PROD-LAPTOP", 1, 1299.99m),
    ("ORD-008", "CUST-003", "PROD-WATCH", 3, 899.97m),
};

foreach (var (orderId, customerId, productId, qty, amount) in allOrders)
{
    var status = orderResults.GetValueOrDefault(orderId, "PENDING");
    var statusColor = status.StartsWith("COMPLETED", StringComparison.OrdinalIgnoreCase) ? "green"
        : status.StartsWith("CANCELLED", StringComparison.OrdinalIgnoreCase) ? "red"
        : status.StartsWith("COMPENSATED", StringComparison.OrdinalIgnoreCase) ? "yellow"
        : "grey";

    resultsTable.AddRow(
        orderId,
        customerId,
        productId,
        qty.ToString(),
        $"[green]{amount:C}[/]",
        $"[{statusColor}]{status}[/]");
}

AnsiConsole.Write(new Panel(resultsTable)
    .Header("[cyan]Order Results[/]")
    .BorderColor(Color.Cyan1));

// Saga state breakdown
var completed = orderResults.Values.Count(v => v.StartsWith("COMPLETED", StringComparison.OrdinalIgnoreCase));
var cancelled = orderResults.Values.Count(v => v.StartsWith("CANCELLED", StringComparison.OrdinalIgnoreCase));
var compensated = orderResults.Values.Count(v => v.StartsWith("COMPENSATED", StringComparison.OrdinalIgnoreCase));
var pending = allOrders.Length - orderResults.Count;

var statsTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Metric")
    .AddColumn(new TableColumn("Count").RightAligned());

statsTable.AddRow("Total orders", allOrders.Length.ToString());
statsTable.AddRow("[green]Completed[/]", $"[green]{completed}[/]");
statsTable.AddRow("[red]Cancelled[/]", $"[red]{cancelled}[/]");
statsTable.AddRow("[yellow]Compensated[/]", $"[yellow]{compensated}[/]");
if (pending > 0)
    statsTable.AddRow("[grey]Pending[/]", $"[grey]{pending}[/]");

AnsiConsole.Write(statsTable);

// Final balances
var balanceTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Customer")
    .AddColumn(new TableColumn("Remaining Balance").RightAligned());

foreach (var (customerId, balance) in accountBalances.OrderBy(kv => kv.Key))
{
    balanceTable.AddRow(customerId, $"[green]{balance:C}[/]");
}

AnsiConsole.Write(new Panel(balanceTable)
    .Header("[cyan]Account Balances After Sagas[/]")
    .BorderColor(Color.Cyan1));

// Inventory state
var stockTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Product")
    .AddColumn(new TableColumn("Remaining Stock").RightAligned());

foreach (var (productId, stock) in inventoryStock.OrderBy(kv => kv.Key))
{
    var color = stock == 0 ? "red" : stock < 5 ? "yellow" : "green";
    stockTable.AddRow(productId, $"[{color}]{stock}[/]");
}

AnsiConsole.Write(new Panel(stockTable)
    .Header("[cyan]Inventory After Sagas[/]")
    .BorderColor(Color.Cyan1));

// Concept summary
var conceptTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

conceptTable.AddRow("Saga Orchestrator", "Central coordinator that drives the workflow via commands/events");
conceptTable.AddRow("Compensation", "Reverse operations to undo completed steps on failure");
conceptTable.AddRow("Idempotent Services", "Each service processes commands and emits events independently");
conceptTable.AddRow("Event-Driven", "Services communicate only through Surgewave topics (no direct calls)");
conceptTable.AddRow("Parallel Execution", "Multiple sagas execute concurrently without interference");
conceptTable.AddRow("Eventual Consistency", "System reaches consistent state through compensation chain");

AnsiConsole.Write(new Panel(conceptTable)
    .Header("[blue]Saga Pattern Concepts Demonstrated[/]")
    .BorderColor(Color.Blue));

AnsiConsole.MarkupLine("\n[green]Order Saga demo completed![/]");
return 0;

// =====================================================================
// Helper methods
// =====================================================================

void UpdateSaga(string orderId, string step, string status, bool compensating = false)
{
    sagaStates.AddOrUpdate(orderId,
        new SagaState(orderId, step, status, compensating),
        (_, existing) => new SagaState(orderId, step, status, compensating || existing.CompensationRequired));
}

void LogStep(string orderId, string service, bool success, string description)
{
    var icon = success ? "[green]OK[/]" : "[red]FAIL[/]";
    AnsiConsole.MarkupLine("  {0} [{1}]{2}[/] | {3}: {4}",
        icon,
        success ? "cyan" : "red",
        orderId,
        service,
        description);
}

void LogCompensation(string orderId, string description)
{
    AnsiConsole.MarkupLine("  [yellow]<< COMPENSATE[/] [cyan]{0}[/] | {1}", orderId, description);
}

// =====================================================================
// Domain records
// =====================================================================

sealed record SagaEvent(
    string OrderId,
    string EventType,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal Amount,
    string Description,
    DateTimeOffset Timestamp);

sealed record SagaState(
    string OrderId,
    string Step,
    string Status,
    bool CompensationRequired);
