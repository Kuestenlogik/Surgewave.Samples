using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Anomalies;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Dashboard.Services;

/// <summary>
/// Current state of equipment including latest telemetry and events.
/// </summary>
public sealed class EquipmentState
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required EquipmentType Type { get; init; }
    public required string Zone { get; init; }
    public required double PositionX { get; init; }
    public required double PositionY { get; init; }
    public required double PositionZ { get; init; }
    
    public OperatingMode Mode { get; set; } = OperatingMode.Stopped;
    public TelemetryReading? LatestTelemetry { get; set; }
    public EquipmentEvent? LatestEvent { get; set; }
    public List<Anomaly> ActiveAnomalies { get; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Telemetry history for trend analysis (last 60 readings = 30 seconds at 500ms)
    private readonly Queue<TelemetryReading> _telemetryHistory = new();
    private const int MaxHistorySize = 60;
    
    public void UpdateTelemetry(TelemetryReading reading)
    {
        LatestTelemetry = reading;
        LastUpdated = DateTime.UtcNow;
        
        _telemetryHistory.Enqueue(reading);
        while (_telemetryHistory.Count > MaxHistorySize)
            _telemetryHistory.Dequeue();
    }
    
    public IReadOnlyList<TelemetryReading> TelemetryHistory => _telemetryHistory.ToList();
    
    public static EquipmentState FromEquipment(Equipment equipment) => new()
    {
        Id = equipment.Id,
        Name = equipment.Name,
        Type = equipment.Type,
        Zone = equipment.Zone,
        PositionX = equipment.PositionX,
        PositionY = equipment.PositionY,
        PositionZ = equipment.PositionZ,
        Mode = equipment.Mode
    };
}
