using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Thresholds;

/// <summary>
/// Threshold configuration for equipment telemetry monitoring.
/// </summary>
public sealed record EquipmentThresholds
{
    public required EquipmentType EquipmentType { get; init; }
    
    // Temperature thresholds (Celsius)
    public double TemperatureWarning { get; init; } = 70.0;
    public double TemperatureCritical { get; init; } = 85.0;
    
    // Vibration thresholds (mm/s)
    public double VibrationWarning { get; init; } = 4.5;
    public double VibrationCritical { get; init; } = 7.1;
    
    // Pressure thresholds (bar)
    public double PressureMinWarning { get; init; } = 2.0;
    public double PressureMaxWarning { get; init; } = 8.0;
    public double PressureMinCritical { get; init; } = 1.5;
    public double PressureMaxCritical { get; init; } = 10.0;
    
    // Power thresholds (kW)
    public double PowerWarning { get; init; } = 45.0;
    public double PowerCritical { get; init; } = 55.0;
    
    // RPM thresholds
    public double RpmMinWarning { get; init; } = 800;
    public double RpmMaxWarning { get; init; } = 3200;
    public double RpmMinCritical { get; init; } = 500;
    public double RpmMaxCritical { get; init; } = 3600;
    
    // Flow rate thresholds (m³/h)
    public double FlowRateMinWarning { get; init; } = 10.0;
    public double FlowRateMaxWarning { get; init; } = 90.0;
    
    // Rapid change thresholds (% change per second)
    public double RapidChangeThreshold { get; init; } = 15.0;
    
    /// <summary>
    /// Get default thresholds for an equipment type.
    /// </summary>
    public static EquipmentThresholds GetDefaults(EquipmentType type) => type switch
    {
        EquipmentType.Pump => new EquipmentThresholds
        {
            EquipmentType = type,
            TemperatureWarning = 65.0,
            TemperatureCritical = 80.0,
            PressureMaxWarning = 12.0,
            PressureMaxCritical = 15.0,
            FlowRateMinWarning = 20.0,
            FlowRateMaxWarning = 150.0
        },
        EquipmentType.Motor => new EquipmentThresholds
        {
            EquipmentType = type,
            TemperatureWarning = 75.0,
            TemperatureCritical = 90.0,
            RpmMinWarning = 1000,
            RpmMaxWarning = 3000,
            PowerWarning = 50.0,
            PowerCritical = 60.0
        },
        EquipmentType.Conveyor => new EquipmentThresholds
        {
            EquipmentType = type,
            TemperatureWarning = 50.0,
            TemperatureCritical = 65.0,
            VibrationWarning = 3.0,
            VibrationCritical = 5.0,
            RpmMinWarning = 50,
            RpmMaxWarning = 200
        },
        EquipmentType.Compressor => new EquipmentThresholds
        {
            EquipmentType = type,
            TemperatureWarning = 80.0,
            TemperatureCritical = 95.0,
            PressureMaxWarning = 10.0,
            PressureMaxCritical = 12.0,
            VibrationWarning = 5.0,
            VibrationCritical = 8.0
        },
        _ => new EquipmentThresholds { EquipmentType = type }
    };
}
