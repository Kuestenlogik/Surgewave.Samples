# RAG with Surgewave.AI

A complete RAG (Retrieval-Augmented Generation) demo using Surgewave.AI's built-in libraries. No API keys, no Docker containers, no external services required.

This is the Surgewave.AI counterpart to the `RagPipeline` sample. While that sample uses OpenAI embeddings and a Qdrant vector database, this sample runs everything locally using Surgewave.AI components.

## Use Case

Development teams need to prototype and test RAG pipelines without API costs or infrastructure. This sample demonstrates Surgewave.AI's batteries-included RAG toolkit -- BM25 + semantic + hybrid retrieval, cross-encoder reranking, prompt templates, and evaluation metrics -- all running locally with zero external dependencies.

## How to Run

```bash
# Interactive mode (includes search loop at the end)
dotnet run --project src/RagWithSurgewaveAI

# Automated test mode (runs all steps and exits)
dotnet run --project src/RagWithSurgewaveAI -- --test
```

No prerequisites. No API keys. No Docker. Just run it.

## What It Demonstrates

### 1. Document Processing
- Creates 8 sample documents about Surgewave architecture, event sourcing, stream processing, etc.
- Splits documents into chunks using `SentenceSplitter` and `RecursiveCharacterSplitter`
- Shows chunk count and size statistics

### 2. Indexing
- Indexes chunks into both a **BM25 inverted index** and an in-memory **VectorStore**
- Uses a hash-based embedder (deterministic, no API needed) to generate 64-dimensional vectors
- Uses `DocumentIndexer` for unified dual-store indexing

### 3. Retrieval
Compares three retrieval strategies on the same query:
- **BM25 (Keyword)** -- term-frequency based search with TF-IDF weighting
- **Semantic (Vector)** -- cosine similarity on embedding vectors
- **Hybrid (RRF)** -- combines BM25 + Semantic using Reciprocal Rank Fusion

### 4. Reranking
- Uses `CrossEncoderReranker` with `KeywordOverlapScorer` to reorder results
- Shows how position-weighted Jaccard similarity improves relevance ordering

### 5. Prompt Building
- `TemplatePromptBuilder` -- simple `{context}` / `{query}` substitution for RAG pipelines
- `RagTemplates` -- four built-in templates (Default, Strict, Creative, Conversational)
- `PromptTemplate.Parse` -- custom templates with `{{#if}}`, `{{#each}}`, message blocks
- Shows generated prompts that would be sent to an LLM

### 6. Evaluation
Runs 6 rule-based metrics (no LLM judge needed):
| Metric | What it Measures |
|--------|-----------------|
| **Faithfulness** | Is the answer grounded in the provided contexts? |
| **Answer Relevancy** | Does the answer address the question? |
| **Context Precision** | Are the retrieved contexts relevant to the question? |
| **Context Recall** | Do the contexts cover the ground truth? |
| **Answer Correctness** | How similar is the answer to the ground truth? (F1) |
| **Answer Completeness** | Does the answer cover all question keywords? |

Displays aggregated statistics: mean, median, min, max, standard deviation.

### 7. End-to-End Pipeline
Builds a `RagPipeline` using the builder API:
```
Embedder -> Retriever -> Reranker -> PromptBuilder
```
Executes a query through all stages and shows the final constructed prompt.

### 8. Interactive Search
After the automated steps, enters an interactive loop where you type questions and see retrieved passages with constructed prompts.

## Comparison: RagPipeline vs RagWithSurgewaveAI

| Feature | RagPipeline (direct API) | RagWithSurgewaveAI (this demo) |
|---------|-------------------------|---------------------------|
| **API Keys** | Yes (OpenAI, Qdrant) | None required |
| **Vector Store** | Qdrant (Docker) | In-memory (built-in) |
| **Embeddings** | OpenAI API ($) | Local / pluggable |
| **Retrieval** | Simple similarity | BM25 + Semantic + Hybrid |
| **Prompt Templates** | Manual string concat | TemplateParser engine |
| **Built-in Templates** | None | 4 RAG templates |
| **Evaluation** | None | 6 metrics built-in |
| **Reranking** | None | CrossEncoderReranker |
| **Document Splitting** | None | Sentence + Recursive |
| **Score Fusion** | None | RRF + Weighted Sum |

## Surgewave.AI Components Used

| Package | Components |
|---------|-----------|
| `Kuestenlogik.Surgewave.AI.Documents` | `SentenceSplitter`, `RecursiveCharacterSplitter`, `Document`, `DocumentChunk` |
| `Kuestenlogik.Surgewave.AI.Retrieval` | `Bm25Retriever`, `SemanticRetriever`, `HybridRetriever`, `VectorStore`, `DocumentIndexer`, `CrossEncoderReranker`, `KeywordOverlapScorer` |
| `Kuestenlogik.Surgewave.AI.Rag` | `RagPipeline`, `RagPipelineBuilder`, `InMemoryRetriever`, `TemplatePromptBuilder`, `SimpleReranker`, `IEmbedder` |
| `Kuestenlogik.Surgewave.AI.Prompts` | `PromptTemplate`, `TemplateParser`, `RagTemplates` |
| `Kuestenlogik.Surgewave.AI.Evaluation` | `EvaluationRunner`, `FaithfulnessMetric`, `AnswerRelevancyMetric`, `ContextPrecisionMetric`, `ContextRecallMetric`, `AnswerCorrectnessMetric`, `AnswerCompletenessMetric` |

## Sample Output

```
Step 1: Document Processing
  8 documents loaded
  14 chunks created with SentenceSplitter (max 500 chars)
  12 chunks created with RecursiveCharacterSplitter (400 chars, 50 overlap)

Step 2: Indexing into BM25 + Vector Store
  BM25 index:    14 documents indexed
  Vector store:  14 vectors stored (64-dim hash embeddings)
  Index time:    3ms

Step 3: Retrieval
  Query: "How does event sourcing store state changes?"

  BM25 (Keyword):
    1. 3.1234 Event sourcing is an architectural pattern where state changes...
    2. 1.8721 CQRS (Command Query Responsibility Segregation) separates...

  Semantic (Vector):
    1. 0.8923 Event sourcing is an architectural pattern where state changes...
    2. 0.7245 Key benefits of event sourcing include: complete audit trails...

  Hybrid (BM25 + Semantic, RRF):
    1. 0.0328 Event sourcing is an architectural pattern where state changes...
    2. 0.0311 Key benefits of event sourcing include: complete audit trails...

Step 6: RAG Evaluation
  Metric           Mean    Median  Min     Max     StdDev
  faithfulness     83.3%   100.0%  50.0%   100.0%  0.236
  answer_relevancy 21.5%   20.8%   15.2%   28.6%   0.056
  context_precision 8.7%   8.3%    5.2%    12.5%   0.030
  answer_correctness 42.1% 40.0%  38.1%   48.3%   0.044

  Overall score: 38.2%
```

## Key Takeaway

Surgewave.AI provides a batteries-included RAG toolkit that runs entirely locally. No API keys, no external services, no containers -- just `dotnet run`. Swap in real embeddings and an LLM when you're ready for production.
