using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when equipment is started.
/// </summary>
public sealed record EquipmentStarted : EquipmentEvent
{
    /// <summary>Previous operating mode before starting.</summary>
    public required OperatingMode PreviousMode { get; init; }

    /// <summary>Reason for starting (manual, scheduled, automatic).</summary>
    public string? StartReason { get; init; }
}
