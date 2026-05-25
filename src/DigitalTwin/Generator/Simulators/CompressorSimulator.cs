using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;

/// <summary>
/// Simulator for compressor equipment with air system characteristics.
/// </summary>
public sealed class CompressorSimulator : EquipmentSimulator
{
    public CompressorSimulator(Equipment equipment) : base(equipment)
    {
        // Compressors run hot and have high pressure
        BaseTemperature = 65.0;
        BaseVibration = 3.5;
        BasePressure = 8.0;
        BasePower = 45.0;
        BaseRpm = 3000;
        BaseFlowRate = 120.0; // Air flow in m³/h
        
        TemperatureNoise = 4.0;
        PressureNoise = 0.6;
    }
}
