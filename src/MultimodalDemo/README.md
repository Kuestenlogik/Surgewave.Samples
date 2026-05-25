# Multimodal Document Processing Demo

Demonstrates Surgewave.AI's multimodal content handling, document splitting strategies, text cleaning pipelines, and the fluent DocumentPipeline builder API.

## Use Case

Modern AI applications process not just text, but also images, audio, and mixed-content documents. This sample shows how to create multimodal content objects, split documents into chunks for RAG embeddings, clean noisy text (headers, excessive whitespace), and compose end-to-end document processing pipelines.

## How to Run

```bash
dotnet run --project src/MultimodalDemo
```

No external dependencies. No API keys required for the core demo. Vision/audio features shown as configuration reference.

## Architecture

```
  Raw Content (Text, Image, Audio)
              |
              v
  +-------------------------+
  | MultimodalContent       |
  | - Text (plain text)     |
  | - Image (PNG bytes)     |
  | - Audio (WAV metadata)  |
  +------------+------------+
               |
               v
  +-------------------------+
  | MultimodalDocument      |
  | - Title, metadata       |
  | - Multiple content items|
  +------------+------------+
               |
               v
  +-------------------------+      +-------------------+
  | Document Splitting      | ---> | SentenceSplitter  |
  |                         |      | RecursiveCharacter |
  +------------+------------+      +-------------------+
               |
               v
  +-------------------------+
  | Document Cleaning       |
  | - Remove headers/footers|
  | - Normalize whitespace  |
  | - Strip control chars   |
  +------------+------------+
               |
               v
  +-------------------------+
  | DocumentPipeline        |
  | (fluent builder API)    |
  | Parsers -> Cleaner ->   |
  |   Splitter -> Chunks    |
  +-------------------------+
```

## What to Expect

1. **Multimodal Content** -- text, image, and audio content objects with metadata
2. **Multimodal Document** -- composite document combining multiple modalities
3. **Document Splitting** -- SentenceSplitter and RecursiveCharacterSplitter compared
4. **Document Cleaning** -- noisy text cleaned (headers, whitespace, control chars removed)
5. **DocumentPipeline** -- fluent builder creates end-to-end processing pipeline
6. **Vision & Audio** -- API-dependent features described (OpenAI Vision, Whisper)

## Prerequisites

- .NET 10 SDK
- (Optional) OPENAI_API_KEY for Vision and Audio features

## Surgewave Features Demonstrated

| Feature | How It's Used | Why It Matters |
|---------|---------------|----------------|
| Multimodal Content | `MultimodalContent` with Text/Image/Audio modalities | Unified model for any content type in AI pipelines |
| Multimodal Document | `MultimodalDocument` combines multiple content items | Documents with mixed media handled as single entity |
| Sentence Splitting | `SentenceSplitter(maxChunkSize, minChunkSize)` | Preserves sentence boundaries for better embeddings |
| Recursive Splitting | `RecursiveCharacterSplitter(chunkSize, overlap)` | Hierarchical splitting with configurable overlap |
| Document Cleaning | `DocumentCleaner` removes headers, normalizes whitespace | Clean input improves embedding and retrieval quality |
| Pipeline Builder | `DocumentPipeline.Create().WithSplitter().Build()` | Composable, fluent API for document processing |
| Built-in Parsers | PDF, DOCX, HTML, PlainText parsers included | No additional libraries needed for common formats |

## Key Code Highlights

### Multimodal Content Creation

```csharp
var textContent = new MultimodalContent
{
    Id = "content-text-01",
    Modality = ContentModality.Text,
    TextContent = "Surgewave is a high-performance message broker...",
    MimeType = "text/plain",
};
```

### Document Splitting Comparison

```csharp
// Sentence-aware splitting preserves natural boundaries
var sentenceSplitter = new SentenceSplitter(maxChunkSize: 500, minChunkSize: 100);
var sentenceChunks = sentenceSplitter.Split(document);

// Character-based splitting with overlap for context continuity
var recursiveSplitter = new RecursiveCharacterSplitter(chunkSize: 400, chunkOverlap: 50);
var recursiveChunks = recursiveSplitter.Split(document);
```

### Fluent Document Pipeline

```csharp
var pipeline = DocumentPipeline.Create()
    .WithSplitter(new RecursiveCharacterSplitter(chunkSize: 300, chunkOverlap: 50))
    .WithLoggerFactory(loggerFactory)
    .Build();

var chunks = await pipeline.ProcessAsync(stream, "text/plain", "document.txt");
```

## Key Takeaway

**Surgewave.AI provides a complete document processing toolkit -- multimodal content handling, intelligent splitting, text cleaning, and composable pipelines -- all running locally without external services.**
