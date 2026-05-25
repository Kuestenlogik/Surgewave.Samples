namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when a fault condition is cleared.
/// </summary>
public sealed record FaultCleared : EquipmentEvent
{
    /// <summary>Fault code that was cleared.</summary>
    public required string FaultCode { get; init; }

    /// <summary>How the fault was resolved (auto-recovery, manual, maintenance).</summary>
    public required string Resolution { get; init; }

    /// <summary>Duration the fault was active.</summary>
    public required TimeSpan FaultDuration { get; init; }

    /// <summary>Notes about the resolution.</summary>
    public string? Notes { get; init; }
}
