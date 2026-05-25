#pragma warning disable CA5394 // Random is not cryptographically secure

using Kuestenlogik.Surgewave.Samples.MultiProtocol;
using Spectre.Console;

// Configuration
const string kafkaBootstrapServers = "localhost:9092";
const string grpcAddress = "http://localhost:5000";
const string topic = "stock-quotes";
var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };

AnsiConsole.Write(new FigletText("Multi-Protocol").Color(Color.Gold1));
AnsiConsole.MarkupLine("[grey]Stock Quote Demo - Same Data, Multiple Protocols[/]\n");

// Initialize protocol demos
var kafkaDemo = new KafkaProtocolDemo(kafkaBootstrapServers, topic);
var nativeDemo = new NativeProtocolDemo(kafkaBootstrapServers, topic);
var grpcDemo = new GrpcProtocolDemo(grpcAddress, topic);

// Display protocol information
AnsiConsole.MarkupLine("[blue]== Available Protocols ==[/]\n");
kafkaDemo.DisplayInfo();
nativeDemo.DisplayInfo();
grpcDemo.DisplayInfo();

// Interactive menu
while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[yellow]Select a demo:[/]")
            .AddChoices(
                "1. Produce via all protocols",
                "2. Consume via all protocols",
                "3. Protocol comparison (roundtrip)",
                "4. Individual protocol test",
                "5. Exit"));

    switch (choice)
    {
        case "1. Produce via all protocols":
            await ProduceViaAllProtocols();
            break;

        case "2. Consume via all protocols":
            await ConsumeViaAllProtocols();
            break;

        case "3. Protocol comparison (roundtrip)":
            await RunProtocolComparison();
            break;

        case "4. Individual protocol test":
            await RunIndividualTest();
            break;

        case "5. Exit":
            AnsiConsole.MarkupLine("[green]Goodbye![/]");
            return 0;
    }

    AnsiConsole.WriteLine();
}

async Task ProduceViaAllProtocols()
{
    const int messagesPerProtocol = 5;

    AnsiConsole.MarkupLine("\n[blue]== Producing Messages via All Protocols ==[/]\n");

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Protocol")
        .AddColumn("Messages")
        .AddColumn("Status");

    await AnsiConsole.Live(table)
        .StartAsync(async ctx =>
        {
            // Kafka
            try
            {
                var quotes = await kafkaDemo.ProduceAsync(symbols, messagesPerProtocol);
                table.AddRow("[cyan]Kafka[/]", quotes.Count.ToString(), "[green]✓ Success[/]");
            }
            catch (Exception ex)
            {
                table.AddRow("[cyan]Kafka[/]", "0", $"[red]✗ {ex.Message}[/]");
            }
            ctx.Refresh();

            // Native
            try
            {
                var quotes = await nativeDemo.ProduceAsync(symbols, messagesPerProtocol);
                table.AddRow("[green]Native[/]", quotes.Count.ToString(), "[green]✓ Success[/]");
            }
            catch (Exception ex)
            {
                table.AddRow("[green]Native[/]", "0", $"[red]✗ {ex.Message}[/]");
            }
            ctx.Refresh();

            // gRPC
            try
            {
                var quotes = await grpcDemo.ProduceAsync(symbols, messagesPerProtocol);
                table.AddRow("[yellow]gRPC[/]", quotes.Count.ToString(), "[green]✓ Success[/]");
            }
            catch (Exception ex)
            {
                table.AddRow("[yellow]gRPC[/]", "0", $"[red]✗ {ex.Message}[/]");
            }
            ctx.Refresh();
        });

    AnsiConsole.MarkupLine("\n[grey]Messages produced to topic: {0}[/]", topic);
}

async Task ConsumeViaAllProtocols()
{
    const int maxMessages = 10;

    AnsiConsole.MarkupLine("\n[blue]== Consuming Messages via All Protocols ==[/]\n");

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Protocol")
        .AddColumn("Symbol")
        .AddColumn("Price")
        .AddColumn("Change")
        .AddColumn("Source Protocol");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    await AnsiConsole.Live(table)
        .StartAsync(async ctx =>
        {
            // Try each protocol
            var protocols = new (string name, Func<int, CancellationToken, Task<List<StockQuote>>> consume)[]
            {
                ("Kafka", kafkaDemo.ConsumeAsync),
                ("Native", nativeDemo.ConsumeAsync),
                ("gRPC", grpcDemo.ConsumeAsync)
            };

            foreach (var (name, consume) in protocols)
            {
                try
                {
                    var quotes = await consume(maxMessages, cts.Token);
                    foreach (var quote in quotes.Take(3)) // Show first 3 per protocol
                    {
                        var changeColor = quote.Change >= 0 ? "green" : "red";
                        var changeSign = quote.Change >= 0 ? "+" : "";

                        table.AddRow(
                            $"[cyan]{name}[/]",
                            quote.Symbol,
                            $"${quote.Price:N2}",
                            $"[{changeColor}]{changeSign}{quote.Change:N2} ({changeSign}{quote.ChangePercent:N2}%)[/]",
                            $"[grey]{quote.Protocol}[/]");
                    }

                    if (quotes.Count > 3)
                    {
                        table.AddRow($"[cyan]{name}[/]", $"[grey]... and {quotes.Count - 3} more[/]", "", "", "");
                    }
                    else if (quotes.Count == 0)
                    {
                        table.AddRow($"[cyan]{name}[/]", "[grey]No messages found[/]", "", "", "");
                    }
                }
                catch (Exception ex)
                {
                    table.AddRow($"[cyan]{name}[/]", $"[red]Error: {ex.Message}[/]", "", "", "");
                }

                ctx.Refresh();
            }
        });
}

