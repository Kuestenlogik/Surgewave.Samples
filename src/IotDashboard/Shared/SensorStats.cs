namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

/// <summary>
/// Aggregated statistics for a sensor over a time window.
/// </summary>
public sealed record SensorStats
{
    /// <summary>
    /// Sensor identifier.
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// Sensor type.
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// Location of the sensor.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Minimum value in the window.
    /// </summary>
    public required double Min { get; init; }

    /// <summary>
    /// Maximum value in the window.
    /// </summary>
    public required double Max { get; init; }

    /// <summary>
    /// Average value in the window.
    /// </summary>
    public required double Average { get; init; }

    /// <summary>
    /// Number of readings in the window.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Unit of measurement.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Start of the aggregation window.
    /// </summary>
    public required DateTimeOffset WindowStart { get; init; }

    /// <summary>
    /// End of the aggregation window.
    /// </summary>
    public required DateTimeOffset WindowEnd { get; init; }
}
