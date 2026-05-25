namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

/// <summary>
/// Represents a reading from an IoT sensor.
/// </summary>
public sealed record SensorReading
{
    /// <summary>
    /// Unique sensor identifier (e.g., "sensor-001").
    /// </summary>
    public required string SensorId { get; init; }

    /// <summary>
    /// Sensor type (Temperature, Humidity, Pressure, CO2, Light).
    /// </summary>
    public required SensorType Type { get; init; }

    /// <summary>
    /// The measured value.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// Unit of measurement (e.g., "°C", "%", "hPa", "ppm", "lux").
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Location or zone of the sensor.
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Timestamp when the reading was taken.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Types of sensors supported.
/// </summary>
public enum SensorType
{
    Temperature,
    Humidity,
    Pressure,
    CO2,
    Light
}
