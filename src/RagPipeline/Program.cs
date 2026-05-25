#pragma warning disable CA2000 // Dispose objects before losing scope

using Kuestenlogik.Surgewave.Samples.RagPipeline;
using Spectre.Console;

// Configuration
const string qdrantHost = "localhost";
const int qdrantPort = 6334;
const string collectionName = "rag-demo";
const string embeddingModel = "text-embedding-3-small";
const int vectorSize = 1536;

// Check for test mode
var testMode = args.Length > 0 && args[0] == "--test";

AnsiConsole.Write(new FigletText("RAG Pipeline").Color(Color.Gold1));
AnsiConsole.MarkupLine("[grey]Documents → Embeddings → Vector DB → Semantic Search[/]\n");

if (testMode)
{
    AnsiConsole.MarkupLine("[cyan]Running in TEST MODE[/]\n");
}

// Check for OpenAI API key
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    AnsiConsole.MarkupLine("[red]Error: OPENAI_API_KEY environment variable not set.[/]");
    AnsiConsole.MarkupLine("[grey]Please set your OpenAI API key:[/]");
    AnsiConsole.MarkupLine("[grey]  Windows: set OPENAI_API_KEY=sk-...[/]");
    AnsiConsole.MarkupLine("[grey]  Linux/Mac: export OPENAI_API_KEY=sk-...[/]");
    return 1;
}

// Initialize services
AnsiConsole.MarkupLine("[yellow]Initializing RAG pipeline...[/]");
AnsiConsole.MarkupLine("[grey]  Embedding model: {0}[/]", embeddingModel);
AnsiConsole.MarkupLine("[grey]  Vector store: Qdrant at {0}:{1}[/]", qdrantHost, qdrantPort);
AnsiConsole.MarkupLine("[grey]  Collection: {0}[/]\n", collectionName);

EmbeddingService embeddingService;
VectorStore vectorStore;
RagPipeline pipeline;

try
{
    embeddingService = new EmbeddingService(model: embeddingModel);
    vectorStore = new VectorStore(qdrantHost, qdrantPort, collectionName, vectorSize);
    pipeline = new RagPipeline(embeddingService, vectorStore);

    // Test connection
    await vectorStore.EnsureCollectionAsync();
    AnsiConsole.MarkupLine("[green]Connected to Qdrant successfully![/]\n");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine("[red]Failed to initialize: {0}[/]", ex.Message);
    AnsiConsole.MarkupLine("\n[grey]Make sure Qdrant is running:[/]");
    AnsiConsole.MarkupLine("[grey]  docker run -p 6334:6334 -p 6333:6333 qdrant/qdrant[/]");
    return 1;
}

await using (pipeline)
{
    if (testMode)
    {
        // Run automated test
        return await RunAutomatedTest();
    }

    // Show interactive menu
    while (true)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select an action:[/]")
                .AddChoices(
                    "1. Load sample documents",
                    "2. Semantic search",
                    "3. Ask a question (RAG)",
                    "4. List stored documents",
                    "5. View statistics",
                    "6. Reset collection",
                    "7. Exit"));

        switch (choice)
        {
            case "1. Load sample documents":
                await LoadSampleDocuments();
                break;

            case "2. Semantic search":
                await SemanticSearch();
                break;

            case "3. Ask a question (RAG)":
                await AskQuestion();
                break;

            case "4. List stored documents":
                await ListDocuments();
                break;

            case "5. View statistics":
                await ViewStatistics();
                break;

            case "6. Reset collection":
                await ResetCollection();
                break;

            case "7. Exit":
                AnsiConsole.MarkupLine("[green]Goodbye![/]");
                return 0;
        }

        AnsiConsole.WriteLine();
    }
}

