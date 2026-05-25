using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Anomalies;

/// <summary>
/// Represents a detected anomaly in equipment telemetry.
/// </summary>
public sealed record Anomaly
{
    public required string Id { get; init; }
    public required string EquipmentId { get; init; }
    public required DateTime DetectedAt { get; init; }
    public required AnomalyType Type { get; init; }
    public required AnomalySeverity Severity { get; init; }
    public required TelemetryType AffectedMetric { get; init; }
    public required double CurrentValue { get; init; }
    public double? ThresholdValue { get; init; }
    public required string Description { get; init; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
