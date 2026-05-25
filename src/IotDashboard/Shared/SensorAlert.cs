namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

/// <summary>
/// Alert generated when sensor readings exceed thresholds.
/// </summary>
public sealed record SensorAlert
{
    /// <summary>
    /// Unique alert identifier.
    /// </summary>
    public required string AlertId { get; init; }

    /// <summary>
    /// Sensor that triggered the alert.
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// Type of sensor.
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// Location of the sensor.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Alert severity level.
    /// </summary>
    public required AlertSeverity Severity { get; init; }

    /// <summary>
    /// Alert message describing the issue.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The value that triggered the alert.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// The threshold that was exceeded.
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// Unit of measurement.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// When the alert was generated.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Severity levels for alerts.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
