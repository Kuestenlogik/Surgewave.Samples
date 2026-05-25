using System.ClientModel;
using OpenAI;
using OpenAI.Embeddings;

namespace Kuestenlogik.Surgewave.Samples.RagPipeline;

/// <summary>
/// Service for generating embeddings using OpenAI API.
/// </summary>
public sealed class EmbeddingService : IDisposable
{
    private readonly EmbeddingClient _client;
    private readonly string _model;
    private readonly int? _dimensions;

    public EmbeddingService(string? apiKey = null, string model = "text-embedding-3-small", int? dimensions = null)
    {
        var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OpenAI API key not found. Set OPENAI_API_KEY environment variable or pass it to the constructor.");

        var openAiClient = new OpenAIClient(new ApiKeyCredential(key));
        _client = openAiClient.GetEmbeddingClient(model);
        _model = model;
        _dimensions = dimensions;
    }

    /// <summary>
    /// Generate embedding for a single text.
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var options = new EmbeddingGenerationOptions();
        if (_dimensions.HasValue)
        {
            options.Dimensions = _dimensions.Value;
        }

        var response = await _client.GenerateEmbeddingAsync(text, options, cancellationToken);
        return response.Value.ToFloats().ToArray();
    }

    /// <summary>
    /// Generate embeddings for multiple texts in a batch.
    /// </summary>
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
            return [];

        var options = new EmbeddingGenerationOptions();
        if (_dimensions.HasValue)
        {
            options.Dimensions = _dimensions.Value;
        }

        var response = await _client.GenerateEmbeddingsAsync(texts, options, cancellationToken);
        return response.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }

    /// <summary>
    /// Generate embeddings for documents and attach them.
    /// </summary>
    public async Task<int> EmbedDocumentsAsync(
        IReadOnlyList<Document> documents,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const int batchSize = 20;
        var embedded = 0;

        for (int i = 0; i < documents.Count; i += batchSize)
        {
            var batch = documents.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(d => $"{d.Title}\n\n{d.Content}").ToList();

            var embeddings = await GenerateEmbeddingsAsync(texts, cancellationToken);

            for (int j = 0; j < batch.Count; j++)
            {
                batch[j].Embedding = embeddings[j];
                embedded++;
                progress?.Report(embedded);
            }
        }

        return embedded;
    }

    public string Model => _model;
    public int? Dimensions => _dimensions;

    public void Dispose()
    {
        // OpenAI client doesn't require disposal, but interface is here for future extensibility
    }
}
