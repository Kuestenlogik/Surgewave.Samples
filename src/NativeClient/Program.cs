using System.Diagnostics;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.NativeClient;
using Spectre.Console;

// Configuration
const string bootstrapServers = "localhost:9092";
const string topicName = "orders";
const int messageCount = 100;

AnsiConsole.Write(new FigletText("Surgewave Native Client").Color(Color.Blue));
AnsiConsole.MarkupLine("[grey]Demonstrating Kuestenlogik.Surgewave.Client native API[/]\n");

// Create the Surgewave client using native protocol
AnsiConsole.MarkupLine("[yellow]Connecting to Surgewave broker...[/]");

ISurgewaveClient client;
try
{
    client = await SurgewaveClient.Create(bootstrapServers)
        .UseSurgewaveProtocol()
        .BuildAsync();

    AnsiConsole.MarkupLine("[green]Connected to Surgewave broker at {0}[/]\n", bootstrapServers);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Failed to connect: {0}[/]", ex.Message);
    AnsiConsole.MarkupLine("[grey]Make sure Surgewave broker is running: dotnet run --project src/Kuestenlogik.Surgewave.Broker[/]");
    return 1;
}

await using (client)
{
    // Main menu loop
    while (true)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to [green]demo[/]?")
                .AddChoices(
                    "Produce messages",
                    "Consume messages",
                    "Produce & Consume (roundtrip)",
                    "Benchmark throughput",
                    "Benchmark latency",
                    "Exit"));

        switch (choice)
        {
            case "Produce messages":
                await ProduceMessagesAsync(client);
                break;
            case "Consume messages":
                await ConsumeMessagesAsync(client);
                break;
            case "Produce & Consume (roundtrip)":
                await ProduceAndConsumeAsync(client);
                break;
            case "Benchmark throughput":
                await BenchmarkThroughputAsync(client);
                break;
            case "Benchmark latency":
                await BenchmarkLatencyAsync(client);
                break;
            case "Exit":
                AnsiConsole.MarkupLine("[grey]Goodbye![/]");
                return 0;
        }

        AnsiConsole.WriteLine();
    }
}

async Task ProduceMessagesAsync(ISurgewaveClient surgewaveClient)
{
    AnsiConsole.MarkupLine("\n[blue]== Producer Demo ==[/]");

    // Create a typed producer with JSON serialization
    await using var producer = surgewaveClient.CreateProducer<string, OrderEvent>(options =>
    {
        options.ValueSerializer = Serializers.Json<OrderEvent>();
    });

    var table = new Table()
        .AddColumn("Order ID")
        .AddColumn("Customer")
        .AddColumn("Product")
        .AddColumn("Quantity")
        .AddColumn("Status")
        .Border(TableBorder.Rounded);

    await AnsiConsole.Live(table)
        .StartAsync(async ctx =>
        {
            for (var i = 1; i <= 10; i++)
            {
                var order = CreateSampleOrder(i);

                // Produce the message
                await producer.ProduceAsync(topicName, order.CustomerId, order);

                table.AddRow(
                    order.OrderId,
                    order.CustomerId,
                    order.ProductId,
                    order.Quantity.ToString(),
                    $"[green]{order.Status}[/]");

                ctx.Refresh();
                await Task.Delay(200);
            }
        });

    AnsiConsole.MarkupLine("[green]Produced 10 messages to topic '{0}'[/]", topicName);
}

async Task ConsumeMessagesAsync(ISurgewaveClient surgewaveClient)
{
    AnsiConsole.MarkupLine("\n[blue]== Consumer Demo ==[/]");
    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop consuming[/]\n");

    // Create a typed consumer with JSON deserialization
    await using var consumer = surgewaveClient.CreateConsumer<string, OrderEvent>(options =>
    {
        options.GroupId = $"native-client-demo-{Guid.NewGuid():N}";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.EnableAutoCommit = true;
        options.ValueDeserializer = Serializers.JsonDeserializer<OrderEvent>();
    });

    consumer.Subscribe(topicName);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var count = 0;
    var table = new Table()
        .AddColumn("Offset")
        .AddColumn("Key")
        .AddColumn("Order ID")
        .AddColumn("Customer")
        .AddColumn("Status")
        .AddColumn("Timestamp")
        .Border(TableBorder.Rounded);

    AnsiConsole.Write(table);

    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(
                timeout: TimeSpan.FromSeconds(1),
                cancellationToken: cts.Token);

            if (result?.Value != null)
            {
                count++;
                AnsiConsole.MarkupLine(
                    "[grey]{0,6}[/] | [cyan]{1,-12}[/] | {2,-10} | {3,-10} | [green]{4,-10}[/] | {5}",
                    result.Offset,
                    result.Key ?? "(null)",
                    result.Value.OrderId,
                    result.Value.CustomerId,
                    result.Value.Status,
                    result.Value.Timestamp.ToString("HH:mm:ss.fff"));
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected
    }

    AnsiConsole.MarkupLine("\n[green]Consumed {0} messages from topic '{1}'[/]", count, topicName);
}

