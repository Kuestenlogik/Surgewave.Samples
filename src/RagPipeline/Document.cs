using System.Text.Json.Serialization;

namespace Kuestenlogik.Surgewave.Samples.RagPipeline;

/// <summary>
/// Represents a document in the RAG pipeline.
/// </summary>
public sealed record Document
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string Source { get; init; }
    public required string Category { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public float[]? Embedding { get; set; }
}

/// <summary>
/// Search result with relevance score.
/// </summary>
public sealed record SearchResult
{
    public required Document Document { get; init; }
    public required float Score { get; init; }
}
