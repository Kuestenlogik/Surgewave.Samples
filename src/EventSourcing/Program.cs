using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Samples.EventSourcing;
using Kuestenlogik.Surgewave.Samples.EventSourcing.Events;
using Kuestenlogik.Surgewave.Samples.EventSourcing.Projections;
using Spectre.Console;

// =====================================================================
// EVENT SOURCING -- Bank Account Demo with Event Replay
// =====================================================================
// Demonstrates event sourcing using Surgewave topics as the event store.
// Surgewave topics are naturally append-only, ordered, and replayable --
// the three core requirements of an event store.
// =====================================================================

const string bootstrapServers = "localhost:9092";

AnsiConsole.Write(new FigletText("Event Sourcing").Color(Color.Gold1));
AnsiConsole.MarkupLine("[grey]Bank Account Demo with Event Replay[/]\n");

// ============= STEP 1: Connect to Surgewave =============
// Surgewave's native protocol provides the fastest event store operations.
AnsiConsole.MarkupLine("[yellow]Connecting to Surgewave broker...[/]");

ISurgewaveClient client;
try
{
    client = await SurgewaveClient.Create(bootstrapServers)
        .UseSurgewaveProtocol()
        .BuildAsync();
    AnsiConsole.MarkupLine("[green]Connected to Surgewave at {0}[/]\n", bootstrapServers);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Failed to connect: {0}[/]", ex.Message);
    AnsiConsole.MarkupLine("[grey]Make sure Surgewave broker is running: dotnet run --project src/Kuestenlogik.Surgewave.Broker[/]");
    return 1;
}

await using (client)
await using (var eventStore = new EventStore(client))
{
    // ============= STEP 2: Create Accounts and Perform Transactions =============
    // The EventStore wraps Surgewave's producer/consumer to provide an append-only
    // event log. Each account's events are keyed by accountId, ensuring all
    // events for the same account land in the same partition (preserving order).
    AnsiConsole.MarkupLine("[blue]== Demo 1: Creating Accounts and Transactions ==[/]\n");

    var alice = await BankAccount.OpenAsync(eventStore, "Alice Smith", "Checking", 1000.00m);
    AnsiConsole.MarkupLine("[green]Created account for Alice:[/] {0}", alice.AccountId);

    var bob = await BankAccount.OpenAsync(eventStore, "Bob Johnson", "Savings", 500.00m);
    AnsiConsole.MarkupLine("[green]Created account for Bob:[/] {0}", bob.AccountId);

    // Perform some transactions
    await alice.DepositAsync(250.00m, "Paycheck deposit");
    await alice.WithdrawAsync(100.00m, "ATM withdrawal");
    await alice.DepositAsync(75.50m, "Cash deposit");
    await alice.WithdrawAsync(50.00m, "Online purchase");

    await bob.DepositAsync(1000.00m, "Birthday gift");
    await bob.WithdrawAsync(200.00m, "Rent payment");

    AnsiConsole.MarkupLine("[grey]Performed transactions on both accounts[/]\n");

    // Show current state
    ShowAccountState("Alice", alice.State);
    ShowAccountState("Bob", bob.State);

    // Demo 2: Show Transaction History (Projection)
    AnsiConsole.MarkupLine("\n[blue]== Demo 2: Transaction History Projection ==[/]\n");

    ShowTransactionHistory("Alice", alice.History);

    // ============= STEP 3: Event Replay =============
    // The key benefit of event sourcing: rebuild state from scratch by
    // replaying all events. Surgewave topics retain all messages, so we can
    // consume from the beginning to reconstruct any aggregate's state.
    AnsiConsole.MarkupLine("\n[blue]== Demo 3: Event Replay - Rebuilding State ==[/]\n");

    AnsiConsole.MarkupLine("[yellow]Loading all events and replaying...[/]");

    var allEvents = await eventStore.LoadAllEventsAsync();
    AnsiConsole.MarkupLine("[grey]Loaded {0} events from event store[/]\n", allEvents.Count);

    // Show events timeline
    var eventsTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Seq")
        .AddColumn("Account")
        .AddColumn("Event Type")
        .AddColumn("Details")
        .AddColumn("Timestamp");

    foreach (var evt in allEvents)
    {
        var details = evt switch
        {
            AccountOpened o => $"Opened by {o.HolderName} with ${o.InitialDepositCents / 100m:N2}",
            MoneyDeposited d => $"+${d.AmountCents / 100m:N2} ({d.Description})",
            MoneyWithdrawn w => $"-${w.AmountCents / 100m:N2} ({w.Description})",
            AccountClosed c => $"Closed: {c.Reason}",
            _ => ""
        };

        var eventType = evt.GetType().Name;
        var color = evt switch
        {
            AccountOpened => "green",
            MoneyDeposited => "cyan",
            MoneyWithdrawn => "yellow",
            AccountClosed => "red",
            _ => "white"
        };

        eventsTable.AddRow(
            evt.SequenceNumber.ToString(),
            evt.AccountId[..8] + "...",
            $"[{color}]{eventType}[/]",
            details,
            evt.Timestamp.ToString("HH:mm:ss.fff"));
    }

    AnsiConsole.Write(eventsTable);

    // Demo 4: Rebuild projections from events
    AnsiConsole.MarkupLine("\n[blue]== Demo 4: Rebuilding Projections from Events ==[/]\n");

    // Group events by account
    var eventsByAccount = allEvents.GroupBy(e => e.AccountId);

    foreach (var accountEvents in eventsByAccount)
    {
        var events = accountEvents.OrderBy(e => e.SequenceNumber).ToList();

        // Rebuild state projection
        var rebuiltState = AccountState.FromEvents(events);

        // Rebuild transaction history projection
        var rebuiltHistory = TransactionHistory.FromEvents(events);

        AnsiConsole.MarkupLine("[cyan]Rebuilt state for {0}:[/]", accountEvents.Key);
        AnsiConsole.MarkupLine("  Holder: {0}", rebuiltState.HolderName);
        AnsiConsole.MarkupLine("  Balance: [green]${0:N2}[/]", rebuiltState.Balance);
        AnsiConsole.MarkupLine("  Transactions: {0}", rebuiltHistory.Transactions.Count);
        AnsiConsole.MarkupLine("  Status: {0}", rebuiltState.IsOpen ? "[green]Open[/]" : "[red]Closed[/]");
        AnsiConsole.WriteLine();
    }

    // Demo 5: Time Travel - State at specific point
    AnsiConsole.MarkupLine("[blue]== Demo 5: Time Travel - State at Specific Point ==[/]\n");

    var aliceEvents = allEvents.Where(e => e.AccountId == alice.AccountId).OrderBy(e => e.SequenceNumber).ToList();

    AnsiConsole.MarkupLine("[yellow]Alice's balance over time:[/]");

    var timelineTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("After Event")
        .AddColumn("Balance");

    var runningState = new AccountState();
    foreach (var evt in aliceEvents)
    {
        runningState.Apply(evt);
        var eventName = evt.GetType().Name;
        timelineTable.AddRow(eventName, $"${runningState.Balance:N2}");
    }

    AnsiConsole.Write(timelineTable);

    // Demo 6: Real-time event subscription
    AnsiConsole.MarkupLine("\n[blue]== Demo 6: Summary ==[/]\n");

    var summaryTable = new Table()
        .Border(TableBorder.Double)
        .AddColumn("Concept")
        .AddColumn("Description");

    summaryTable.AddRow("Events", "Immutable facts stored in Surgewave topics");
    summaryTable.AddRow("Event Store", "Surgewave topic as append-only event log");
    summaryTable.AddRow("Projections", "Derived views rebuilt from events");
    summaryTable.AddRow("Replay", "Rebuild state by re-applying all events");
    summaryTable.AddRow("Time Travel", "Query state at any point in history");

    AnsiConsole.Write(new Panel(summaryTable)
        .Header("[blue]Event Sourcing Concepts Demonstrated[/]")
        .BorderColor(Color.Blue));
}