async Task<int> RunAutomatedTest()
{
    AnsiConsole.MarkupLine("[blue]== Step 1: Reset Collection ==[/]");
    await pipeline.ResetAsync();
    AnsiConsole.MarkupLine("[green]✓ Collection reset[/]\n");

    AnsiConsole.MarkupLine("[blue]== Step 2: Load Sample Documents ==[/]");
    var documents = SampleDocuments.GetTechDocuments();
    AnsiConsole.MarkupLine("Loading {0} documents...", documents.Count);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    await pipeline.IngestDocumentsAsync(documents);
    sw.Stop();

    AnsiConsole.MarkupLine("[green]✓ Loaded {0} documents in {1:N1}s[/]\n", documents.Count, sw.Elapsed.TotalSeconds);

    AnsiConsole.MarkupLine("[blue]== Step 3: View Statistics ==[/]");
    var (points, vectors) = await pipeline.GetStatsAsync();
    AnsiConsole.MarkupLine("  Points: {0}", points);
    AnsiConsole.MarkupLine("  Vectors: {0}", vectors);
    AnsiConsole.MarkupLine("[green]✓ Statistics retrieved[/]\n");

    AnsiConsole.MarkupLine("[blue]== Step 4: Semantic Search ==[/]");
    var searchQuery = "What is event sourcing?";
    AnsiConsole.MarkupLine("Query: \"{0}\"", searchQuery);

    var searchResults = await pipeline.SearchAsync(searchQuery, limit: 3);
    AnsiConsole.MarkupLine("Found {0} results:", searchResults.Count);

    foreach (var result in searchResults)
    {
        AnsiConsole.MarkupLine("  - [cyan]{0}[/] {1} (Score: {2:P1})",
            Markup.Escape(result.Document.Category),
            Markup.Escape(result.Document.Title),
            result.Score);
    }
    AnsiConsole.MarkupLine("[green]✓ Search completed[/]\n");

    AnsiConsole.MarkupLine("[blue]== Step 5: RAG Query ==[/]");
    var ragQuery = "How does Kafka handle message ordering?";
    AnsiConsole.MarkupLine("Question: \"{0}\"", ragQuery);

    var (answer, sources) = await pipeline.AnswerAsync(ragQuery);
    AnsiConsole.MarkupLine("\nAnswer preview: {0}...", Markup.Escape(answer[..Math.Min(200, answer.Length)]));
    AnsiConsole.MarkupLine("\nSources used: {0}", sources.Count);
    foreach (var source in sources)
    {
        AnsiConsole.MarkupLine("  - {0} (Score: {1:P1})", Markup.Escape(source.Document.Title), source.Score);
    }
    AnsiConsole.MarkupLine("[green]✓ RAG query completed[/]\n");

    AnsiConsole.MarkupLine("[blue]== Step 6: List Documents ==[/]");
    var storedDocs = await pipeline.ListDocumentsAsync();
    AnsiConsole.MarkupLine("Stored documents: {0}", storedDocs.Count);
    AnsiConsole.MarkupLine("[green]✓ Document listing completed[/]\n");

    AnsiConsole.MarkupLine("[green]========================================[/]");
    AnsiConsole.MarkupLine("[green]  ALL TESTS PASSED SUCCESSFULLY!  [/]");
    AnsiConsole.MarkupLine("[green]========================================[/]");

    return 0;
}

async Task LoadSampleDocuments()
{
    var documents = SampleDocuments.GetTechDocuments();

    AnsiConsole.MarkupLine("\n[blue]== Loading {0} Sample Documents ==[/]\n", documents.Count);

    // Show documents to be loaded
    var previewTable = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("ID")
        .AddColumn("Title")
        .AddColumn("Category");

    foreach (var doc in documents)
    {
        previewTable.AddRow(doc.Id, doc.Title, $"[cyan]{doc.Category}[/]");
    }

    AnsiConsole.Write(previewTable);
    AnsiConsole.WriteLine();

    // Ingest with progress
    await AnsiConsole.Progress()
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Ingesting documents[/]", maxValue: documents.Count);

            var progress = new Progress<(int current, int total, string status)>(p =>
            {
                task.Value = p.current;
                task.Description = $"[green]{p.status}[/]";
            });

            await pipeline.IngestDocumentsAsync(documents, progress);
            task.Value = documents.Count;
        });

    AnsiConsole.MarkupLine("\n[green]Successfully loaded {0} documents![/]", documents.Count);
}

