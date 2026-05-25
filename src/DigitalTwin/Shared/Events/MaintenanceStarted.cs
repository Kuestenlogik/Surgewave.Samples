namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when equipment enters maintenance mode.
/// </summary>
public sealed record MaintenanceStarted : EquipmentEvent
{
    /// <summary>Type of maintenance (preventive, corrective, inspection).</summary>
    public required string MaintenanceType { get; init; }

    /// <summary>Work order or ticket number.</summary>
    public string? WorkOrderId { get; init; }

    /// <summary>Estimated duration for the maintenance.</summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>Description of maintenance activities.</summary>
    public string? Description { get; init; }
}
