using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;

/// <summary>
/// Simulator for motor equipment with drive-specific characteristics.
/// </summary>
public sealed class MotorSimulator : EquipmentSimulator
{
    public MotorSimulator(Equipment equipment) : base(equipment)
    {
        // Motors run hotter and have higher RPM
        BaseTemperature = 55.0;
        BaseVibration = 2.5;
        BasePressure = 1.0; // N/A for motors
        BasePower = 35.0;
        BaseRpm = 2400;
        BaseFlowRate = 0; // N/A for motors
        
        TemperatureNoise = 3.0;
        RpmNoise = 100;
    }
}
