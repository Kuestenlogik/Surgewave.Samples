using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when equipment is stopped.
/// </summary>
public sealed record EquipmentStopped : EquipmentEvent
{
    /// <summary>Previous operating mode before stopping.</summary>
    public required OperatingMode PreviousMode { get; init; }

    /// <summary>Reason for stopping (manual, scheduled, fault, maintenance).</summary>
    public string? StopReason { get; init; }

    /// <summary>Duration equipment was running before stop, if applicable.</summary>
    public TimeSpan? RunDuration { get; init; }
}
