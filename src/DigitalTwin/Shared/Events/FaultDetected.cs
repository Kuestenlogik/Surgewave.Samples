namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Event raised when a fault condition is detected.
/// </summary>
public sealed record FaultDetected : EquipmentEvent
{
    /// <summary>Fault code identifier.</summary>
    public required string FaultCode { get; init; }

    /// <summary>Severity level (warning, critical, emergency).</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable fault description.</summary>
    public required string Description { get; init; }

    /// <summary>Telemetry type that triggered the fault, if applicable.</summary>
    public string? TriggerMetric { get; init; }

    /// <summary>Value that triggered the fault.</summary>
    public double? TriggerValue { get; init; }

    /// <summary>Threshold that was exceeded.</summary>
    public double? ThresholdValue { get; init; }
}
