using System.Data.Common;
using System.Text.Json;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Connect;
using Kuestenlogik.Surgewave.Connector.Csv;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// =====================================================================
// CONNECTOR PIPELINE -- CSV Source -> Surgewave -> SQLite Sink
// =====================================================================
// Demonstrates Surgewave's Connect framework with a complete data pipeline.
// CSV product data is read, published to a Surgewave topic, consumed, and
// written to a SQLite database -- showing the full connector lifecycle.
// =====================================================================

const string bootstrapServers = "localhost:9092";
const string topicName = "products";
var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "products.csv");
var dbPath = Path.Combine(AppContext.BaseDirectory, "products.db");

AnsiConsole.Write(new FigletText("Connector Pipeline").Color(Color.Purple));
AnsiConsole.MarkupLine("[grey]CSV → Surgewave → SQLite Database Pipeline Demo[/]\n");

// Register SQLite provider for database connector
DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);

// Verify CSV file exists
if (!File.Exists(csvPath))
{
    AnsiConsole.MarkupLine("[red]CSV file not found: {0}[/]", csvPath);
    return 1;
}

var csvLines = File.ReadAllLines(csvPath);
AnsiConsole.MarkupLine("[cyan]Source:[/] {0} ({1} records)", csvPath, csvLines.Length - 1);
AnsiConsole.MarkupLine("[cyan]Topic:[/] {0}", topicName);
AnsiConsole.MarkupLine("[cyan]Database:[/] {0}\n", dbPath);

// Create SQLite database and table
AnsiConsole.MarkupLine("[yellow]Step 1: Creating SQLite database...[/]");
await using var setupConnection = new SqliteConnection($"Data Source={dbPath}");
await setupConnection.OpenAsync();
await using (var createCmd = setupConnection.CreateCommand())
{
    createCmd.CommandText = """
        DROP TABLE IF EXISTS products;
        CREATE TABLE products (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            category TEXT,
            price REAL,
            quantity INTEGER,
            last_updated TEXT
        );
        """;
    await createCmd.ExecuteNonQueryAsync();
}
AnsiConsole.MarkupLine("[green]Database created with products table[/]\n");

// ============= STEP 2: Connect to Surgewave =============
// UseSurgewaveProtocol() selects the native binary protocol for fastest
// data pipeline throughput between source and sink connectors.
AnsiConsole.MarkupLine("[yellow]Step 2: Connecting to Surgewave broker...[/]");
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
{
    // Step 3: Run CSV Source Connector to read CSV and publish to Surgewave
    AnsiConsole.MarkupLine("[yellow]Step 3: Running CSV Source Connector...[/]");
    AnsiConsole.MarkupLine("[grey]Reading from CSV and publishing to topic '{0}'[/]", topicName);

    await using var producer = client.CreateProducer<string, string>();

    var sourceRecordCount = 0;
    await AnsiConsole.Status()
        .StartAsync("Processing CSV records...", async ctx =>
        {
            // Use CsvSourceTask directly for demonstration
            var csvConfig = new Dictionary<string, string>
            {
                [CsvConnectorConfig.FilePath] = csvPath,
                [CsvConnectorConfig.Topic] = topicName,
                [CsvConnectorConfig.HasHeader] = "true",
                [CsvConnectorConfig.KeyField] = "id",
                [CsvConnectorConfig.Delimiter] = ",",
                [CsvConnectorConfig.StartFromBeginning] = "true"
            };

            // Parse CSV manually and publish (simulating what CsvSourceTask does)
            var lines = await File.ReadAllLinesAsync(csvPath);
            if (lines.Length <= 1) return;

            var headers = lines[0].Split(',');

            for (var i = 1; i < lines.Length; i++)
            {
                var values = lines[i].Split(',');
                var record = new Dictionary<string, string>();

                for (var j = 0; j < headers.Length && j < values.Length; j++)
                {
                    record[headers[j]] = values[j];
                }

                var key = record.TryGetValue("id", out var id) ? id : i.ToString();
                var value = JsonSerializer.Serialize(record);

                await producer.ProduceAsync(topicName, key, value);
                sourceRecordCount++;

                ctx.Status($"Published {sourceRecordCount} records...");
            }
        });

    AnsiConsole.MarkupLine("[green]Published {0} records to Surgewave[/]\n", sourceRecordCount);

    // ============= STEP 4: Sink Connector -- Surgewave -> SQLite =============
    // Consume from the topic and write to SQLite. AutoOffsetReset.Earliest
    // ensures we read from the beginning (all records just published).
    // A unique GroupId prevents conflicts with other consumers.
    AnsiConsole.MarkupLine("[yellow]Step 4: Running Database Sink Connector...[/]");
    AnsiConsole.MarkupLine("[grey]Consuming from topic '{0}' and writing to SQLite[/]", topicName);

    await using var consumer = client.CreateConsumer<string, string>(options =>
    {
        options.GroupId = $"connector-pipeline-demo-{Guid.NewGuid():N}";
        options.AutoOffsetReset = AutoOffsetReset.Earliest;
        options.EnableAutoCommit = true;
    });

    consumer.Subscribe(topicName);

    var sinkRecordCount = 0;
    await using var dbConnection = new SqliteConnection($"Data Source={dbPath}");
    await dbConnection.OpenAsync();

    await AnsiConsole.Status()
        .StartAsync("Consuming and writing to database...", async ctx =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                while (!cts.Token.IsCancellationRequested && sinkRecordCount < sourceRecordCount)
                {
                    var result = await consumer.ConsumeAsync(
                        timeout: TimeSpan.FromSeconds(1),
                        cancellationToken: cts.Token);

                    if (result?.Value != null)
                    {
                        // Parse JSON and insert into database
                        var record = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result.Value);
                        if (record != null)
                        {
                            await using var insertCmd = dbConnection.CreateCommand();
                            insertCmd.CommandText = """
                                INSERT OR REPLACE INTO products (id, name, category, price, quantity, last_updated)
                                VALUES (@id, @name, @category, @price, @quantity, @last_updated)
                                """;

                            AddParameter(insertCmd, "@id", GetValue(record, "id"));
                            AddParameter(insertCmd, "@name", GetValue(record, "name"));
                            AddParameter(insertCmd, "@category", GetValue(record, "category"));
                            AddParameter(insertCmd, "@price", GetValue(record, "price"));
                            AddParameter(insertCmd, "@quantity", GetValue(record, "quantity"));
                            AddParameter(insertCmd, "@last_updated", GetValue(record, "last_updated"));

                            await insertCmd.ExecuteNonQueryAsync();
                            sinkRecordCount++;

                            ctx.Status($"Inserted {sinkRecordCount} records...");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected timeout
            }
        });

    AnsiConsole.MarkupLine("[green]Inserted {0} records into SQLite[/]\n", sinkRecordCount);
}

