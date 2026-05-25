namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

/// <summary>
/// Threshold configuration for sensor alerts.
/// </summary>
public static class SensorThresholds
{
    public static readonly Dictionary<SensorType, (double WarningLow, double WarningHigh, double CriticalLow, double CriticalHigh)> Values = new()
    {
        [SensorType.Temperature] = (15.0, 28.0, 10.0, 35.0),    // °C
        [SensorType.Humidity] = (30.0, 60.0, 20.0, 80.0),       // %
        [SensorType.Pressure] = (980.0, 1040.0, 960.0, 1060.0), // hPa
        [SensorType.CO2] = (0, 800.0, 0, 1200.0),               // ppm
        [SensorType.Light] = (100.0, 800.0, 50.0, 1000.0)       // lux
    };

    public static AlertSeverity? CheckThreshold(SensorType type, double value)
    {
        if (!Values.TryGetValue(type, out var thresholds))
            return null;

        if (value < thresholds.CriticalLow || value > thresholds.CriticalHigh)
            return AlertSeverity.Critical;

        if (value < thresholds.WarningLow || value > thresholds.WarningHigh)
            return AlertSeverity.Warning;

        return null;
    }

    public static string GetUnit(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Humidity => "%",
        SensorType.Pressure => "hPa",
        SensorType.CO2 => "ppm",
        SensorType.Light => "lux",
        _ => ""
    };
}
