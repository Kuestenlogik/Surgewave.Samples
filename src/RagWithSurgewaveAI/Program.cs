// ============================================================
// Surgewave.AI RAG Pipeline Demo
// No API keys required -- all processing runs locally
// ============================================================
//
// This sample demonstrates the full RAG (Retrieval-Augmented Generation)
// workflow using Surgewave.AI's built-in libraries. Unlike the RagPipeline
// sample that requires OpenAI and Qdrant, this runs entirely in-process.
// ============================================================

using System.Diagnostics;
using System.Globalization;
using Kuestenlogik.Surgewave.AI.Documents.Models;
using Kuestenlogik.Surgewave.AI.Documents.Splitting;
using Kuestenlogik.Surgewave.AI.Evaluation.Metrics;
using Kuestenlogik.Surgewave.AI.Evaluation.Runner;
using Kuestenlogik.Surgewave.AI.Prompts.BuiltIn;
using Kuestenlogik.Surgewave.AI.Prompts.Parsing;
using Kuestenlogik.Surgewave.AI.Rag.Components;
using Kuestenlogik.Surgewave.AI.Rag.Implementations;
using Kuestenlogik.Surgewave.AI.Rag.Pipeline;
using Kuestenlogik.Surgewave.AI.Retrieval.Hybrid;
using Kuestenlogik.Surgewave.AI.Retrieval.Indexing;
using Kuestenlogik.Surgewave.AI.Retrieval.Keyword;
using Kuestenlogik.Surgewave.AI.Retrieval.Reranking;
using Kuestenlogik.Surgewave.AI.Retrieval.Semantic;
using Spectre.Console;

// Check for test mode
var testMode = args.Length > 0 && args[0] == "--test";

