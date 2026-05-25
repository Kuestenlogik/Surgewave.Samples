namespace Kuestenlogik.Surgewave.Samples.RagPipeline;

/// <summary>
/// RAG (Retrieval-Augmented Generation) pipeline that combines
/// document ingestion, embedding generation, vector storage, and semantic search.
/// </summary>
public sealed class RagPipeline : IAsyncDisposable
{
    private readonly EmbeddingService _embeddingService;
    private readonly VectorStore _vectorStore;

    public RagPipeline(
        EmbeddingService embeddingService,
        VectorStore vectorStore)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Ingest documents into the pipeline: generate embeddings and store in vector DB.
    /// </summary>
    public async Task<int> IngestDocumentsAsync(
        IReadOnlyList<Document> documents,
        IProgress<(int current, int total, string status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return 0;

        // Step 1: Generate embeddings
        progress?.Report((0, documents.Count, "Generating embeddings..."));

        var embeddingProgress = new Progress<int>(current =>
            progress?.Report((current, documents.Count, $"Embedding {current}/{documents.Count}...")));

        await _embeddingService.EmbedDocumentsAsync(documents, embeddingProgress, cancellationToken);

        // Step 2: Store in vector database
        progress?.Report((documents.Count, documents.Count, "Storing in vector database..."));
        await _vectorStore.UpsertDocumentsAsync(documents, cancellationToken);

        progress?.Report((documents.Count, documents.Count, "Complete"));
        return documents.Count;
    }

    /// <summary>
    /// Search for documents similar to the query.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int limit = 5,
        float? scoreThreshold = null,
        string? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        // Generate embedding for the query
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Search in vector store
        return await _vectorStore.SearchAsync(
            queryVector,
            limit,
            scoreThreshold,
            categoryFilter,
            cancellationToken);
    }

    /// <summary>
    /// Get context for a query (formatted for LLM consumption).
    /// </summary>
    public async Task<string> GetContextAsync(
        string query,
        int limit = 3,
        float? scoreThreshold = 0.7f,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, limit, scoreThreshold, null, cancellationToken);

        if (results.Count == 0)
            return "No relevant documents found.";

        var context = string.Join("\n\n---\n\n", results.Select((r, i) =>
            $"[Document {i + 1}: {r.Document.Title}]\n{r.Document.Content}"));

        return context;
    }

    /// <summary>
    /// Generate an answer using retrieved context (for demo purposes, just shows the context).
    /// In a full implementation, this would call an LLM with the context.
    /// </summary>
    public async Task<(string answer, IReadOnlyList<SearchResult> sources)> AnswerAsync(
        string question,
        int contextDocuments = 3,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(question, contextDocuments, 0.5f, null, cancellationToken);

        if (results.Count == 0)
        {
            return ("I couldn't find any relevant information to answer your question.", []);
        }

        // In a real RAG implementation, you would:
        // 1. Format the context from retrieved documents
        // 2. Create a prompt like: "Answer the question based on the following context: {context}\n\nQuestion: {question}"
        // 3. Call an LLM (GPT-4, Claude, etc.) with this prompt
        // 4. Return the LLM's response along with the source documents

        // For this demo, we'll return a summary of what was found
        var topResult = results[0];
        var answer = $"Based on the document \"{topResult.Document.Title}\" (relevance: {topResult.Score:P0}):\n\n" +
            $"{topResult.Document.Content[..Math.Min(500, topResult.Document.Content.Length)]}...";

        return (answer, results);
    }

    /// <summary>
    /// Reset the vector store (delete all documents).
    /// </summary>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        return _vectorStore.ResetCollectionAsync(cancellationToken);
    }

    /// <summary>
    /// Get statistics about the stored documents.
    /// </summary>
    public Task<(ulong pointsCount, ulong vectorsCount)> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        return _vectorStore.GetStatsAsync(cancellationToken);
    }

    /// <summary>
    /// List all documents in the store.
    /// </summary>
    public Task<IReadOnlyList<Document>> ListDocumentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _vectorStore.ListDocumentsAsync(limit, cancellationToken);
    }

    public EmbeddingService EmbeddingService => _embeddingService;
    public VectorStore VectorStore => _vectorStore;

    public async ValueTask DisposeAsync()
    {
        _embeddingService.Dispose();
        await _vectorStore.DisposeAsync();
    }
}
