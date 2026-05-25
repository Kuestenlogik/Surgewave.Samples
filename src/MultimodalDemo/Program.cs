using Kuestenlogik.Surgewave.AI.Documents.Cleaning;
using Kuestenlogik.Surgewave.AI.Documents.Models;
using Kuestenlogik.Surgewave.AI.Documents.Pipeline;
using Kuestenlogik.Surgewave.AI.Documents.Splitting;
using Kuestenlogik.Surgewave.AI.Multimodal.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spectre.Console;

AnsiConsole.Write(new FigletText("Multimodal").Color(Color.MediumPurple));
AnsiConsole.MarkupLine("[grey]Multimodal Content | Document Pipeline | Splitting | Cleaning[/]\n");

// ──────────────────────────────────────────────────────────────
// 1. Multimodal Content Creation
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]1. Multimodal Content[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Creating multimodal content objects with different modalities.[/]\n");

var textContent = new MultimodalContent
{
    Id = "content-text-01",
    Modality = ContentModality.Text,
    TextContent = "Surgewave is a high-performance message broker built on .NET 10, "
                + "designed as a drop-in replacement for Apache Kafka.",
    MimeType = "text/plain",
    Metadata = new MultimodalMetadata
    {
        Custom = { ["source"] = "documentation", ["language"] = "en" }
    }
};

// Simulate an image (we create placeholder bytes -- no real image required)
var imageBytes = new byte[100];
System.Security.Cryptography.RandomNumberGenerator.Fill(imageBytes);

var imageContent = new MultimodalContent
{
    Id = "content-image-01",
    Modality = ContentModality.Image,
    RawData = imageBytes,
    MimeType = "image/png",
    Metadata = new MultimodalMetadata
    {
        Width = 1920,
        Height = 1080,
        Custom = { ["description"] = "Architecture diagram of Surgewave cluster" }
    }
};

// Simulate audio metadata (no real audio required)
var audioContent = new MultimodalContent
{
    Id = "content-audio-01",
    Modality = ContentModality.Audio,
    RawData = [],
    MimeType = "audio/wav",
    Metadata = new MultimodalMetadata
    {
        Duration = TimeSpan.FromSeconds(45.5),
        SampleRate = 44100,
        Channels = 2,
        Custom = { ["title"] = "Surgewave architecture overview narration" }
    }
};

var contentItems = new[] { textContent, imageContent, audioContent };

var contentTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("ID")
    .AddColumn("Modality")
    .AddColumn("MIME Type")
    .AddColumn("Details");

foreach (var item in contentItems)
{
    var details = item.Modality switch
    {
        ContentModality.Text => $"{item.TextContent?.Length ?? 0} chars",
        ContentModality.Image => $"{item.Metadata.Width}x{item.Metadata.Height}, {item.RawData?.Length ?? 0} bytes",
        ContentModality.Audio => $"{item.Metadata.Duration?.TotalSeconds:F1}s, {item.Metadata.SampleRate}Hz, {item.Metadata.Channels}ch",
        _ => "N/A"
    };

    contentTable.AddRow(
        $"[cyan]{item.Id}[/]",
        $"[yellow]{item.Modality}[/]",
        item.MimeType ?? "N/A",
        details);
}

AnsiConsole.Write(contentTable);
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 2. Multimodal Document
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]2. Multimodal Document[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Composite document combining text, images, and audio.[/]\n");

var multiDoc = new MultimodalDocument
{
    Id = "doc-multimodal-01",
    Title = "Surgewave Architecture Overview",
    Contents = contentItems,
    Metadata = new Dictionary<string, string>
    {
        ["author"] = "Surgewave Team",
        ["version"] = "1.0",
        ["category"] = "architecture"
    }
};

AnsiConsole.MarkupLine($"  Document ID:    [cyan]{multiDoc.Id}[/]");
AnsiConsole.MarkupLine($"  Title:          [cyan]{multiDoc.Title}[/]");
AnsiConsole.MarkupLine($"  Content items:  [cyan]{multiDoc.Contents.Count}[/]");
AnsiConsole.MarkupLine($"  Modalities:     [cyan]{string.Join(", ", multiDoc.Contents.Select(c => c.Modality).Distinct())}[/]");
AnsiConsole.MarkupLine($"  Metadata:       [grey]{string.Join(", ", multiDoc.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}[/]");
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 3. Document Splitting
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]3. Document Splitting[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Splitting documents into chunks for RAG embeddings.[/]\n");

var sampleDocContent = """
    Apache Kafka is a distributed event streaming platform used by thousands of companies.
    It provides high-throughput, fault-tolerant publish-subscribe messaging along with
    stream processing capabilities.

    Surgewave is designed as a modern alternative to Kafka, built entirely in .NET 10.
    It achieves superior performance through zero-copy transfers, memory pooling,
    and optimized thread channel patterns. Surgewave aims to match Aeron-level throughput
    while providing a much simpler operational model.

    Key features of Surgewave include native Kafka protocol compatibility, allowing existing
    Kafka clients (such as Confluent.Kafka for .NET or Java clients) to connect with
    minimal code changes. Surgewave also supports gRPC and native TCP protocols for
    applications that need maximum performance.

    The Surgewave Connect framework enables building data pipelines with source and sink
    connectors. Over 100 connectors are available for databases, cloud services,
    file systems, and messaging platforms. Connectors are discovered via a plugin
    architecture and can be loaded at runtime.

    Surgewave AI provides integrated AI capabilities including agent hosting, RAG pipelines,
    document processing, guardrails for content safety, and MCP (Model Context Protocol)
    support for tool-augmented AI applications. The AI subsystem includes memory management
    for agents, tool result caching, and multimodal content processing.

    Deployment options range from single-node development setups to multi-node production
    clusters orchestrated with Kubernetes. Helm charts and monitoring dashboards are
    provided in the Surgewave.Templates repository.
    """;

var parsedDoc = new Document
{
    Id = "doc-surgewave-overview",
    Content = sampleDocContent,
    Metadata = new DocumentMetadata
    {
        Title = "Surgewave Overview",
        Author = "Surgewave Team",
        MimeType = "text/plain",
    }
};

// Sentence splitter
AnsiConsole.MarkupLine("[yellow]Sentence Splitter[/] (maxChunkSize=500, minChunkSize=100):\n");

var sentenceSplitter = new SentenceSplitter(maxChunkSize: 500, minChunkSize: 100);
var sentenceChunks = sentenceSplitter.Split(parsedDoc);

for (var i = 0; i < sentenceChunks.Count; i++)
{
    var chunk = sentenceChunks[i];
    var preview = chunk.Content.Length > 80
        ? string.Concat(chunk.Content.AsSpan(0, 77), "...")
        : chunk.Content;

    AnsiConsole.MarkupLine($"  Chunk {i}: [grey]{chunk.Content.Length,4} chars[/]  offset={chunk.StartOffset}-{chunk.EndOffset}");
    AnsiConsole.MarkupLine($"           [cyan]{Markup.Escape(preview)}[/]");
}

AnsiConsole.WriteLine();

// Recursive character splitter
AnsiConsole.MarkupLine("[yellow]Recursive Character Splitter[/] (chunkSize=400, overlap=50):\n");

var recursiveSplitter = new RecursiveCharacterSplitter(chunkSize: 400, chunkOverlap: 50);
var recursiveChunks = recursiveSplitter.Split(parsedDoc);

for (var i = 0; i < recursiveChunks.Count; i++)
{
    var chunk = recursiveChunks[i];
    var preview = chunk.Content.Length > 80
        ? string.Concat(chunk.Content.AsSpan(0, 77), "...")
        : chunk.Content;

    AnsiConsole.MarkupLine($"  Chunk {i}: [grey]{chunk.Content.Length,4} chars[/]  offset={chunk.StartOffset}-{chunk.EndOffset}");
    AnsiConsole.MarkupLine($"           [cyan]{Markup.Escape(preview)}[/]");
}

AnsiConsole.WriteLine();

// Comparison table
var splitterTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Splitter")
    .AddColumn("Chunks")
    .AddColumn("Avg Size")
    .AddColumn("Min Size")
    .AddColumn("Max Size");

splitterTable.AddRow(
    "Sentence",
    sentenceChunks.Count.ToString(),
    $"{sentenceChunks.Average(c => c.Content.Length):F0}",
    sentenceChunks.Min(c => c.Content.Length).ToString(),
    sentenceChunks.Max(c => c.Content.Length).ToString());

splitterTable.AddRow(
    "RecursiveCharacter",
    recursiveChunks.Count.ToString(),
    $"{recursiveChunks.Average(c => c.Content.Length):F0}",
    recursiveChunks.Min(c => c.Content.Length).ToString(),
    recursiveChunks.Max(c => c.Content.Length).ToString());

AnsiConsole.Write(splitterTable);
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 4. Document Cleaning
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]4. Document Cleaning[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Removing headers, normalizing whitespace, stripping control characters.[/]\n");

var dirtyContent = """
    Page 1 of 5


    Surgewave Architecture Guide

    This   is   a   document   with   excessive   spacing   and
    multiple    blank    lines    between    paragraphs.




    It also has page numbers that should be removed.

    42

    And control characters mixed in: Hello World.


    Page 2 of 5
    """;

AnsiConsole.MarkupLine("[yellow]Before cleaning:[/]");
var dirtyLines = dirtyContent.Split('\n');
for (var i = 0; i < dirtyLines.Length; i++)
{
    AnsiConsole.MarkupLine($"  [grey]{i + 1,2}|[/] {Markup.Escape(dirtyLines[i])}");
}

AnsiConsole.WriteLine();

var cleanerOptions = new DocumentCleanerOptions();

var cleaner = new DocumentCleaner(
    Options.Create(cleanerOptions),
    NullLoggerFactory.Instance.CreateLogger<DocumentCleaner>());

var cleanedContent = cleaner.Clean(dirtyContent);

AnsiConsole.MarkupLine("[yellow]After cleaning:[/]");
var cleanLines = cleanedContent.Split('\n');
for (var i = 0; i < cleanLines.Length; i++)
{
    AnsiConsole.MarkupLine($"  [grey]{i + 1,2}|[/] {Markup.Escape(cleanLines[i])}");
}

AnsiConsole.MarkupLine($"\n  [grey]Before: {dirtyContent.Length} chars -> After: {cleanedContent.Length} chars " +
                       $"({100.0 - (100.0 * cleanedContent.Length / dirtyContent.Length):F0}% reduction)[/]");
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 5. Document Pipeline Builder
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]5. Document Pipeline Builder[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Fluent API for constructing end-to-end document processing pipelines.[/]\n");

AnsiConsole.MarkupLine("[yellow]Building pipeline with default parsers and custom splitter:[/]\n");

// Note: DocumentCleanerOptions defaults already enable header/footer removal,
// whitespace normalization, and line trimming -- no custom config needed.
var pipeline = DocumentPipeline.Create()
    .WithSplitter(new RecursiveCharacterSplitter(chunkSize: 300, chunkOverlap: 50))
    .WithLoggerFactory(NullLoggerFactory.Instance)
    .Build();

AnsiConsole.MarkupLine("  Pipeline components:");
AnsiConsole.MarkupLine("    [cyan]Parsers:[/]   PDF, DOCX, HTML, PlainText (default set)");
AnsiConsole.MarkupLine("    [cyan]Cleaner:[/]   Headers/footers removal, whitespace normalization");
AnsiConsole.MarkupLine("    [cyan]Splitter:[/]  RecursiveCharacter (300 chars, 50 overlap)");
AnsiConsole.WriteLine();

// Process a plain text document through the pipeline
AnsiConsole.MarkupLine("[yellow]Processing sample text through the pipeline:[/]\n");

using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sampleDocContent));
var pipelineChunks = await pipeline.ProcessAsync(textStream, "text/plain", "surgewave-overview.txt");

