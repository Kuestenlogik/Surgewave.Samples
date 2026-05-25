using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Generator.Simulators;

/// <summary>
/// Simulator for pump equipment with flow-specific characteristics.
/// </summary>
public sealed class PumpSimulator : EquipmentSimulator
{
    public PumpSimulator(Equipment equipment) : base(equipment)
    {
        // Pumps have specific telemetry characteristics
        BaseTemperature = 45.0;
        BaseVibration = 1.8;
        BasePressure = 8.0;
        BasePower = 15.0;
        BaseRpm = 1800;
        BaseFlowRate = 80.0;
        
        PressureNoise = 0.5;
        FlowRateNoise = 8.0;
    }
}
