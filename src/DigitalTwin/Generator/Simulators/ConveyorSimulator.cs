using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;

/// <summary>
/// Simulator for conveyor equipment with material handling characteristics.
/// </summary>
public sealed class ConveyorSimulator : EquipmentSimulator
{
    public ConveyorSimulator(Equipment equipment) : base(equipment)
    {
        // Conveyors run cooler but vibration is important
        BaseTemperature = 35.0;
        BaseVibration = 3.0;
        BasePressure = 1.0; // N/A
        BasePower = 10.0;
        BaseRpm = 120; // Belt speed RPM equivalent
        BaseFlowRate = 0; // N/A
        
        VibrationNoise = 0.8;
        TemperatureNoise = 1.5;
    }
}
