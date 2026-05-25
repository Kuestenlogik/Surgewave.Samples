namespace Kuestenlogik.Surgewave.Samples.NativeClient;

/// <summary>
/// Sample order event demonstrating a real-world message type.
/// </summary>
public sealed record OrderEvent
{
    /// <summary>
    /// Unique order identifier.
    /// </summary>
    public required string OrderId { get; init; }

    /// <summary>
    /// Customer who placed the order.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// Product being ordered.
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Total price in cents.
    /// </summary>
    public required long TotalCents { get; init; }

    /// <summary>
    /// Order status (Created, Confirmed, Shipped, Delivered).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}
