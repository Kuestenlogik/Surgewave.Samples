#pragma warning disable CA1859 // Use concrete types for better performance

using Google.Protobuf.Collections;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Kuestenlogik.Surgewave.Samples.RagPipeline;

/// <summary>
/// Vector store service using Qdrant for similarity search.
/// </summary>
public sealed class VectorStore : IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;
    private readonly int _vectorSize;
    private bool _collectionCreated;

    public VectorStore(
        string host = "localhost",
        int port = 6334,
        string collectionName = "rag-documents",
        int vectorSize = 1536)
    {
        _client = new QdrantClient(host, port);
        _collectionName = collectionName;
        _vectorSize = vectorSize;
    }

    /// <summary>
    /// Ensure the collection exists, creating it if necessary.
    /// </summary>
    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionCreated)
            return;

        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (collections.Contains(_collectionName))
        {
            _collectionCreated = true;
            return;
        }

        await _client.CreateCollectionAsync(
            _collectionName,
            new VectorParams
            {
                Size = (ulong)_vectorSize,
                Distance = Distance.Cosine
            },
            cancellationToken: cancellationToken);

        _collectionCreated = true;
    }

    /// <summary>
    /// Delete and recreate the collection.
    /// </summary>
    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        var collections = await _client.ListCollectionsAsync(cancellationToken);
        if (collections.Contains(_collectionName))
        {
            await _client.DeleteCollectionAsync(_collectionName, cancellationToken: cancellationToken);
        }

        _collectionCreated = false;
        await EnsureCollectionAsync(cancellationToken);
    }

    /// <summary>
    /// Store documents with their embeddings.
    /// </summary>
    public async Task UpsertDocumentsAsync(
        IReadOnlyList<Document> documents,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var points = documents
            .Where(d => d.Embedding != null)
            .Select(d => new PointStruct
            {
                Id = CreatePointId(d.Id),
                Vectors = d.Embedding!,
                Payload =
                {
                    ["id"] = d.Id,
                    ["title"] = d.Title,
                    ["content"] = d.Content,
                    ["source"] = d.Source,
                    ["category"] = d.Category,
                    ["created_at"] = d.CreatedAt.ToUnixTimeMilliseconds()
                }
            })
            .ToList();

        if (points.Count > 0)
        {
            await _client.UpsertAsync(_collectionName, points, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Search for similar documents by vector.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit = 5,
        float? scoreThreshold = null,
        string? categoryFilter = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        Filter? filter = null;
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "category",
                            Match = new Match { Keyword = categoryFilter }
                        }
                    }
                }
            };
        }

        var results = await _client.SearchAsync(
            _collectionName,
            queryVector,
            limit: (ulong)limit,
            scoreThreshold: scoreThreshold,
            filter: filter,
            cancellationToken: cancellationToken);

        return results.Select(r => new SearchResult
        {
            Document = new Document
            {
                Id = GetPayloadString(r.Payload, "id"),
                Title = GetPayloadString(r.Payload, "title"),
                Content = GetPayloadString(r.Payload, "content"),
                Source = GetPayloadString(r.Payload, "source"),
                Category = GetPayloadString(r.Payload, "category"),
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(GetPayloadLong(r.Payload, "created_at"))
            },
            Score = r.Score
        }).ToList();
    }

    /// <summary>
    /// Get collection statistics.
    /// </summary>
    public async Task<(ulong pointsCount, ulong vectorsCount)> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        var info = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken: cancellationToken);
        return (info.PointsCount, info.PointsCount);
    }

    /// <summary>
    /// List all documents in the collection by performing a search with high limit.
    /// </summary>
    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionAsync(cancellationToken);

        // Use scroll to get all points
        var scrollResponse = await _client.ScrollAsync(
            _collectionName,
            limit: (uint)limit,
            cancellationToken: cancellationToken);

        var documents = new List<Document>();
        foreach (var p in scrollResponse.Result)
        {
            documents.Add(new Document
            {
                Id = GetPayloadString(p.Payload, "id"),
                Title = GetPayloadString(p.Payload, "title"),
                Content = GetPayloadString(p.Payload, "content"),
                Source = GetPayloadString(p.Payload, "source"),
                Category = GetPayloadString(p.Payload, "category"),
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(GetPayloadLong(p.Payload, "created_at"))
            });
        }
        return documents;
    }

    private static PointId CreatePointId(string id)
    {
        // Create deterministic UUID from document ID
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(id));
        var guid = new Guid(hash.AsSpan(0, 16));
        return new PointId { Uuid = guid.ToString() };
    }

    private static string GetPayloadString(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.StringValue : "";
    }

    private static long GetPayloadLong(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.IntegerValue : 0;
    }

    public string CollectionName => _collectionName;
    public int VectorSize => _vectorSize;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }
}
