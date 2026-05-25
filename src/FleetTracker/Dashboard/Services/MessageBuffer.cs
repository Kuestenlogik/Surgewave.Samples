using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Services;

/// <summary>
/// Thread-safe buffer that stores consumed messages and periodic snapshots.
/// Uses snapshot-based approach for efficient time-travel queries.
/// Shared singleton that all clients read from.
/// </summary>
public sealed class MessageBuffer
{
    private readonly ConcurrentDictionary<long, VehiclePosition> _messages = new();
    private readonly List<FleetSnapshot> _snapshots = new();
    private readonly object _snapshotLock = new();

    private long _earliestOffset = -1;
    private long _latestOffset = -1;
    private long _lastSnapshotOffset = -1;

    // Create a snapshot every N messages
    private const int SnapshotInterval = 100;

    /// <summary>
    /// Gets the earliest available offset.
    /// </summary>
    public long EarliestOffset => _earliestOffset;

    /// <summary>
    /// Gets the latest offset in the buffer.
    /// </summary>
    public long LatestOffset => _latestOffset;

    /// <summary>
    /// Gets the total number of messages in the buffer.
    /// </summary>
    public int Count => _messages.Count;

    /// <summary>
    /// Gets the number of snapshots.
    /// </summary>
    public int SnapshotCount
    {
        get
        {
            lock (_snapshotLock)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <summary>
    /// Event raised when new messages are added.
    /// </summary>
    public event Action? OnMessagesAdded;

    /// <summary>
    /// Add a message to the buffer.
    /// Creates periodic snapshots for efficient time-travel queries.
    /// </summary>
    public void Add(long offset, VehiclePosition position)
    {
        _messages[offset] = position;

        // Update bounds
        if (_earliestOffset < 0 || offset < _earliestOffset)
        {
            _earliestOffset = offset;
        }
        if (_latestOffset < 0 || offset > _latestOffset)
        {
            _latestOffset = offset;
        }

        // Create snapshot periodically
        if (offset - _lastSnapshotOffset >= SnapshotInterval)
        {
            CreateSnapshot(offset);
        }

        // Notify subscribers periodically (every 20 messages to reduce overhead)
        if (offset % 20 == 0)
        {
            OnMessagesAdded?.Invoke();
        }
    }

    /// <summary>
    /// Creates a snapshot at the current offset.
    /// </summary>
    private void CreateSnapshot(long offset)
    {
        var positions = BuildStateUpToOffset(offset);

        lock (_snapshotLock)
        {
            _snapshots.Add(new FleetSnapshot
            {
                Offset = offset,
                Timestamp = DateTimeOffset.UtcNow,
                Positions = positions
            });
            _lastSnapshotOffset = offset;
        }
    }

    /// <summary>
    /// Builds complete state by iterating all messages up to offset.
    /// Used internally for snapshot creation.
    /// </summary>
    private Dictionary<string, VehiclePosition> BuildStateUpToOffset(long targetOffset)
    {
        var state = new Dictionary<string, VehiclePosition>();

        foreach (var kvp in _messages.Where(m => m.Key <= targetOffset).OrderBy(m => m.Key))
        {
            state[kvp.Value.VehicleId] = kvp.Value;
        }

        return state;
    }

    /// <summary>
    /// Get all vehicle positions at a specific point in time (offset).
    /// Uses snapshots for efficiency: finds nearest snapshot before target,
    /// then applies messages from snapshot to target offset.
    /// </summary>
    public Dictionary<string, VehiclePosition> GetStateAtOffset(long targetOffset)
    {
        FleetSnapshot? nearestSnapshot = null;

        lock (_snapshotLock)
        {
            // Find the nearest snapshot at or before targetOffset
            foreach (var snapshot in _snapshots)
            {
                if (snapshot.Offset <= targetOffset)
                {
                    nearestSnapshot = snapshot;
                }
                else
                {
                    break; // Snapshots are ordered by offset
                }
            }
        }

        if (nearestSnapshot != null)
        {
            // Start from snapshot and apply delta messages
            var state = nearestSnapshot.ClonePositions();

            // Apply messages between snapshot and target
            foreach (var kvp in _messages
                .Where(m => m.Key > nearestSnapshot.Offset && m.Key <= targetOffset)
                .OrderBy(m => m.Key))
            {
                state[kvp.Value.VehicleId] = kvp.Value;
            }

            return state;
        }
        else
        {
            // No snapshot available, build from scratch
            return BuildStateUpToOffset(targetOffset);
        }
    }

    /// <summary>
    /// Get messages in a range for playback.
    /// </summary>
    public IEnumerable<(long Offset, VehiclePosition Position)> GetMessagesInRange(long fromOffset, long toOffset)
    {
        return _messages
            .Where(m => m.Key >= fromOffset && m.Key <= toOffset)
            .OrderBy(m => m.Key)
            .Select(m => (m.Key, m.Value));
    }

    /// <summary>
    /// Get the message at a specific offset.
    /// </summary>
    public VehiclePosition? GetMessage(long offset)
    {
        return _messages.TryGetValue(offset, out var position) ? position : null;
    }
}