async Task RunProtocolComparison()
{
    const int messageCount = 100;

    AnsiConsole.MarkupLine("\n[blue]== Protocol Comparison (Roundtrip) ==[/]\n");
    AnsiConsole.MarkupLine("[grey]Sending {0} messages through each protocol and measuring roundtrip time...[/]\n", messageCount);

    var results = new List<(string protocol, int produced, int consumed, TimeSpan time)>();

    await AnsiConsole.Status()
        .StartAsync("Running Kafka roundtrip...", async ctx =>
        {
            try
            {
                var (produced, consumed, time) = await kafkaDemo.RunRoundtripAsync(symbols, messageCount);
                results.Add(("Kafka", produced, consumed, time));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Kafka failed: {0}[/]", ex.Message);
            }

            ctx.Status("Running Native roundtrip...");
            try
            {
                var (produced, consumed, time) = await nativeDemo.RunRoundtripAsync(symbols, messageCount);
                results.Add(("Native", produced, consumed, time));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]Native failed: {0}[/]", ex.Message);
            }

            ctx.Status("Running gRPC roundtrip...");
            try
            {
                var (produced, consumed, time) = await grpcDemo.RunRoundtripAsync(symbols, messageCount);
                results.Add(("gRPC", produced, consumed, time));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]gRPC failed: {0}[/]", ex.Message);
            }
        });

    if (results.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]No protocols completed successfully. Is Surgewave broker running?[/]");
        return;
    }

    // Display results
    var table = new Table()
        .Border(TableBorder.Double)
        .AddColumn("Protocol")
        .AddColumn("Produced")
        .AddColumn("Consumed")
        .AddColumn("Roundtrip Time")
        .AddColumn("Throughput (msg/s)");

    var fastestProtocol = results.OrderBy(r => r.time).First().protocol;

    foreach (var (protocol, produced, consumed, time) in results.OrderBy(r => r.time))
    {
        var throughput = produced / time.TotalSeconds;
        var isFastest = protocol == fastestProtocol;
        var mark = isFastest ? " [green]★[/]" : "";

        table.AddRow(
            $"[cyan]{protocol}[/]{mark}",
            produced.ToString(),
            consumed.ToString(),
            $"{time.TotalMilliseconds:N0} ms",
            $"{throughput:N0}");
    }

    AnsiConsole.Write(table);

    AnsiConsole.MarkupLine("\n[green]★ {0} was the fastest protocol![/]", fastestProtocol);
}

async Task RunIndividualTest()
{
    var protocol = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[yellow]Select protocol to test:[/]")
            .AddChoices("Kafka", "Native", "gRPC", "Back"));

    if (protocol == "Back") return;

    var action = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[yellow]Select action:[/]")
            .AddChoices("Produce", "Consume", "Roundtrip", "Back"));

    if (action == "Back") return;

    var count = AnsiConsole.Ask("How many messages?", 10);

    AnsiConsole.MarkupLine("\n[blue]Running {0} {1} with {2} messages...[/]\n", protocol, action, count);

    try
    {
        switch ((protocol, action))
        {
            case ("Kafka", "Produce"):
                await kafkaDemo.ProduceAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]Produced {0} messages via Kafka[/]", count);
                break;

            case ("Native", "Produce"):
                await nativeDemo.ProduceAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]Produced {0} messages via Native[/]", count);
                break;

            case ("gRPC", "Produce"):
                await grpcDemo.ProduceAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]Produced {0} messages via gRPC[/]", count);
                break;

            case ("Kafka", "Consume"):
                using (var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var quotes = await kafkaDemo.ConsumeAsync(count, cts1.Token);
                    AnsiConsole.MarkupLine("[green]Consumed {0} messages via Kafka[/]", quotes.Count);
                }
                break;

            case ("Native", "Consume"):
                using (var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var quotes = await nativeDemo.ConsumeAsync(count, cts2.Token);
                    AnsiConsole.MarkupLine("[green]Consumed {0} messages via Native[/]", quotes.Count);
                }
                break;

            case ("gRPC", "Consume"):
                using (var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var quotes = await grpcDemo.ConsumeAsync(count, cts3.Token);
                    AnsiConsole.MarkupLine("[green]Consumed {0} messages via gRPC[/]", quotes.Count);
                }
                break;

            case ("Kafka", "Roundtrip"):
                var (kp, kc, kt) = await kafkaDemo.RunRoundtripAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]Kafka: Produced {0}, Consumed {1} in {2:N0}ms[/]", kp, kc, kt.TotalMilliseconds);
                break;

            case ("Native", "Roundtrip"):
                var (np, nc, nt) = await nativeDemo.RunRoundtripAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]Native: Produced {0}, Consumed {1} in {2:N0}ms[/]", np, nc, nt.TotalMilliseconds);
                break;

            case ("gRPC", "Roundtrip"):
                var (gp, gc, gt) = await grpcDemo.RunRoundtripAsync(symbols, count);
                AnsiConsole.MarkupLine("[green]gRPC: Produced {0}, Consumed {1} in {2:N0}ms[/]", gp, gc, gt.TotalMilliseconds);
                break;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine("[red]Error: {0}[/]", ex.Message);
    }
}
