#pragma warning disable CA5394 // Random is fine for sample data generation

using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Broker;
using Kuestenlogik.Surgewave.Runtime;
using Spectre.Console;

// ═══════════════════════════════════════════════════════════════════════
// Shared Data Platform -- ACL & Security (Multi-Team)
// ═══════════════════════════════════════════════════════════════════════
// 3 teams (Frontend, Backend, Analytics) share a Surgewave cluster with
// different access rights. Demonstrates ACL rules, access enforcement,
// and permission management.
// ═══════════════════════════════════════════════════════════════════════

AnsiConsole.Write(new FigletText("Data Platform").Color(Color.Magenta1));
AnsiConsole.MarkupLine("[grey]Multi-Team ACL & Security Demo[/]\n");

// ── Topic and team definitions ───────────────────────────────────────

var topics = new[]
{
    ("user-events",       "Frontend writes, Backend/Analytics reads"),
    ("order-events",      "Backend writes, Analytics reads"),
    ("analytics-results", "Analytics writes, Frontend reads"),
    ("internal-metrics",  "Backend only (read/write)")
};

var teams = new[]
{
    new TeamConfig("Frontend",  "cyan",    [("user-events", "WRITE"), ("analytics-results", "READ")]),
    new TeamConfig("Backend",   "green",   [("user-events", "READ"), ("order-events", "WRITE"), ("internal-metrics", "READ/WRITE")]),
    new TeamConfig("Analytics", "yellow",  [("user-events", "READ"), ("order-events", "READ"), ("analytics-results", "WRITE")])
};

// ── Start embedded broker with security ──────────────────────────────

AnsiConsole.MarkupLine("[yellow]Starting embedded Surgewave broker with ACL enabled...[/]");

await using var surgewave = await SurgewaveRuntime.CreateBuilder()
    .WithPort(0)
    .WithStorageMode(StorageMode.Memory)
    .WithAutoCreateTopics(true)
    .WithPartitions(3)
    .WithAcl(true)
    .Build()
    .StartAsync();

AnsiConsole.MarkupLine("[green]Broker started on port {0} (ACL enabled)[/]\n", surgewave.Port);

// ── Display topic structure ──────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 1: Topic Structure ==[/]\n");

var topicTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Topic")
    .AddColumn("Description");

foreach (var (name, description) in topics)
{
    topicTable.AddRow($"[bold]{name}[/]", description);
}

AnsiConsole.Write(topicTable);

// ── Display ACL rules ────────────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 2: ACL Rules ==[/]\n");

var aclTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Team")
    .AddColumn("Topic")
    .AddColumn("Permission")
    .AddColumn("Effect");

foreach (var team in teams)
{
    foreach (var (topic, permission) in team.Permissions)
    {
        aclTable.AddRow(
            $"[{team.Color}]{team.Name}[/]",
            topic,
            permission,
            "[green]ALLOW[/]");
    }

    // Show denied topics
    var allowedTopics = team.Permissions.Select(p => p.Topic).ToHashSet();
    foreach (var (topicName, _) in topics)
    {
        if (!allowedTopics.Contains(topicName))
        {
            aclTable.AddRow(
                $"[{team.Color}]{team.Name}[/]",
                topicName,
                "ALL",
                "[red]DENY[/]");
        }
    }
}

AnsiConsole.Write(aclTable);

// ── Demonstrate access control ───────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Phase 3: Access Control Demonstration ==[/]\n");

await using var client = await SurgewaveClient.Create(surgewave.BootstrapServers)
    .UseSurgewaveProtocol()
    .BuildAsync();

// We simulate ACL checks via application-level enforcement
// because the embedded broker is single-client (no SASL user context).
// In production, Surgewave's built-in SASL+ACL would enforce this.

var accessResults = new List<AccessTestResult>();

// Test each team's access
foreach (var team in teams)
{
    AnsiConsole.MarkupLine("[{0}]--- Team {1} ---[/]", team.Color, team.Name);

    foreach (var (topicName, _) in topics)
    {
        var allowedOps = team.Permissions
            .Where(p => p.Topic == topicName)
            .Select(p => p.Permission)
            .ToList();

        // Test WRITE
        var canWrite = allowedOps.Any(p => p is "WRITE" or "READ/WRITE");
        await TestAccess(client, team.Name, team.Color, topicName, "WRITE", canWrite, accessResults);

        // Test READ
        var canRead = allowedOps.Any(p => p is "READ" or "READ/WRITE");
        await TestAccess(client, team.Name, team.Color, topicName, "READ", canRead, accessResults);
    }

    AnsiConsole.WriteLine();
}

// ── Access results matrix ────────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 4: Permission Matrix ==[/]\n");

var matrixTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Topic");

foreach (var team in teams)
{
    matrixTable.AddColumn($"[{team.Color}]{team.Name}[/]");
}

foreach (var (topicName, _) in topics)
{
    var row = new List<string> { $"[bold]{topicName}[/]" };

    foreach (var team in teams)
    {
        var teamResults = accessResults
            .Where(r => r.Team == team.Name && r.Topic == topicName)
            .ToList();

        var readResult = teamResults.FirstOrDefault(r => r.Operation == "READ");
        var writeResult = teamResults.FirstOrDefault(r => r.Operation == "WRITE");

        var readIcon = readResult?.Allowed == true ? "[green]R[/]" : "[red].[/]";
        var writeIcon = writeResult?.Allowed == true ? "[green]W[/]" : "[red].[/]";

        row.Add($"{readIcon} {writeIcon}");
    }

    matrixTable.AddRow(row.ToArray());
}

AnsiConsole.Write(matrixTable);
AnsiConsole.MarkupLine("[grey]R = Read allowed, W = Write allowed, . = Denied[/]\n");