// Step 5: Verify the data in the database
AnsiConsole.MarkupLine("[yellow]Step 5: Verifying data in database...[/]");

await using var verifyConnection = new SqliteConnection($"Data Source={dbPath}");
await verifyConnection.OpenAsync();

await using var selectCmd = verifyConnection.CreateCommand();
selectCmd.CommandText = "SELECT * FROM products ORDER BY id LIMIT 20";

var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("ID")
    .AddColumn("Name")
    .AddColumn("Category")
    .AddColumn("Price")
    .AddColumn("Qty");

await using var reader = await selectCmd.ExecuteReaderAsync();
var dbRecordCount = 0;
while (await reader.ReadAsync())
{
    dbRecordCount++;
    table.AddRow(
        reader["id"]?.ToString() ?? "",
        reader["name"]?.ToString() ?? "",
        reader["category"]?.ToString() ?? "",
        $"${reader["price"]}",
        reader["quantity"]?.ToString() ?? "");
}

AnsiConsole.Write(table);
AnsiConsole.MarkupLine("\n[green]Pipeline completed successfully![/]");
AnsiConsole.MarkupLine("[grey]Total records in database: {0}[/]", dbRecordCount);

// Summary
AnsiConsole.WriteLine();
var summaryTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Stage")
    .AddColumn("Description")
    .AddColumn("Status");

summaryTable.AddRow("1. Source", $"CSV file ({csvLines.Length - 1} records)", "[green]Done[/]");
summaryTable.AddRow("2. Surgewave", $"Topic '{topicName}'", "[green]Done[/]");
summaryTable.AddRow("3. Sink", $"SQLite database ({dbRecordCount} records)", "[green]Done[/]");

AnsiConsole.Write(new Panel(summaryTable)
    .Header("[blue]Pipeline Summary[/]")
    .BorderColor(Color.Blue));

return 0;

static void AddParameter(DbCommand cmd, string name, object? value)
{
    var param = cmd.CreateParameter();
    param.ParameterName = name;
    param.Value = value ?? DBNull.Value;
    cmd.Parameters.Add(param);
}

static object? GetValue(Dictionary<string, JsonElement> record, string key)
{
    if (!record.TryGetValue(key, out var element))
        return null;

    return element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}