AnsiConsole.MarkupLine($"  Input:  [grey]{sampleDocContent.Length} chars[/]");
AnsiConsole.MarkupLine($"  Output: [cyan]{pipelineChunks.Count} chunks[/]\n");

var pipelineTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("#")
    .AddColumn("Chunk ID")
    .AddColumn("Size")
    .AddColumn("Preview");

foreach (var chunk in pipelineChunks)
{
    var preview = chunk.Content.Length > 60
        ? string.Concat(chunk.Content.AsSpan(0, 57), "...")
        : chunk.Content;

    pipelineTable.AddRow(
        chunk.Index.ToString(),
        chunk.Id,
        $"{chunk.Content.Length} chars",
        Markup.Escape(preview));
}

AnsiConsole.Write(pipelineTable);
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 6. Vision and Audio (API Configuration)
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]6. Vision & Audio Setup[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]These features require API keys (OpenAI for Vision/Whisper).[/]\n");

AnsiConsole.Write(new Panel(new Markup(
    "[yellow]Vision (Image Analysis)[/]\n" +
    "  The [cyan]VisionLlmClient[/] sends images to an LLM with vision capabilities.\n" +
    "  Setup: Register via [cyan]services.AddSurgewaveMultimodal()[/]\n" +
    "  Requires: OPENAI_API_KEY environment variable\n\n" +
    "[yellow]Audio Transcription[/]\n" +
    "  The [cyan]WhisperTranscriber[/] converts audio to text via the Whisper API.\n" +
    "  Supports: WAV, MP3, FLAC, and other common audio formats\n" +
    "  Setup: Register via [cyan]services.AddSurgewaveMultimodal()[/]\n" +
    "  Requires: OPENAI_API_KEY environment variable\n\n" +
    "[yellow]Multimodal Embeddings[/]\n" +
    "  The [cyan]ClipEmbedder[/] creates embeddings for both images and text.\n" +
    "  Enables cross-modal search (find images by text description).\n" +
    "  Setup: Register via [cyan]services.AddSurgewaveMultimodal()[/]\n\n" +
    "[yellow]Multimodal RAG[/]\n" +
    "  The [cyan]MultimodalRagPipeline[/] combines text and image context\n" +
    "  for retrieval-augmented generation with visual understanding.\n" +
    "  The [cyan]ImageContextBuilder[/] formats image descriptions for LLM prompts."))
    .Header("[cyan]API-Dependent Features[/]")
    .Border(BoxBorder.Rounded));

AnsiConsole.MarkupLine($"\n[green]Demo complete![/]");
