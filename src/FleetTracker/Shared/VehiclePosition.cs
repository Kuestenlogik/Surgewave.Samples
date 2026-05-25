namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

/// <summary>
/// Represents a vehicle's GPS position at a point in time.
/// </summary>
public sealed record VehiclePosition
{
    /// <summary>
    /// Unique identifier for the vehicle (e.g., "truck-001").
    /// </summary>
    public required string VehicleId { get; init; }

    /// <summary>
    /// Latitude in decimal degrees (-90 to 90).
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude in decimal degrees (-180 to 180).
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Current speed in km/h.
    /// </summary>
    public double Speed { get; init; }

    /// <summary>
    /// Heading in degrees (0-360, where 0 is North).
    /// </summary>
    public double Heading { get; init; }

    /// <summary>
    /// Timestamp when this position was recorded.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Vehicle status: "moving", "stopped", or "idle".
    /// </summary>
    public string Status { get; init; } = "moving";

    /// <summary>
    /// Optional driver name.
    /// </summary>
    public string? DriverName { get; init; }
}
