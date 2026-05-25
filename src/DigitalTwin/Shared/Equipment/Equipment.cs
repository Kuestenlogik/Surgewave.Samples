namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

/// <summary>
/// Represents an industrial equipment unit in the factory.
/// </summary>
public sealed record Equipment
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required EquipmentType Type { get; init; }
    public required string Zone { get; init; }
    public required double PositionX { get; init; }
    public required double PositionY { get; init; }
    public required double PositionZ { get; init; }
    public OperatingMode Mode { get; set; } = OperatingMode.Stopped;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