async Task ProduceAndConsumeAsync(ISurgewaveClient surgewaveClient)
{
    AnsiConsole.MarkupLine("\n[blue]== Roundtrip Demo ==[/]");

    // Create producer
    await using var producer = surgewaveClient.CreateProducer<string, OrderEvent>(options =>
    {
        options.ValueSerializer = Serializers.Json<OrderEvent>();
    });

    // Create consumer with unique group to see all messages
    await using var consumer = surgewaveClient.CreateConsumer<string, OrderEvent>(options =>
    {
        options.GroupId = $"roundtrip-demo-{Guid.NewGuid():N}";
        options.AutoOffsetReset = AutoOffsetReset.Latest;
        options.EnableAutoCommit = true;
        options.ValueDeserializer = Serializers.JsonDeserializer<OrderEvent>();
    });

    consumer.Subscribe(topicName);

    // Small delay for subscription to be ready
    await Task.Delay(500);

    var sentOrders = new List<OrderEvent>();
    var receivedOrders = new List<OrderEvent>();

    // Produce messages
    AnsiConsole.MarkupLine("[yellow]Producing 5 messages...[/]");
    for (var i = 1; i <= 5; i++)
    {
        var order = CreateSampleOrder(i);
        await producer.ProduceAsync(topicName, order.CustomerId, order);
        sentOrders.Add(order);
        AnsiConsole.MarkupLine("  [grey]Sent:[/] {0}", order.OrderId);
    }

    // Consume messages
    AnsiConsole.MarkupLine("\n[yellow]Consuming messages...[/]");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    try
    {
        while (receivedOrders.Count < 5 && !cts.Token.IsCancellationRequested)
        {
            var result = await consumer.ConsumeAsync(
                timeout: TimeSpan.FromSeconds(1),
                cancellationToken: cts.Token);

            if (result?.Value != null)
            {
                receivedOrders.Add(result.Value);
                AnsiConsole.MarkupLine("  [grey]Received:[/] {0} at offset {1}", result.Value.OrderId, result.Offset);
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Timeout
    }

    // Summary
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]Sent: {0} | Received: {1}[/]", sentOrders.Count, receivedOrders.Count);
}

async Task BenchmarkThroughputAsync(ISurgewaveClient surgewaveClient)
{
    AnsiConsole.MarkupLine("\n[blue]== Throughput Benchmark ==[/]");

    await using var producer = surgewaveClient.CreateProducer<string, OrderEvent>(options =>
    {
        options.ValueSerializer = Serializers.Json<OrderEvent>();
    });

    var order = CreateSampleOrder(1);
    var sw = Stopwatch.StartNew();

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask($"[green]Producing {messageCount:N0} messages[/]", maxValue: messageCount);

            for (var i = 0; i < messageCount; i++)
            {
                await producer.ProduceAsync(topicName, order.CustomerId, order);
                task.Increment(1);
            }
        });

    sw.Stop();

    var messagesPerSecond = messageCount / sw.Elapsed.TotalSeconds;
    var table = new Table()
        .AddColumn("Metric")
        .AddColumn("Value")
        .Border(TableBorder.Rounded);

    table.AddRow("Messages", messageCount.ToString("N0"));
    table.AddRow("Duration", $"{sw.Elapsed.TotalMilliseconds:N2} ms");
    table.AddRow("Throughput", $"[green]{messagesPerSecond:N0} msg/s[/]");
    table.AddRow("Avg Latency", $"{sw.Elapsed.TotalMilliseconds / messageCount:N3} ms/msg");

    AnsiConsole.Write(table);
}

async Task BenchmarkLatencyAsync(ISurgewaveClient surgewaveClient)
{
    AnsiConsole.MarkupLine("\n[blue]== Latency Benchmark ==[/]");

    await using var producer = surgewaveClient.CreateProducer<string, OrderEvent>(options =>
    {
        options.ValueSerializer = Serializers.Json<OrderEvent>();
    });

    var order = CreateSampleOrder(1);
    var latencies = new List<double>();

    await AnsiConsole.Progress()
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Measuring latencies[/]", maxValue: 100);

            for (var i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                await producer.ProduceAsync(topicName, order.CustomerId, order);
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMicroseconds);
                task.Increment(1);
            }
        });

    latencies.Sort();

    var table = new Table()
        .AddColumn("Percentile")
        .AddColumn("Latency")
        .Border(TableBorder.Rounded);

    table.AddRow("Min", $"{latencies[0]:N0} µs");
    table.AddRow("P50", $"{latencies[49]:N0} µs");
    table.AddRow("P90", $"{latencies[89]:N0} µs");
    table.AddRow("P99", $"{latencies[98]:N0} µs");
    table.AddRow("Max", $"{latencies[99]:N0} µs");
    table.AddRow("Avg", $"[green]{latencies.Average():N0} µs[/]");

    AnsiConsole.Write(table);
}

#pragma warning disable CA5394 // Random is fine for sample data generation
static OrderEvent CreateSampleOrder(int sequence)
{
    var random = new Random(sequence);
    var products = new[] { "LAPTOP", "PHONE", "TABLET", "WATCH", "HEADPHONES" };
    var statuses = new[] { "Created", "Confirmed", "Shipped", "Delivered" };

    return new OrderEvent
    {
        OrderId = $"ORD-{sequence:D6}",
        CustomerId = $"CUST-{random.Next(1, 100):D4}",
        ProductId = products[random.Next(products.Length)],
        Quantity = random.Next(1, 5),
        TotalCents = random.Next(1000, 100000),
        Status = statuses[random.Next(statuses.Length)],
        Timestamp = DateTimeOffset.UtcNow
    };
}
