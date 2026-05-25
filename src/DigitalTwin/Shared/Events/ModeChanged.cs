using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when equipment operating mode changes.
/// </summary>
public sealed record ModeChanged : EquipmentEvent
{
    /// <summary>Previous operating mode.</summary>
    public required OperatingMode PreviousMode { get; init; }

    /// <summary>New operating mode.</summary>
    public required OperatingMode NewMode { get; init; }

    /// <summary>Reason for the mode change.</summary>
    public string? Reason { get; init; }
}
