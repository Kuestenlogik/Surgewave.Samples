namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Dashboard.Services;

/// <summary>
/// A snapshot of all equipment states at a point in time.
/// Used for time-travel replay functionality.
/// </summary>
public sealed class TwinSnapshot
{
    public required long TelemetryOffset { get; init; }
    public required long EventOffset { get; init; }
    public required DateTime Timestamp { get; init; }
    public required Dictionary<string, EquipmentState> States { get; init; }
}
