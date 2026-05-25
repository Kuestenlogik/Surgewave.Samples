using Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Generator;

/// <summary>
/// Simulates IoT sensor readings with realistic variations.
/// </summary>
#pragma warning disable CA5394 // Random is fine for sensor simulation
public sealed class SensorSimulator
{
    private readonly string _sensorId;
    private readonly SensorType _type;
    private readonly string _location;
    private readonly string _unit;
    private readonly double _baseValue;
    private readonly double _variance;
    private readonly Random _random;

    private double _currentValue;
    private double _trend;

    public string SensorId => _sensorId;
    public SensorType Type => _type;
    public string Location => _location;

    public SensorSimulator(string sensorId, SensorType type, string location, int seed)
    {
        _sensorId = sensorId;
        _type = type;
        _location = location;
        _unit = SensorThresholds.GetUnit(type);
        _random = new Random(seed);

        // Set base values and variance based on sensor type
        (_baseValue, _variance) = type switch
        {
            SensorType.Temperature => (22.0, 3.0),      // 22°C ± 3
            SensorType.Humidity => (45.0, 10.0),        // 45% ± 10
            SensorType.Pressure => (1013.0, 15.0),      // 1013 hPa ± 15
            SensorType.CO2 => (450.0, 150.0),           // 450 ppm ± 150
            SensorType.Light => (400.0, 200.0),         // 400 lux ± 200
            _ => (50.0, 10.0)
        };

        _currentValue = _baseValue + (_random.NextDouble() - 0.5) * _variance;
        _trend = 0;
    }

    /// <summary>
    /// Generate the next sensor reading with smooth transitions.
    /// </summary>
    public SensorReading NextReading()
    {
        // Apply random walk with mean reversion
        _trend = _trend * 0.9 + (_random.NextDouble() - 0.5) * 0.2;

        // Add some noise and drift
        var noise = (_random.NextDouble() - 0.5) * _variance * 0.1;
        var meanReversion = (_baseValue - _currentValue) * 0.02;

        _currentValue += _trend + noise + meanReversion;

        // Occasionally spike to trigger alerts (1% chance)
        if (_random.NextDouble() < 0.01)
        {
            var spikeDirection = _random.NextDouble() < 0.5 ? -1 : 1;
            _currentValue += spikeDirection * _variance * 1.5;
        }

        // Clamp to reasonable ranges
        _currentValue = _type switch
        {
            SensorType.Temperature => Math.Clamp(_currentValue, 5.0, 40.0),
            SensorType.Humidity => Math.Clamp(_currentValue, 10.0, 95.0),
            SensorType.Pressure => Math.Clamp(_currentValue, 950.0, 1070.0),
            SensorType.CO2 => Math.Clamp(_currentValue, 300.0, 1500.0),
            SensorType.Light => Math.Clamp(_currentValue, 0.0, 1200.0),
            _ => _currentValue
        };

        return new SensorReading
        {
            SensorId = _sensorId,
            Type = _type,
            Value = Math.Round(_currentValue, 2),
            Unit = _unit,
            Location = _location,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
