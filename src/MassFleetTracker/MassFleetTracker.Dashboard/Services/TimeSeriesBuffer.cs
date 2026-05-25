using MassFleetTracker.Shared;

namespace MassFleetTracker.Dashboard.Services;

/// <summary>
/// Stores time-series of aggregated grid snapshots for time-travel navigation.
/// Optimized for 100k vehicles by storing grid aggregates instead of individual positions.
/// </summary>
public sealed class TimeSeriesBuffer
{
    private readonly List<GridSnapshot> _snapshots = new();
    private readonly object _lock = new();

    // Configuration
    private readonly int _maxSnapshots;
    private readonly TimeSpan _snapshotInterval;

    private DateTimeOffset _lastSnapshotTime = DateTimeOffset.MinValue;

    public TimeSeriesBuffer(int maxSnapshots = 600, TimeSpan? snapshotInterval = null)
    {
        _maxSnapshots = maxSnapshots; // Default: 600 snapshots = 10 minutes at 1/sec
        _snapshotInterval = snapshotInterval ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Gets the earliest available timestamp.
    /// </summary>
    public DateTimeOffset EarliestTime
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count > 0 ? _snapshots[0].Timestamp : DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    /// Gets the latest available timestamp.
    /// </summary>
    public DateTimeOffset LatestTime
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count > 0 ? _snapshots[^1].Timestamp : DateTimeOffset.UtcNow;
            }
        }
    }

    /// <summary>
    /// Gets the number of stored snapshots.
    /// </summary>
    public int SnapshotCount
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <summary>
    /// Event raised when new snapshots are added.
    /// </summary>
    public event EventHandler? OnSnapshotAdded;

    /// <summary>
    /// Adds a grid snapshot if enough time has passed since the last one.
    /// </summary>
    public void AddSnapshot(IReadOnlyList<GridCell> activeCells, FleetStatistics stats)
    {
        var now = DateTimeOffset.UtcNow;

        if (now - _lastSnapshotTime < _snapshotInterval)
            return;

        lock (_lock)
        {
            // Create snapshot with deep copy of grid cells
            var snapshot = new GridSnapshot
            {
                Timestamp = now,
                Statistics = stats.Clone(),
                Cells = activeCells.Select(c => c.Clone()).ToList()
            };

            _snapshots.Add(snapshot);
            _lastSnapshotTime = now;

            // Remove old snapshots if we exceed max
            while (_snapshots.Count > _maxSnapshots)
            {
                _snapshots.RemoveAt(0);
            }
        }

        OnSnapshotAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the grid state at a specific time (or nearest available).
    /// </summary>
    public GridSnapshot? GetSnapshotAtTime(DateTimeOffset targetTime)
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
                return null;

            // Find the snapshot at or just before the target time
            GridSnapshot? result = null;
            foreach (var snapshot in _snapshots)
            {
                if (snapshot.Timestamp <= targetTime)
                {
                    result = snapshot;
                }
                else
                {
                    break; // Snapshots are ordered by time
                }
            }

            return result ?? _snapshots[0]; // Return earliest if target is before all snapshots
        }
    }

    /// <summary>
    /// Gets all snapshots in a time range (for charts/graphs).
    /// </summary>
    public IReadOnlyList<GridSnapshot> GetSnapshotsInRange(DateTimeOffset from, DateTimeOffset to)
    {
        lock (_lock)
        {
            return _snapshots
                .Where(s => s.Timestamp >= from && s.Timestamp <= to)
                .ToList();
        }
    }
}

/// <summary>
/// A point-in-time snapshot of the aggregated grid.
/// </summary>
public class GridSnapshot
{
    public DateTimeOffset Timestamp { get; init; }
    public FleetStatistics Statistics { get; init; } = new();
    public List<GridCell> Cells { get; init; } = new();
}
