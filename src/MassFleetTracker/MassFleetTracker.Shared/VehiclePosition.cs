namespace MassFleetTracker.Shared;

/// <summary>
/// Represents a vehicle's position and status at a point in time.
/// </summary>
public sealed record VehiclePosition
{
    /// <summary>
    /// Unique identifier for the vehicle.
    /// </summary>
    public required string VehicleId { get; init; }

    /// <summary>
    /// Latitude in degrees (-90 to 90).
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude in degrees (-180 to 180).
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Speed in km/h.
    /// </summary>
    public required double Speed { get; init; }

    /// <summary>
    /// Heading in degrees (0-360, 0=North, 90=East).
    /// </summary>
    public required double Heading { get; init; }

    /// <summary>
    /// Timestamp when this position was recorded.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Current status of the vehicle.
    /// </summary>
    public required VehicleStatus Status { get; init; }

    /// <summary>
    /// Optional driver name.
    /// </summary>
    public string? DriverName { get; init; }

    /// <summary>
    /// Partition key for distribution across partitions.
    /// </summary>
    public int PartitionKey => Math.Abs(VehicleId.GetHashCode()) % 100;
}

/// <summary>
/// Vehicle status enumeration.
/// </summary>
public enum VehicleStatus
{
    /// <summary>Vehicle is currently moving.</summary>
    Moving,

    /// <summary>Vehicle is stopped (speed = 0).</summary>
    Stopped,

    /// <summary>Vehicle is idling (engine on, not moving).</summary>
    Idling,

    /// <summary>Vehicle is offline/disconnected.</summary>
    Offline
}
