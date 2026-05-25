using System.Text.Json.Serialization;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Anomalies;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;

/// <summary>
/// Base class for equipment state change events.
/// Uses polymorphic JSON serialization.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(EquipmentStartedEvent), "started")]
[JsonDerivedType(typeof(EquipmentStoppedEvent), "stopped")]
[JsonDerivedType(typeof(MaintenanceStartedEvent), "maintenance_started")]
[JsonDerivedType(typeof(MaintenanceCompletedEvent), "maintenance_completed")]
[JsonDerivedType(typeof(FaultDetectedEvent), "fault_detected")]
[JsonDerivedType(typeof(FaultClearedEvent), "fault_cleared")]
[JsonDerivedType(typeof(ModeChangedEvent), "mode_changed")]
public abstract record EquipmentEvent
{
    public required string EventId { get; init; }
    public required string EquipmentId { get; init; }
    public required DateTime Timestamp { get; init; }
}

public sealed record EquipmentStartedEvent : EquipmentEvent
{
    public required OperatingMode PreviousMode { get; init; }
}

public sealed record EquipmentStoppedEvent : EquipmentEvent
{
    public required string Reason { get; init; }
}

public sealed record MaintenanceStartedEvent : EquipmentEvent
{
    public required string MaintenanceType { get; init; }
    public required string Technician { get; init; }
}

public sealed record MaintenanceCompletedEvent : EquipmentEvent
{
    public required TimeSpan Duration { get; init; }
    public required string Notes { get; init; }
}

public sealed record FaultDetectedEvent : EquipmentEvent
{
    public required string FaultCode { get; init; }
    public required string Description { get; init; }
    public required AnomalySeverity Severity { get; init; }
}

public sealed record FaultClearedEvent : EquipmentEvent
{
    public required string FaultCode { get; init; }
    public required string Resolution { get; init; }
}

public sealed record ModeChangedEvent : EquipmentEvent
{
    public required OperatingMode OldMode { get; init; }
    public required OperatingMode NewMode { get; init; }
}