async Task SemanticSearch()
{
    AnsiConsole.MarkupLine("\n[blue]== Semantic Search ==[/]\n");

    var query = AnsiConsole.Ask<string>("[yellow]Enter your search query:[/]");

    var limit = AnsiConsole.Ask("Number of results?", 5);

    var categoryFilter = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[yellow]Filter by category?[/]")
            .AddChoices("All", "Messaging", "Architecture", "AI/ML"));

    var filter = categoryFilter == "All" ? null : categoryFilter;

    AnsiConsole.WriteLine();

    await AnsiConsole.Status()
        .StartAsync("Searching...", async ctx =>
        {
            var results = await pipeline.SearchAsync(query, limit, 0.0f, filter);

            ctx.Status("Done");

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            AnsiConsole.MarkupLine("[green]Found {0} results:[/]\n", results.Count);

            foreach (var (result, index) in results.Select((r, i) => (r, i)))
            {
                var scoreColor = result.Score switch
                {
                    >= 0.8f => "green",
                    >= 0.6f => "yellow",
                    _ => "grey"
                };

                var panel = new Panel(new Markup(
                    $"[bold]Score:[/] [{scoreColor}]{result.Score:P1}[/]\n" +
                    $"[bold]Category:[/] [cyan]{result.Document.Category}[/]\n" +
                    $"[bold]Source:[/] {result.Document.Source}\n\n" +
                    $"{Markup.Escape(result.Document.Content[..Math.Min(300, result.Document.Content.Length)])}..."))
                    .Header($"[cyan]{index + 1}. {Markup.Escape(result.Document.Title)}[/]")
                    .Border(BoxBorder.Rounded);

                AnsiConsole.Write(panel);
            }
        });
}

async Task AskQuestion()
{
    AnsiConsole.MarkupLine("\n[blue]== Ask a Question (RAG) ==[/]\n");

    AnsiConsole.MarkupLine("[grey]This demonstrates retrieval-augmented generation.[/]");
    AnsiConsole.MarkupLine("[grey]Your question is used to find relevant documents, which provide context for the answer.[/]\n");

    var question = AnsiConsole.Ask<string>("[yellow]Enter your question:[/]");

    AnsiConsole.WriteLine();

    await AnsiConsole.Status()
        .StartAsync("Finding relevant documents and generating answer...", async ctx =>
        {
            var (answer, sources) = await pipeline.AnswerAsync(question);

            ctx.Status("Done");

            // Show the answer
            var answerPanel = new Panel(new Markup(Markup.Escape(answer)))
                .Header("[green]Answer[/]")
                .Border(BoxBorder.Double)
                .BorderColor(Color.Green);

            AnsiConsole.Write(answerPanel);

            // Show sources
            if (sources.Count > 0)
            {
                AnsiConsole.MarkupLine("\n[yellow]Sources used:[/]");

                var sourcesTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Relevance")
                    .AddColumn("Title")
                    .AddColumn("Category");

                foreach (var source in sources)
                {
                    var scoreColor = source.Score >= 0.7f ? "green" : source.Score >= 0.5f ? "yellow" : "grey";
                    sourcesTable.AddRow(
                        $"[{scoreColor}]{source.Score:P0}[/]",
                        source.Document.Title,
                        $"[cyan]{source.Document.Category}[/]");
                }

                AnsiConsole.Write(sourcesTable);
            }
        });
}

async Task ListDocuments()
{
    AnsiConsole.MarkupLine("\n[blue]== Stored Documents ==[/]\n");

    var documents = await pipeline.ListDocumentsAsync();

    if (documents.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No documents stored. Load sample documents first.[/]");
        return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("ID")
        .AddColumn("Title")
        .AddColumn("Category")
        .AddColumn("Source");

    foreach (var doc in documents)
    {
        table.AddRow(
            doc.Id,
            doc.Title.Length > 40 ? doc.Title[..40] + "..." : doc.Title,
            $"[cyan]{doc.Category}[/]",
            doc.Source);
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine("\n[grey]Total: {0} documents[/]", documents.Count);
}

async Task ViewStatistics()
{
    AnsiConsole.MarkupLine("\n[blue]== Collection Statistics ==[/]\n");

    var (points, vectors) = await pipeline.GetStatsAsync();

    var table = new Table()
        .Border(TableBorder.Double)
        .AddColumn("Metric")
        .AddColumn("Value");

    table.AddRow("Collection", collectionName);
    table.AddRow("Documents (Points)", points.ToString("N0"));
    table.AddRow("Vectors", vectors.ToString("N0"));
    table.AddRow("Vector Size", $"{vectorSize} dimensions");
    table.AddRow("Embedding Model", embeddingModel);
    table.AddRow("Distance Metric", "Cosine");

    AnsiConsole.Write(new Panel(table)
        .Header("[cyan]Qdrant Collection Info[/]")
        .BorderColor(Color.Cyan1));
}

async Task ResetCollection()
{
    var confirm = AnsiConsole.Confirm("[red]Are you sure you want to delete all documents?[/]", false);

    if (!confirm)
    {
        AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
        return;
    }

    await pipeline.ResetAsync();
    AnsiConsole.MarkupLine("[green]Collection reset successfully.[/]");
}