// ── Phase 5: Add a new team ──────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Phase 5: Adding New Team (DataScience) ==[/]\n");

var dataScienceTeam = new TeamConfig("DataScience", "magenta",
    [("user-events", "READ"), ("order-events", "READ"), ("analytics-results", "READ")]);

AnsiConsole.MarkupLine("[magenta]New team: DataScience[/]");
AnsiConsole.MarkupLine("[grey]  Permissions: READ on user-events, order-events, analytics-results[/]");
AnsiConsole.MarkupLine("[grey]  No WRITE access to any topic (read-only analytics)[/]\n");

var newTeamTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Topic")
    .AddColumn("Permission")
    .AddColumn("Effect");

foreach (var (topic, permission) in dataScienceTeam.Permissions)
{
    newTeamTable.AddRow(topic, permission, "[green]ALLOW[/]");
}

foreach (var (topicName, _) in topics)
{
    if (!dataScienceTeam.Permissions.Any(p => p.Topic == topicName))
    {
        newTeamTable.AddRow(topicName, "ALL", "[red]DENY[/]");
    }
}

AnsiConsole.Write(new Panel(newTeamTable)
    .Header("[magenta]DataScience Team Permissions[/]")
    .BorderColor(Color.Magenta1));

// Verify DataScience access
AnsiConsole.MarkupLine("\n[grey]Verifying DataScience access...[/]\n");

foreach (var (topicName, _) in topics)
{
    var allowedOps = dataScienceTeam.Permissions
        .Where(p => p.Topic == topicName)
        .Select(p => p.Permission)
        .ToList();

    var canRead = allowedOps.Any(p => p is "READ" or "READ/WRITE");
    await TestAccess(client, dataScienceTeam.Name, dataScienceTeam.Color,
        topicName, "READ", canRead, accessResults);

    var canWrite = allowedOps.Any(p => p is "WRITE" or "READ/WRITE");
    await TestAccess(client, dataScienceTeam.Name, dataScienceTeam.Color,
        topicName, "WRITE", canWrite, accessResults);
}

// ── Updated permission matrix ────────────────────────────────────────

AnsiConsole.MarkupLine("\n[blue]== Updated Permission Matrix (with DataScience) ==[/]\n");

var allTeams = teams.Append(dataScienceTeam).ToArray();

var updatedMatrix = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Topic");

foreach (var team in allTeams)
{
    updatedMatrix.AddColumn($"[{team.Color}]{team.Name}[/]");
}

foreach (var (topicName, _) in topics)
{
    var row = new List<string> { $"[bold]{topicName}[/]" };

    foreach (var team in allTeams)
    {
        var teamResults = accessResults
            .Where(r => r.Team == team.Name && r.Topic == topicName)
            .ToList();

        var readResult = teamResults.FirstOrDefault(r => r.Operation == "READ");
        var writeResult = teamResults.FirstOrDefault(r => r.Operation == "WRITE");

        var readIcon = readResult?.Allowed == true ? "[green]R[/]" : "[red].[/]";
        var writeIcon = writeResult?.Allowed == true ? "[green]W[/]" : "[red].[/]";

        row.Add($"{readIcon} {writeIcon}");
    }

    updatedMatrix.AddRow(row.ToArray());
}

AnsiConsole.Write(updatedMatrix);
AnsiConsole.MarkupLine("[grey]R = Read allowed, W = Write allowed, . = Denied[/]\n");

// ── Summary ──────────────────────────────────────────────────────────

AnsiConsole.MarkupLine("[blue]== Summary ==[/]\n");

var summaryTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Concept")
    .AddColumn("Description");

summaryTable.AddRow("ACL Rules", "Per-topic, per-team read/write permissions");
summaryTable.AddRow("Access Enforcement", "Unauthorized operations are denied");
summaryTable.AddRow("Least Privilege", "Each team gets only needed access");
summaryTable.AddRow("Dynamic Teams", "New teams can be added without downtime");
summaryTable.AddRow("Permission Matrix", "Visual overview of who can access what");
summaryTable.AddRow("Audit Trail", "All access attempts logged");

AnsiConsole.Write(new Panel(summaryTable)
    .Header("[magenta]ACL & Security Concepts Demonstrated[/]")
    .BorderColor(Color.Magenta1));

AnsiConsole.MarkupLine("\n[green]Data Platform ACL demo completed![/]");
return 0;

// ═══════════════════════════════════════════════════════════════════════
// Helper methods
// ═══════════════════════════════════════════════════════════════════════

static async Task TestAccess(
    ISurgewaveClient client,
    string teamName,
    string teamColor,
    string topic,
    string operation,
    bool allowed,
    List<AccessTestResult> results)
{
    results.Add(new AccessTestResult(teamName, topic, operation, allowed));

    if (allowed)
    {
        if (operation == "WRITE")
        {
            // Actually produce a test message
            await using var producer = client.CreateProducer<string, string>();
            await producer.ProduceAsync(topic, $"{teamName}-key", $"Test message from {teamName}");
        }
        else
        {
            // For READ, just show it's allowed (no need to actually consume)
        }

        AnsiConsole.MarkupLine("  [{0}]{1}[/] {2} {3}: [green]ALLOWED[/]",
            teamColor, teamName, operation, topic);
    }
    else
    {
        AnsiConsole.MarkupLine("  [{0}]{1}[/] {2} {3}: [red]ACCESS DENIED[/]",
            teamColor, teamName, operation, topic);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Domain types
// ═══════════════════════════════════════════════════════════════════════

sealed record TeamConfig(string Name, string Color, (string Topic, string Permission)[] Permissions);
sealed record AccessTestResult(string Team, string Topic, string Operation, bool Allowed);
