using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Services;

/// <summary>
/// A snapshot of the complete fleet state at a specific offset.
/// Snapshots are created periodically for efficient time-travel queries.
/// </summary>
public sealed class FleetSnapshot
{
    /// <summary>
    /// The offset at which this snapshot was taken.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The timestamp when this snapshot was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Complete vehicle positions at this point in time.
    /// </summary>
    public required Dictionary<string, VehiclePosition> Positions { get; init; }

    /// <summary>
    /// Creates a deep copy of the positions dictionary.
    /// </summary>
    public Dictionary<string, VehiclePosition> ClonePositions()
    {
        return new Dictionary<string, VehiclePosition>(Positions);
    }
}
