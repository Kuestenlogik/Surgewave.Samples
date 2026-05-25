namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;

/// <summary>
/// A single telemetry reading from equipment sensors.
/// </summary>
public sealed record TelemetryReading
{
    public required string EquipmentId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required double Temperature { get; init; }
    public required double Vibration { get; init; }
    public required double Pressure { get; init; }
    public required double Power { get; init; }
    public required double Rpm { get; init; }
    public required double FlowRate { get; init; }
}
