namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when maintenance is completed.
/// </summary>
public sealed record MaintenanceCompleted : EquipmentEvent
{
    /// <summary>Work order or ticket number.</summary>
    public string? WorkOrderId { get; init; }

    /// <summary>Actual duration of the maintenance.</summary>
    public required TimeSpan ActualDuration { get; init; }

    /// <summary>Outcome of the maintenance (success, partial, deferred).</summary>
    public required string Outcome { get; init; }

    /// <summary>Notes or findings from maintenance.</summary>
    public string? Notes { get; init; }
}
