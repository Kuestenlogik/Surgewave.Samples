# RAG Pipeline Sample

Retrieval-Augmented Generation (RAG) demonstration: Documents -> OpenAI embeddings -> Qdrant vector store -> Semantic search.

## Use Case

AI applications that need to answer questions from a custom knowledge base require a RAG pipeline. This sample shows how Surgewave's connectors (OpenAI for embeddings, Qdrant for vector storage) create a production-ready document ingestion and semantic search pipeline with built-in reliability, buffering, and monitoring.

## What It Does

- **Document Ingestion**: Load and process documents
- **Embedding Generation**: Convert text to vectors via OpenAI
- **Vector Storage**: Store embeddings in Qdrant
- **Semantic Search**: Find relevant documents by meaning
- **RAG Query**: Answer questions using retrieved context

## Prerequisites

### 1. OpenAI API Key
```bash
# Windows
set OPENAI_API_KEY=sk-...

# Linux/Mac
export OPENAI_API_KEY=sk-...
```

### 2. Qdrant Vector Database
```bash
# Start Qdrant using Docker
docker run -d --name qdrant -p 6334:6334 -p 6333:6333 qdrant/qdrant
```

## How to Run

```bash
# Interactive mode
dotnet run --project samples/RagPipeline

# Automated test mode
dotnet run --project samples/RagPipeline -- --test
```

## Why Surgewave for RAG Pipelines?

### Production RAG Architecture

```
                    ┌─────────────────────────────────────┐
                    │         Document Sources            │
                    │  (Web, Files, DBs, APIs, Streams)   │
                    └─────────────────┬───────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Surgewave Broker                                │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    Topic: documents-raw                      │   │
│  │  [doc1] [doc2] [doc3] [doc4] [doc5] ...                     │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                               │                                     │
│                               ▼                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              OpenAI Connector (Embeddings)                   │   │
│  │          • text-embedding-3-small/large                     │   │
│  │          • Batch processing (20 docs/request)               │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                               │                                     │
│                               ▼                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                 Topic: documents-embedded                    │   │
│  │  [{doc, vector}] [{doc, vector}] ...                        │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                               │                                     │
│                               ▼                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  Qdrant Connector (Sink)                     │   │
│  │          • Auto-create collections                          │   │
│  │          • HNSW indexing for fast search                    │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
                    ┌─────────────────────────────────────┐
                    │         Qdrant Vector Store          │
                    │  (Semantic Search, ~10ms queries)    │
                    └─────────────────────────────────────┘
```

### Key Benefits Over Direct Ingestion

| Feature | Direct API Calls | Surgewave Pipeline |
|---------|-----------------|----------------|
| **Scalability** | Rate limited | Buffered, parallel |
| **Reliability** | Lost on failure | Replay from topic |
| **Monitoring** | Custom logging | Built-in metrics |
| **Multi-Consumer** | Duplicate calls | Single embedding |
| **Backpressure** | Overwhelm API | Graceful queuing |

### Real-Time Document Updates

```
┌──────────────┐     ┌─────────────┐     ┌──────────────┐
│ Web Scraper  │────▶│             │────▶│              │
├──────────────┤     │   Surgewave     │     │   Qdrant     │
│ DB CDC       │────▶│   Topic     │────▶│  (Updated)   │
├──────────────┤     │             │     │              │
│ File Watcher │────▶│             │     │              │
└──────────────┘     └─────────────┘     └──────────────┘

• New documents automatically embedded and indexed
• Knowledge base always current
• No batch jobs needed
```

### Surgewave's AI Connectors

| Connector | Use Case |
|-----------|----------|
| **OpenAI** | Embeddings (text-embedding-3-small/large), Chat completions |
| **Ollama** | Local embeddings (nomic-embed-text), Local LLM (llama3) |
| **Qdrant** | Vector storage with HNSW indexing |
| **pgvector** | PostgreSQL-based vector storage |

### Comparison with Alternatives

| Approach | Latency | Reliability | Scalability | Cost |
|----------|---------|-------------|-------------|------|
| Direct API | Medium | Low | Limited | Per-call |
| LangChain | Medium | Medium | Manual | Per-call |
| LlamaIndex | Medium | Medium | Manual | Per-call |
| **Surgewave Pipeline** | **Low** | **High** | **Auto** | **Optimized** |

### Embedding Strategies

| Strategy | When to Use |
|----------|-------------|
| **Batch** | Initial load, bulk updates |
| **Stream** | Real-time document changes |
| **Hybrid** | Batch for history + stream for new |

### Chunking Options (Text Chunking Connector)

| Method | Documents |
|--------|-----------|
| Fixed Size | Any (simple) |
| Sentence | Articles, blogs |
| Paragraph | Technical docs |
| Semantic | Research papers |

### Production Considerations

| Aspect | Surgewave Approach |
|--------|----------------|
| **Rate Limiting** | Connector handles backoff |
| **Cost Control** | Batch embeddings, cache hits |
| **Versioning** | Topic per embedding model |
| **Re-indexing** | Replay from beginning |
| **Monitoring** | Built-in connector metrics |

### Sample Results

```
Semantic Search: "What is event sourcing?"

Found 3 results:
  - [Architecture] Understanding Event Sourcing (Score: 79.8%)
  - [Messaging] Introduction to Apache Kafka (Score: 31.5%)
  - [Messaging] Stream Processing with Kafka Streams (Score: 29.5%)
```

### Integration with LLMs

```csharp
// 1. Retrieve relevant documents
var results = await pipeline.SearchAsync(question, limit: 3);

// 2. Build context from results
var context = string.Join("\n\n", results.Select(r => r.Document.Content));

// 3. Call LLM with context (OpenAI, Claude, Ollama)
var prompt = $"""
    Answer based on this context:
    {context}

    Question: {question}
    """;
var answer = await llm.CompleteAsync(prompt);
```

## Key Takeaway

**Surgewave provides a production-ready RAG pipeline with built-in connectors for OpenAI, Ollama, and Qdrant - turning complex ML infrastructure into simple configuration.**