AnsiConsole.Write(new FigletText("Surgewave.AI RAG").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Documents -> Splitting -> Indexing -> Retrieval -> Prompts -> Evaluation[/]");
AnsiConsole.MarkupLine("[green]No API keys required -- all processing runs locally[/]\n");

var totalSw = Stopwatch.StartNew();

// ============================================================
// 1. DOCUMENT PROCESSING
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 1: Document Processing[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Creating sample documents and splitting into chunks...[/]\n");

var documents = SampleDocuments.GetAll();

var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("ID")
    .AddColumn("Title")
    .AddColumn("Length");

foreach (var doc in documents)
{
    table.AddRow(
        $"[cyan]{Markup.Escape(doc.Id)}[/]",
        Markup.Escape(doc.Metadata.Title ?? "Untitled"),
        $"{doc.Content.Length:N0} chars");
}

AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"\n[green]{documents.Count} documents loaded[/]");

// Split documents using SentenceSplitter
var splitter = new SentenceSplitter(maxChunkSize: 500, minChunkSize: 50);
var allChunks = new List<DocumentChunk>();

foreach (var doc in documents)
{
    var chunks = splitter.Split(doc);
    allChunks.AddRange(chunks);
}

AnsiConsole.MarkupLine($"[green]{allChunks.Count} chunks created with SentenceSplitter (max 500 chars)[/]");

// Also demonstrate RecursiveCharacterSplitter
var recursiveSplitter = new RecursiveCharacterSplitter(chunkSize: 400, chunkOverlap: 50);
var recursiveChunks = new List<DocumentChunk>();

foreach (var doc in documents)
{
    var chunks = recursiveSplitter.Split(doc);
    recursiveChunks.AddRange(chunks);
}

AnsiConsole.MarkupLine($"[green]{recursiveChunks.Count} chunks created with RecursiveCharacterSplitter (400 chars, 50 overlap)[/]\n");

// ============================================================
// 2. INDEXING
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 2: Indexing into BM25 + Vector Store[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Creating hash-based embeddings and indexing into dual stores...[/]\n");

var embedder = new HashEmbedder(dimensions: 64);
using var bm25Retriever = new Bm25Retriever();
using var vectorStore = new VectorStore();

var indexer = new DocumentIndexer(bm25Retriever, vectorStore);

var indexSw = Stopwatch.StartNew();
await indexer.IndexAsync(allChunks, embedder);
indexSw.Stop();

AnsiConsole.MarkupLine($"  BM25 index:    [green]{bm25Retriever.Count}[/] documents indexed");
AnsiConsole.MarkupLine($"  Vector store:  [green]{vectorStore.Count}[/] vectors stored (64-dim hash embeddings)");
AnsiConsole.MarkupLine($"  Index time:    [green]{indexSw.ElapsedMilliseconds}ms[/]\n");

// ============================================================
// 3. RETRIEVAL
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 3: Retrieval -- BM25, Semantic, and Hybrid[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Comparing three retrieval strategies on the same query...[/]\n");

var semanticRetriever = new SemanticRetriever(vectorStore, embedder);
var hybridRetriever = new HybridRetriever(bm25Retriever, semanticRetriever, new HybridConfig
{
    KeywordWeight = 0.3,
    SemanticWeight = 0.7,
    Strategy = FusionStrategy.ReciprocalRankFusion
});

var searchQuery = "How does event sourcing store state changes?";
AnsiConsole.MarkupLine($"  Query: [yellow]\"{Markup.Escape(searchQuery)}\"[/]\n");

var context = new RagPipelineContext();
var queryEmbedding = await embedder.EmbedAsync(searchQuery);

var retrieverInput = new RetrieverInput
{
    Query = searchQuery,
    QueryEmbedding = queryEmbedding,
    TopK = 5
};

// BM25 retrieval
var bm25Results = await bm25Retriever.ExecuteAsync(retrieverInput, context);
PrintRetrievalResults("BM25 (Keyword)", bm25Results.Documents);

// Semantic retrieval
var semanticResults = await semanticRetriever.ExecuteAsync(retrieverInput, context);
PrintRetrievalResults("Semantic (Vector)", semanticResults.Documents);

// Hybrid retrieval
var hybridResults = await hybridRetriever.ExecuteAsync(retrieverInput, context);
PrintRetrievalResults("Hybrid (BM25 + Semantic, RRF)", hybridResults.Documents);

// ============================================================
// 4. RERANKING
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 4: Reranking with CrossEncoderReranker[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Using KeywordOverlapScorer to rerank hybrid results...[/]\n");

var scorer = new KeywordOverlapScorer();
var reranker = new CrossEncoderReranker(scorer);

var rerankerInput = new RerankerInput
{
    Query = searchQuery,
    Documents = hybridResults.Documents,
    TopK = 3
};

var reranked = await reranker.ExecuteAsync(rerankerInput, context);
PrintRetrievalResults("Reranked (Top 3)", reranked.Documents);

// ============================================================
// 5. PROMPT BUILDING
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 5: Prompt Building with Templates[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Demonstrating TemplatePromptBuilder and PromptTemplate engine...[/]\n");

// 5a. Using TemplatePromptBuilder from RAG pipeline
var promptBuilder = new TemplatePromptBuilder();
var promptInput = new PromptBuilderInput
{
    Query = searchQuery,
    Documents = reranked.Documents
};

var promptOutput = await promptBuilder.ExecuteAsync(promptInput, context);

AnsiConsole.MarkupLine("[yellow]--- TemplatePromptBuilder (default RAG template) ---[/]");
var promptPanel = new Panel(Markup.Escape(promptOutput.Prompt))
    .Header("[cyan]Generated Prompt[/]")
    .Border(BoxBorder.Rounded)
    .BorderColor(Color.Cyan1);
AnsiConsole.Write(promptPanel);
AnsiConsole.MarkupLine($"  Messages: [green]{promptOutput.Messages.Count}[/] (system + user)\n");

// 5b. Using RagTemplates from Surgewave.AI.Prompts
var contextText = string.Join("\n---\n",
    reranked.Documents.Select((d, i) => $"[Source {i + 1}] {d.Content}"));

var templateVariables = new Dictionary<string, object?>
{
    ["context"] = contextText,
    ["query"] = searchQuery
};

AnsiConsole.MarkupLine("[yellow]--- Built-in RagTemplates ---[/]");
var templates = new (string Name, PromptTemplate Template)[]
{
    ("Default", RagTemplates.Default),
    ("Strict", RagTemplates.Strict),
    ("Creative", RagTemplates.Creative),
    ("Conversational", RagTemplates.Conversational)
};

var templateTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Template")
    .AddColumn("System Message (excerpt)");

foreach (var (name, tmpl) in templates)
{
    var messages = tmpl.RenderMessages(templateVariables);
    var systemMsg = messages.FirstOrDefault(m => m.Role == Kuestenlogik.Surgewave.AI.Prompts.Messages.ChatRole.System);
    var excerpt = systemMsg is not null
        ? systemMsg.Content[..Math.Min(80, systemMsg.Content.Length)] + "..."
        : "(no system message)";
    templateTable.AddRow($"[cyan]{name}[/]", Markup.Escape(excerpt));
}

AnsiConsole.Write(templateTable);

// 5c. Custom template with conditionals
AnsiConsole.MarkupLine("\n[yellow]--- Custom Template with Conditionals ---[/]");
var customTemplate = PromptTemplate.Parse(
    """
    {{#system}}You are a Surgewave messaging expert.{{/system}}
    {{#user}}{{#if context}}Based on the following context:
    {{context}}

    {{/if}}{{query}}{{/user}}
    """,
    "surgewave-expert");

var customMessages = customTemplate.RenderMessages(templateVariables);
foreach (var msg in customMessages)
{
    var roleColor = msg.Role == Kuestenlogik.Surgewave.AI.Prompts.Messages.ChatRole.System ? "blue" : "green";
    var preview = msg.Content.Length > 120 ? msg.Content[..120] + "..." : msg.Content;
    AnsiConsole.MarkupLine($"  [{roleColor}]{msg.Role}:[/] {Markup.Escape(preview)}");
}

AnsiConsole.WriteLine();

// ============================================================
// 6. EVALUATION (without LLM)
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 6: RAG Evaluation (rule-based, no LLM needed)[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Running 6 metrics on a test dataset...[/]\n");

// Create evaluation metrics
IEvaluationMetric[] metrics =
[
    new FaithfulnessMetric(),
    new AnswerRelevancyMetric(),
    new ContextPrecisionMetric(),
    new ContextRecallMetric(),
    new AnswerCorrectnessMetric(),
    new AnswerCompletenessMetric()
];

var runner = new EvaluationRunner(metrics);

// Create test dataset: simulate RAG answers
var evalInputs = new List<EvaluationInput>
{
    new()
    {
        Question = "What is event sourcing?",
        Answer = "Event sourcing is an architectural pattern that stores state changes as a sequence of immutable events. Instead of overwriting the current state, every change is captured as a new event, enabling full audit trails and temporal queries.",
        Contexts = reranked.Documents.Select(d => d.Content).ToList(),
        GroundTruth = "Event sourcing stores state changes as an immutable sequence of events rather than mutable records."
    },
    new()
    {
        Question = "How does Surgewave handle message partitioning?",
        Answer = "Surgewave uses consistent hashing for partition assignment, distributing messages across partitions based on their keys. This ensures ordered delivery within a partition while maximizing parallelism across consumers.",
        Contexts = [
            "Surgewave distributes messages across partitions using keys for ordering guarantees.",
            "Partitioning enables parallel consumption while maintaining per-key ordering."
        ],
        GroundTruth = "Surgewave partitions messages by key using consistent hashing for ordered delivery within partitions."
    },
    new()
    {
        Question = "What is stream processing?",
        Answer = "Stream processing is the continuous, real-time processing of data as it arrives. It enables transformations, aggregations, and pattern detection on unbounded data streams without waiting for batch completion.",
        Contexts = [
            "Stream processing handles unbounded data in real-time, applying transformations as events arrive.",
            "Unlike batch processing, stream processing operates continuously on incoming data."
        ],
        GroundTruth = "Stream processing is real-time processing of continuous data streams with transformations and aggregations."
    }
};

var report = await runner.EvaluateBatchAsync(evalInputs);

// Display per-question results
foreach (var result in report.Results)
{
    var questionExcerpt = result.Input.Question.Length > 50
        ? result.Input.Question[..50] + "..."
        : result.Input.Question;

    AnsiConsole.MarkupLine($"  [yellow]Q:[/] {Markup.Escape(questionExcerpt)}  [grey]Overall: {result.OverallScore:P0}[/]");
}

AnsiConsole.WriteLine();

// Display metric summaries
var summaryTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Metric")
    .AddColumn("Mean")
    .AddColumn("Median")
    .AddColumn("Min")
    .AddColumn("Max")
    .AddColumn("StdDev");

foreach (var summary in report.MetricSummaries.Values)
{
    summaryTable.AddRow(
        $"[cyan]{summary.MetricName}[/]",
        summary.Mean.ToString("P1", CultureInfo.InvariantCulture),
        summary.Median.ToString("P1", CultureInfo.InvariantCulture),
        summary.Min.ToString("P1", CultureInfo.InvariantCulture),
        summary.Max.ToString("P1", CultureInfo.InvariantCulture),
        summary.StdDev.ToString("F3", CultureInfo.InvariantCulture));
}

AnsiConsole.Write(new Panel(summaryTable)
    .Header("[cyan]Evaluation Report[/]")
    .BorderColor(Color.Cyan1));

AnsiConsole.MarkupLine($"\n  Overall score: [green]{report.OverallScore:P1}[/]");
AnsiConsole.MarkupLine($"  Evaluated at:  [grey]{report.EvaluatedAt:yyyy-MM-dd HH:mm:ss}[/]\n");

// ============================================================
// 7. COMPARISON TABLE
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 7: Comparison -- RagPipeline vs RagWithSurgewaveAI[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Side-by-side comparison of the two approaches...[/]\n");

var comparisonTable = new Table()
    .Border(TableBorder.Double)
    .AddColumn("Feature")
    .AddColumn("[red]RagPipeline (direct API)[/]")
    .AddColumn("[green]RagWithSurgewaveAI (this demo)[/]");

comparisonTable.AddRow("API Keys", "[red]Yes (OpenAI, Qdrant)[/]", "[green]None required[/]");
comparisonTable.AddRow("Vector Store", "Qdrant (Docker)", "[green]In-memory (built-in)[/]");
comparisonTable.AddRow("Embeddings", "OpenAI API ($)", "[green]Local / pluggable[/]");
comparisonTable.AddRow("Retrieval", "Simple similarity", "[green]BM25 + Semantic + Hybrid[/]");
comparisonTable.AddRow("Prompt Templates", "Manual string concat", "[green]TemplateParser engine[/]");
comparisonTable.AddRow("Built-in Templates", "None", "[green]4 RAG templates[/]");
comparisonTable.AddRow("Evaluation", "None", "[green]6 metrics built-in[/]");
comparisonTable.AddRow("Reranking", "None", "[green]CrossEncoderReranker[/]");
comparisonTable.AddRow("Document Splitting", "None", "[green]Sentence + Recursive[/]");
comparisonTable.AddRow("Score Fusion", "None", "[green]RRF + Weighted Sum[/]");

AnsiConsole.Write(comparisonTable);
AnsiConsole.WriteLine();

// ============================================================
// 8. RAG PIPELINE BUILDER
// ============================================================
AnsiConsole.Write(new Rule("[cyan]Step 8: Full RAG Pipeline (end-to-end)[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Building a complete pipeline: Embedder -> Retriever -> Reranker -> PromptBuilder[/]\n");

// Build the pipeline components
var inMemoryRetriever = new InMemoryRetriever();

// Index all chunks into the InMemoryRetriever for the pipeline demo
foreach (var chunk in allChunks)
{
    var embedding = await embedder.EmbedAsync(chunk.Content);
    inMemoryRetriever.AddDocument(
        chunk.Id,
        chunk.Content,
        embedding,
        new Dictionary<string, object?>
        {
            ["documentId"] = chunk.DocumentId,
            ["index"] = chunk.Index
        });
}

var pipelinePromptBuilder = new TemplatePromptBuilder(
    template: """
        Answer the question based on the provided context. Be concise and precise.

        Context:
        {context}

        Question: {query}

        Answer:
        """,
    systemMessage: "You are an expert on distributed messaging systems.");

var pipelineReranker = new SimpleReranker();

// Show the pipeline composition
AnsiConsole.MarkupLine("  Pipeline stages:");
AnsiConsole.MarkupLine($"    1. [cyan]{embedder.Name}[/] ({embedder.Type})");
AnsiConsole.MarkupLine($"    2. [cyan]{inMemoryRetriever.Name}[/] ({inMemoryRetriever.Type})");
AnsiConsole.MarkupLine($"    3. [cyan]{pipelineReranker.Name}[/] ({pipelineReranker.Type})");
AnsiConsole.MarkupLine($"    4. [cyan]{pipelinePromptBuilder.Name}[/] ({pipelinePromptBuilder.Type})");
AnsiConsole.MarkupLine("    [grey](In production, add an ILlmClient as stage 5 for LLM completion)[/]");

// Execute stages manually to show each step
var pipelineQuery = "What are the benefits of event sourcing over traditional CRUD?";
AnsiConsole.MarkupLine($"\n  Query: [yellow]\"{Markup.Escape(pipelineQuery)}\"[/]\n");

var pipelineContext = new RagPipelineContext();

// Stage 1: Embed the query
var embedOutput = await embedder.ExecuteAsync(
    new EmbedderInput { Query = pipelineQuery }, pipelineContext);
AnsiConsole.MarkupLine($"  Stage 1 (Embed):    [green]{embedOutput.QueryEmbedding.Length}-dim vector[/]");

// Stage 2: Retrieve relevant documents
var retrieveOutput = await inMemoryRetriever.ExecuteAsync(
    new RetrieverInput
    {
        Query = pipelineQuery,
        QueryEmbedding = embedOutput.QueryEmbedding,
        TopK = 5
    }, pipelineContext);
AnsiConsole.MarkupLine($"  Stage 2 (Retrieve): [green]{retrieveOutput.Documents.Count} documents[/]");

// Stage 3: Rerank
var rerankOutput = await pipelineReranker.ExecuteAsync(
    new RerankerInput
    {
        Query = pipelineQuery,
        Documents = retrieveOutput.Documents,
        TopK = 3
    }, pipelineContext);
AnsiConsole.MarkupLine($"  Stage 3 (Rerank):   [green]{rerankOutput.Documents.Count} documents (top 3)[/]");

// Stage 4: Build prompt
var buildOutput = await pipelinePromptBuilder.ExecuteAsync(
    new PromptBuilderInput
    {
        Query = pipelineQuery,
        Documents = rerankOutput.Documents
    }, pipelineContext);
AnsiConsole.MarkupLine($"  Stage 4 (Prompt):   [green]{buildOutput.Prompt.Length} chars, {buildOutput.Messages.Count} messages[/]\n");

var pipelinePromptPanel = new Panel(Markup.Escape(
    buildOutput.Prompt.Length > 600
        ? buildOutput.Prompt[..600] + "\n..."
        : buildOutput.Prompt))
    .Header("[green]Pipeline Output (constructed prompt)[/]")
    .Border(BoxBorder.Double)
    .BorderColor(Color.Green);

AnsiConsole.Write(pipelinePromptPanel);
AnsiConsole.MarkupLine($"\n  [grey]Without an LLM, the pipeline stops at prompt construction.[/]");
AnsiConsole.MarkupLine($"  [grey]In production, add an ILlmClient to get the final answer.[/]\n");

totalSw.Stop();

// ============================================================
// SUMMARY
// ============================================================
AnsiConsole.Write(new Rule("[green]Complete[/]").LeftJustified());
AnsiConsole.MarkupLine($"  Total time: [green]{totalSw.ElapsedMilliseconds}ms[/]");
AnsiConsole.MarkupLine($"  Documents processed: [green]{documents.Count}[/]");
AnsiConsole.MarkupLine($"  Chunks indexed: [green]{allChunks.Count}[/]");
AnsiConsole.MarkupLine($"  Retrieval strategies: [green]3[/] (BM25, Semantic, Hybrid)");
AnsiConsole.MarkupLine($"  Prompt templates: [green]4[/] built-in + custom");
AnsiConsole.MarkupLine($"  Evaluation metrics: [green]6[/]");
AnsiConsole.MarkupLine($"  API keys required: [green]0[/]\n");

if (!testMode)
{
    // Interactive search loop
    AnsiConsole.Write(new Rule("[cyan]Interactive Search[/]").LeftJustified());
    AnsiConsole.MarkupLine("[grey]Type a question to search the knowledge base. Type 'quit' to exit.[/]\n");

    while (true)
    {
        var question = AnsiConsole.Ask<string>("[yellow]Question:[/]");

        if (string.Equals(question, "quit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[green]Goodbye![/]");
            break;
        }

        var qEmbedding = await embedder.EmbedAsync(question);
        var qInput = new RetrieverInput
        {
            Query = question,
            QueryEmbedding = qEmbedding,
            TopK = 3
        };

        var qResults = await hybridRetriever.ExecuteAsync(qInput, context);

        if (qResults.Documents.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No relevant documents found.[/]\n");
            continue;
        }

        AnsiConsole.MarkupLine($"\n[green]Found {qResults.Documents.Count} relevant passages:[/]\n");

        foreach (var (doc, idx) in qResults.Documents.Select((d, i) => (d, i)))
        {
            var scoreColor = doc.Score switch
            {
                >= 0.02 => "green",
                >= 0.01 => "yellow",
                _ => "grey"
            };
            var preview = doc.Content.Length > 200 ? doc.Content[..200] + "..." : doc.Content;
            var resultPanel = new Panel(Markup.Escape(preview))
                .Header($"[cyan]{idx + 1}.[/] [{scoreColor}]Score: {doc.Score:F4}[/]  [grey]{Markup.Escape(doc.Id)}[/]")
                .Border(BoxBorder.Rounded);
            AnsiConsole.Write(resultPanel);
        }

        // Build prompt using the Strict template
        var qContext = string.Join("\n---\n",
            qResults.Documents.Select((d, i) => $"[Source {i + 1}] {d.Content}"));

        var qVars = new Dictionary<string, object?>
        {
            ["context"] = qContext,
            ["query"] = question
        };

        var renderedMessages = RagTemplates.Strict.RenderMessages(qVars);
        var userMsg = renderedMessages.LastOrDefault(m => m.Role == Kuestenlogik.Surgewave.AI.Prompts.Messages.ChatRole.User);

        if (userMsg is not null)
        {
            AnsiConsole.MarkupLine("\n[yellow]Constructed prompt (would be sent to LLM):[/]");
            var userPromptPreview = userMsg.Content.Length > 400
                ? userMsg.Content[..400] + "\n..."
                : userMsg.Content;
            AnsiConsole.Write(new Panel(Markup.Escape(userPromptPreview))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow));
        }

        AnsiConsole.WriteLine();
    }
}

return 0;

// ============================================================
// Helper methods
// ============================================================
static void PrintRetrievalResults(string label, List<RetrievedDocument> docs)
{
    AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(label)}:[/]");
    if (docs.Count == 0)
    {
        AnsiConsole.MarkupLine("    (no results)");
    }
    else
    {
        foreach (var (doc, i) in docs.Select((d, idx) => (d, idx)))
        {
            var preview = doc.Content.Length > 80 ? doc.Content[..80] + "..." : doc.Content;
            var scoreColor = doc.Score switch
            {
                >= 0.5 => "green",
                >= 0.1 => "yellow",
                _ => "grey"
            };
            AnsiConsole.MarkupLine($"    {i + 1}. [{scoreColor}]{doc.Score:F4}[/] {Markup.Escape(preview)}");
        }
    }

    AnsiConsole.WriteLine();
}

// ============================================================
// Hash-based Embedder (no API keys needed)
// ============================================================
/// <summary>
/// Deterministic hash-based embedder that produces consistent pseudo-embeddings
/// from text content. Useful for demos and testing without external API dependencies.
/// </summary>
sealed class HashEmbedder : IEmbedder
{
    private readonly int _dimensions;

    public HashEmbedder(int dimensions = 64)
    {
        _dimensions = dimensions;
    }

    public string Name => "hash-embedder";

    public string Type => "embedder";

    public Task<EmbedderOutput> ExecuteAsync(
        EmbedderInput input,
        RagPipelineContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var queryEmbedding = ComputeEmbedding(input.Query);

        IReadOnlyList<float[]>? docEmbeddings = null;
        if (input.Documents is { Count: > 0 })
        {
            docEmbeddings = input.Documents.Select(ComputeEmbedding).ToList();
        }

        return Task.FromResult(new EmbedderOutput
        {
            QueryEmbedding = queryEmbedding,
            DocumentEmbeddings = docEmbeddings
        });
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        return Task.FromResult(ComputeEmbedding(text));
    }

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        var embeddings = texts.Select(ComputeEmbedding).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    private float[] ComputeEmbedding(string text)
    {
        // Use a stable hash to create deterministic pseudo-embeddings.
        // Texts with similar words will produce somewhat similar vectors
        // because we accumulate per-word contributions.
        var embedding = new float[_dimensions];

        // Split into words and contribute each word's hash to the vector
        var words = text.Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var wordHash = StableHash(word.ToLowerInvariant());
#pragma warning disable CA5394 // Intentionally using Random for deterministic pseudo-embeddings
            var rng = new Random(wordHash);

            for (var i = 0; i < _dimensions; i++)
            {
                embedding[i] += (float)(rng.NextDouble() * 2 - 1);
            }
#pragma warning restore CA5394
        }

        // Normalize to unit vector
        var magnitude = 0f;
        for (var i = 0; i < _dimensions; i++)
        {
            magnitude += embedding[i] * embedding[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude > 0)
        {
            for (var i = 0; i < _dimensions; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }

    /// <summary>
    /// Produces a stable hash that does not change between runs (unlike string.GetHashCode).
    /// </summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = 5381;
            foreach (var c in text)
            {
                hash = ((hash << 5) + hash) + c;
            }

            return hash;
        }
    }
}

// ============================================================
// Sample Documents
// ============================================================
static class SampleDocuments
{
    public static IReadOnlyList<Document> GetAll() =>
    [
        new Document
        {
            Id = "doc-event-sourcing",
            Content = """
                Event sourcing is an architectural pattern where state changes are stored as an immutable
                sequence of events rather than mutable database records. Instead of updating a row in a
                database when something changes, you append a new event to an event log. The current
                state of any entity can be reconstructed by replaying all events from the beginning.

                Key benefits of event sourcing include: complete audit trails showing exactly what happened
                and when, the ability to reconstruct state at any point in time (temporal queries), natural
                support for event-driven architectures, and simplified debugging since every state transition
                is explicitly recorded. Event sourcing pairs naturally with CQRS (Command Query Responsibility
                Segregation) where the write model appends events and read models are projected from the
                event stream. This separation allows independent scaling and optimization of reads and writes.
                """,
            Metadata = new DocumentMetadata { Title = "Understanding Event Sourcing" }
        },
        new Document
        {
            Id = "doc-stream-processing",
            Content = """
                Stream processing is the real-time, continuous processing of data as it arrives. Unlike batch
                processing which operates on bounded datasets at scheduled intervals, stream processing handles
                unbounded data flows with low latency. Events are processed individually or in micro-batches
                as they flow through the system.

                Common stream processing operations include filtering (selecting relevant events), mapping
                (transforming event data), windowing (grouping events by time intervals), aggregation
                (computing running totals, averages, counts), and joining (correlating events from multiple
                streams). Stream processing frameworks must handle challenges like out-of-order events, late
                arrivals, and exactly-once processing semantics. Surgewave provides built-in stream processing
                with its Streams API, supporting tumbling, sliding, and session windows with watermark-based
                late event handling.
                """,
            Metadata = new DocumentMetadata { Title = "Stream Processing Fundamentals" }
        },
        new Document
        {
            Id = "doc-surgewave-architecture",
            Content = """
                Surgewave is a high-performance distributed messaging system built on modern .NET. It serves as a
                drop-in replacement for Apache Kafka with significantly lower operational overhead. The Surgewave
                broker manages topics, partitions, and consumer groups with zero-copy message delivery using
                Memory<T> and Span<T> for maximum throughput.

                Surgewave's architecture centers on an append-only commit log where messages are persisted in
                partition order. Producers send messages with optional keys for partition routing, ensuring
                ordered delivery within a partition. Consumers read from partitions using offsets, enabling
                replay and parallel consumption through consumer groups. The broker supports multiple
                protocols including a native binary protocol optimized for .NET, gRPC for cross-platform
                interoperability, and a Kafka-compatible protocol for migration scenarios.
                """,
            Metadata = new DocumentMetadata { Title = "Surgewave Architecture Overview" }
        },
        new Document
        {
            Id = "doc-partitioning",
            Content = """
                Message partitioning is a fundamental concept in distributed messaging systems. A topic is
                divided into partitions, each being an ordered, immutable sequence of messages. Producers
                assign messages to partitions using a partition key -- messages with the same key always go
                to the same partition, guaranteeing ordering for related messages.

                Surgewave uses consistent hashing for partition assignment, which minimizes rebalancing when
                partitions are added or removed. Each partition can be consumed by exactly one consumer
                within a consumer group, enabling parallel processing. The number of partitions determines
                the maximum parallelism for consumers. For optimal throughput, Surgewave recommends setting
                partition count equal to the expected number of concurrent consumers. Partition rebalancing
                occurs automatically when consumers join or leave a group, using a cooperative sticky
                assignment strategy to minimize disruption.
                """,
            Metadata = new DocumentMetadata { Title = "Message Partitioning in Surgewave" }
        },
        new Document
        {
            Id = "doc-connect-framework",
            Content = """
                Surgewave Connect is a framework for building data integration pipelines using source and sink
                connectors. Source connectors ingest data from external systems (databases, APIs, file systems)
                into Surgewave topics. Sink connectors push data from Surgewave topics to external destinations.

                The connector framework provides automatic offset management, schema evolution support, and
                fault-tolerant task distribution. Connectors are distributed as plugins and discovered at
                runtime from a configurable plugins directory. Each connector defines its configuration
                schema, validation rules, and task lifecycle. Surgewave includes built-in connectors for common
                integrations: file system (CSV, JSON, Avro), databases (SQL via CDC), HTTP/webhooks, and
                standard I/O streams. Third-party connectors extend the ecosystem with support for cloud
                storage, message queues, and specialized data sources.
                """,
            Metadata = new DocumentMetadata { Title = "Surgewave Connect Framework" }
        },
        new Document
        {
            Id = "doc-consumer-groups",
            Content = """
                Consumer groups enable parallel message processing by distributing partitions among multiple
                consumers. Each consumer in a group is assigned a subset of partitions, and each partition
                is consumed by exactly one consumer within the group. This ensures that messages within a
                partition are processed in order while enabling horizontal scaling.

                When a consumer joins or leaves a group, Surgewave triggers a rebalance to redistribute
                partitions. The cooperative sticky assignor minimizes partition movement during rebalances,
                allowing existing assignments to continue processing while only reassigning the minimum
                necessary partitions. Consumers commit offsets to track their progress, enabling recovery
                after failures. Surgewave supports both auto-commit (periodic) and manual commit modes. For
                exactly-once semantics, consumers can use transactional offsets tied to output operations.
                """,
            Metadata = new DocumentMetadata { Title = "Consumer Groups and Offset Management" }
        },
        new Document
        {
            Id = "doc-cqrs-pattern",
            Content = """
                CQRS (Command Query Responsibility Segregation) separates read and write operations into
                distinct models. The write side processes commands that generate events, while the read side
                maintains materialized views optimized for queries. This separation allows each side to scale
                independently and use storage technologies best suited to their access patterns.

                In a Surgewave-based CQRS system, commands are published to a topic, processed by a command
                handler that validates and generates events, which are then published to an event topic.
                Read-model projectors consume the event topic and update denormalized views in a query
                database. This architecture supports eventual consistency between write and read models,
                typically with sub-second propagation delays. CQRS combined with event sourcing provides
                a powerful architecture for complex domains where audit trails, temporal queries, and
                independent scaling are important requirements.
                """,
            Metadata = new DocumentMetadata { Title = "CQRS with Surgewave" }
        },
        new Document
        {
            Id = "doc-ai-pipelines",
            Content = """
                Surgewave.AI provides a comprehensive toolkit for building AI-powered applications on top of
                the Surgewave messaging platform. The RAG (Retrieval-Augmented Generation) pipeline combines
                document processing, vector-based retrieval, and prompt engineering into a cohesive workflow.

                Key components include: DocumentPipeline for parsing and splitting documents into chunks,
                VectorStore for in-memory similarity search, BM25 and Semantic retrievers with hybrid
                fusion via Reciprocal Rank Fusion (RRF), TemplateParser for advanced prompt templates
                with conditionals and loops, and EvaluationRunner for measuring RAG quality with metrics
                like faithfulness, answer relevancy, context precision, and answer correctness. The AI
                pipeline integrates natively with Surgewave topics, enabling real-time document ingestion
                and embedding updates through the connector framework.
                """,
            Metadata = new DocumentMetadata { Title = "Surgewave.AI Pipeline Components" }
        }
    ];
}
