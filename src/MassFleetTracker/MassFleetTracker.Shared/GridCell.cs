namespace MassFleetTracker.Shared;

/// <summary>
/// Represents an aggregated grid cell for efficient map rendering.
/// The map area is divided into a grid (e.g., 100x100 cells) where each cell
/// contains aggregated statistics for all vehicles within that cell.
/// </summary>
public sealed class GridCell
{
    /// <summary>
    /// Row index in the grid (0-based).
    /// </summary>
    public required int Row { get; init; }

    /// <summary>
    /// Column index in the grid (0-based).
    /// </summary>
    public required int Col { get; init; }

    /// <summary>
    /// Number of vehicles in this cell.
    /// </summary>
    public int VehicleCount { get; set; }

    /// <summary>
    /// Average speed of vehicles in this cell (km/h).
    /// </summary>
    public double AvgSpeed { get; set; }

    /// <summary>
    /// Center latitude of all vehicles in this cell.
    /// </summary>
    public double AvgLatitude { get; set; }

    /// <summary>
    /// Center longitude of all vehicles in this cell.
    /// </summary>
    public double AvgLongitude { get; set; }

    /// <summary>
    /// Number of moving vehicles in this cell.
    /// </summary>
    public int MovingCount { get; set; }

    /// <summary>
    /// Number of stopped/idling vehicles in this cell.
    /// </summary>
    public int StoppedCount { get; set; }

    /// <summary>
    /// Maximum speed observed in this cell.
    /// </summary>
    public double MaxSpeed { get; set; }

    /// <summary>
    /// Timestamp of last update.
    /// </summary>
    public DateTimeOffset LastUpdate { get; set; }

    /// <summary>
    /// Resets the cell for a new aggregation cycle.
    /// </summary>
    public void Reset()
    {
        VehicleCount = 0;
        AvgSpeed = 0;
        AvgLatitude = 0;
        AvgLongitude = 0;
        MovingCount = 0;
        StoppedCount = 0;
        MaxSpeed = 0;
    }

    /// <summary>
    /// Adds a vehicle position to this cell's aggregation.
    /// </summary>
    public void AddVehicle(VehiclePosition position)
    {
        // Running average calculation
        var newCount = VehicleCount + 1;
        AvgSpeed = (AvgSpeed * VehicleCount + position.Speed) / newCount;
        AvgLatitude = (AvgLatitude * VehicleCount + position.Latitude) / newCount;
        AvgLongitude = (AvgLongitude * VehicleCount + position.Longitude) / newCount;

        VehicleCount = newCount;

        if (position.Speed > MaxSpeed)
            MaxSpeed = position.Speed;

        if (position.Status == VehicleStatus.Moving)
            MovingCount++;
        else
            StoppedCount++;

        LastUpdate = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a deep copy of this grid cell.
    /// </summary>
    public GridCell Clone() => new()
    {
        Row = Row,
        Col = Col,
        VehicleCount = VehicleCount,
        AvgSpeed = AvgSpeed,
        AvgLatitude = AvgLatitude,
        AvgLongitude = AvgLongitude,
        MovingCount = MovingCount,
        StoppedCount = StoppedCount,
        MaxSpeed = MaxSpeed,
        LastUpdate = LastUpdate
    };
}
