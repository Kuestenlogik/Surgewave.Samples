using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Anomalies;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;

/// <summary>
/// Base class for equipment simulators that generate telemetry and events.
/// </summary>
public abstract class EquipmentSimulator
{
    public Equipment Equipment { get; }
    private readonly Random _random = new();
    
    // Base values for telemetry (modified by operating mode)
    protected double BaseTemperature = 40.0;
    protected double BaseVibration = 2.0;
    protected double BasePressure = 5.0;
    protected double BasePower = 20.0;
    protected double BaseRpm = 1500;
    protected double BaseFlowRate = 50.0;
    
    // Noise factors for realistic variation
    protected double TemperatureNoise = 2.0;
    protected double VibrationNoise = 0.5;
    protected double PressureNoise = 0.3;
    protected double PowerNoise = 1.0;
    protected double RpmNoise = 50;
    protected double FlowRateNoise = 5.0;
    
    // Event generation probability (per tick)
    private int _ticksSinceLastEvent;
    private const int MinTicksBetweenEvents = 60; // ~30 seconds at 500ms interval
    
    protected EquipmentSimulator(Equipment equipment)
    {
        Equipment = equipment;
    }
    
    /// <summary>
    /// Generate a telemetry reading based on current operating mode.
    /// </summary>
    public TelemetryReading GenerateTelemetry()
    {
        var modeMultiplier = GetModeMultiplier();
        
        return new TelemetryReading
        {
            EquipmentId = Equipment.Id,
            Timestamp = DateTime.UtcNow,
            Temperature = GenerateValue(BaseTemperature * modeMultiplier, TemperatureNoise),
            Vibration = GenerateValue(BaseVibration * modeMultiplier, VibrationNoise),
            Pressure = GenerateValue(BasePressure * modeMultiplier, PressureNoise),
            Power = GenerateValue(BasePower * modeMultiplier, PowerNoise),
            Rpm = Equipment.Mode == OperatingMode.Running ? GenerateValue(BaseRpm, RpmNoise) : 0,
            FlowRate = Equipment.Mode == OperatingMode.Running ? GenerateValue(BaseFlowRate * modeMultiplier, FlowRateNoise) : 0
        };
    }
    
    /// <summary>
    /// Try to generate a state change event (probabilistic).
    /// </summary>
    public EquipmentEvent? TryGenerateEvent()
    {
        _ticksSinceLastEvent++;
        
        if (_ticksSinceLastEvent < MinTicksBetweenEvents)
            return null;
        
        // 2% chance per tick after minimum interval
        if (_random.NextDouble() > 0.02)
            return null;
        
        _ticksSinceLastEvent = 0;
        return GenerateRandomEvent();
    }
    
    protected virtual EquipmentEvent? GenerateRandomEvent()
    {
        var eventType = _random.Next(0, 10);
        var timestamp = DateTime.UtcNow;
        var eventId = Guid.NewGuid().ToString("N")[..8];
        
        return eventType switch
        {
            0 or 1 when Equipment.Mode != OperatingMode.Running => new EquipmentStartedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                PreviousMode = Equipment.Mode
            },
            2 when Equipment.Mode == OperatingMode.Running => new EquipmentStoppedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                Reason = "Scheduled shutdown"
            },
            3 when Equipment.Mode == OperatingMode.Stopped => new MaintenanceStartedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                MaintenanceType = "Preventive",
                Technician = $"Tech-{_random.Next(1, 10):D2}"
            },
            4 when Equipment.Mode == OperatingMode.Maintenance => new MaintenanceCompletedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                Duration = TimeSpan.FromMinutes(_random.Next(15, 120)),
                Notes = "Maintenance completed successfully"
            },
            5 when Equipment.Mode == OperatingMode.Running => new FaultDetectedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                FaultCode = $"F{_random.Next(100, 999)}",
                Description = GetRandomFaultDescription(),
                Severity = _random.NextDouble() > 0.7 ? AnomalySeverity.Critical : AnomalySeverity.Warning
            },
            6 when Equipment.Mode == OperatingMode.Fault => new FaultClearedEvent
            {
                EventId = eventId,
                EquipmentId = Equipment.Id,
                Timestamp = timestamp,
                FaultCode = $"F{_random.Next(100, 999)}",
                Resolution = "Operator intervention"
            },
            _ => null
        };
    }
    
    /// <summary>
    /// Apply an event to update the equipment state.
    /// </summary>
    public void ApplyEvent(EquipmentEvent evt)
    {
        var oldMode = Equipment.Mode;
        Equipment.Mode = evt switch
        {
            EquipmentStartedEvent => OperatingMode.Running,
            EquipmentStoppedEvent => OperatingMode.Stopped,
            MaintenanceStartedEvent => OperatingMode.Maintenance,
            MaintenanceCompletedEvent => OperatingMode.Stopped,
            FaultDetectedEvent => OperatingMode.Fault,
            FaultClearedEvent => OperatingMode.Stopped,
            _ => Equipment.Mode
        };
        Equipment.LastUpdated = DateTime.UtcNow;
    }
    
    protected double GenerateValue(double baseValue, double noise)
    {
        return baseValue + (_random.NextDouble() * 2 - 1) * noise;
    }
    
    protected double GetModeMultiplier() => Equipment.Mode switch
    {
        OperatingMode.Stopped => 0.3,
        OperatingMode.Starting => 0.7,
        OperatingMode.Running => 1.0,
        OperatingMode.Stopping => 0.6,
        OperatingMode.Maintenance => 0.2,
        OperatingMode.Fault => 1.3,
        _ => 1.0
    };
    
    private string GetRandomFaultDescription()
    {
        var faults = new[]
        {
            "High temperature detected",
            "Excessive vibration",
            "Pressure anomaly",
            "Power fluctuation",
            "Bearing wear detected",
            "Seal leakage suspected"
        };
        return faults[_random.Next(faults.Length)];
    }
}