AnsiConsole.MarkupLine("\n[green]Event Sourcing demo completed![/]");
return 0;

static void ShowAccountState(string name, AccountState state)
{
    var panel = new Panel(new Markup(
        $"[bold]Account ID:[/] {state.AccountId}\n" +
        $"[bold]Holder:[/] {state.HolderName}\n" +
        $"[bold]Type:[/] {state.AccountType}\n" +
        $"[bold]Balance:[/] [green]${state.Balance:N2}[/]\n" +
        $"[bold]Status:[/] {(state.IsOpen ? "[green]Open[/]" : "[red]Closed[/]")}"))
        .Header($"[cyan]{name}'s Account[/]")
        .Border(BoxBorder.Rounded);

    AnsiConsole.Write(panel);
}

static void ShowTransactionHistory(string name, TransactionHistory history)
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Date")
        .AddColumn("Type")
        .AddColumn("Amount")
        .AddColumn("Description")
        .AddColumn("Balance");

    foreach (var tx in history.Transactions)
    {
        var typeColor = tx.Type switch
        {
            TransactionType.Deposit => "green",
            TransactionType.Withdrawal => "red",
            TransactionType.Closure => "grey",
            _ => "white"
        };

        var amountStr = tx.Type == TransactionType.Deposit
            ? $"[green]+${tx.Amount:N2}[/]"
            : tx.Type == TransactionType.Withdrawal
                ? $"[red]-${tx.Amount:N2}[/]"
                : "-";

        table.AddRow(
            tx.Timestamp.ToString("MM/dd HH:mm"),
            $"[{typeColor}]{tx.Type}[/]",
            amountStr,
            tx.Description.Length > 20 ? tx.Description[..20] + "..." : tx.Description,
            $"${tx.RunningBalance:N2}");
    }

    AnsiConsole.Write(new Panel(table)
        .Header($"[cyan]{name}'s Transaction History[/]")
        .BorderColor(Color.Cyan1));
}
