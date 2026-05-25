using System.Collections.Concurrent;
using MassFleetTracker.Shared;

namespace MassFleetTracker.Dashboard.Services;

/// <summary>
/// Aggregates vehicle positions into a grid for efficient visualization.
/// Instead of rendering 100k individual markers, we aggregate into ~10k grid cells.
/// </summary>
public sealed class AggregationService
{
    // Berlin area bounds
    private const double MinLat = 52.35;
    private const double MaxLat = 52.65;
    private const double MinLon = 13.1;
    private const double MaxLon = 13.7;

    // Grid dimensions (100x100 = 10k cells max)
    public const int GridRows = 100;
    public const int GridCols = 100;

    private readonly double _cellLatSize = (MaxLat - MinLat) / GridRows;
    private readonly double _cellLonSize = (MaxLon - MinLon) / GridCols;

    // Current aggregated grid (jagged array for performance)
    private readonly GridCell[][] _grid;
    private readonly object _gridLock = new();

    // Latest positions per vehicle (for detail view)
    private readonly ConcurrentDictionary<string, VehiclePosition> _latestPositions = new();

    // Statistics
    private readonly FleetStatistics _statistics = new();
    private long _messageCount;
    private DateTime _lastStatsUpdate = DateTime.UtcNow;
    private long _lastMessageCount;

    public AggregationService()
    {
        // Initialize grid (jagged array)
        _grid = new GridCell[GridRows][];
        for (int row = 0; row < GridRows; row++)
        {
            _grid[row] = new GridCell[GridCols];
            for (int col = 0; col < GridCols; col++)
            {
                _grid[row][col] = new GridCell { Row = row, Col = col };
            }
        }
    }

    /// <summary>
    /// Gets current fleet statistics.
    /// </summary>
    public FleetStatistics Statistics => _statistics;

    /// <summary>
    /// Updates with a new vehicle position.
    /// </summary>
    public void Update(VehiclePosition position)
    {
        _latestPositions[position.VehicleId] = position;
        Interlocked.Increment(ref _messageCount);
    }

    /// <summary>
    /// Rebuilds the aggregated grid from current positions.
    /// Call this periodically (e.g., every 500ms) rather than on every update.
    /// </summary>
    public void RebuildGrid()
    {
        lock (_gridLock)
        {
            // Reset all cells
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    _grid[row][col].Reset();
                }
            }

            // Aggregate all positions
            int moving = 0, stopped = 0, idling = 0, offline = 0;
            double totalSpeed = 0;
            double maxSpeed = 0;
            int speedCount = 0;

            foreach (var position in _latestPositions.Values)
            {
                var (row, col) = GetCellIndex(position.Latitude, position.Longitude);
                if (row >= 0 && row < GridRows && col >= 0 && col < GridCols)
                {
                    _grid[row][col].AddVehicle(position);
                }

                // Update statistics
                switch (position.Status)
                {
                    case VehicleStatus.Moving:
                        moving++;
                        totalSpeed += position.Speed;
                        speedCount++;
                        if (position.Speed > maxSpeed) maxSpeed = position.Speed;
                        break;
                    case VehicleStatus.Stopped:
                        stopped++;
                        break;
                    case VehicleStatus.Idling:
                        idling++;
                        break;
                    case VehicleStatus.Offline:
                        offline++;
                        break;
                }
            }

            // Update statistics
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastStatsUpdate).TotalSeconds;
            if (elapsed > 0)
            {
                var messages = Interlocked.Read(ref _messageCount);
                _statistics.MessagesPerSecond = (messages - _lastMessageCount) / elapsed;
                _lastMessageCount = messages;
                _lastStatsUpdate = now;
            }

            _statistics.TotalVehicles = _latestPositions.Count;
            _statistics.MovingVehicles = moving;
            _statistics.StoppedVehicles = stopped;
            _statistics.IdlingVehicles = idling;
            _statistics.OfflineVehicles = offline;
            _statistics.AverageSpeed = speedCount > 0 ? totalSpeed / speedCount : 0;
            _statistics.MaxSpeed = maxSpeed;
            _statistics.TotalMessagesProcessed = Interlocked.Read(ref _messageCount);
            _statistics.Timestamp = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Gets non-empty grid cells for rendering.
    /// </summary>
    public IReadOnlyList<GridCell> GetActiveCells()
    {
        var cells = new List<GridCell>();
        lock (_gridLock)
        {
            for (int row = 0; row < GridRows; row++)
            {
                for (int col = 0; col < GridCols; col++)
                {
                    if (_grid[row][col].VehicleCount > 0)
                    {
                        cells.Add(_grid[row][col]);
                    }
                }
            }
        }
        return cells;
    }

    /// <summary>
    /// Gets vehicles in a specific grid cell (for detail view).
    /// </summary>
    public IReadOnlyList<VehiclePosition> GetVehiclesInCell(int row, int col)
    {
        return _latestPositions.Values
            .Where(p =>
            {
                var (r, c) = GetCellIndex(p.Latitude, p.Longitude);
                return r == row && c == col;
            })
            .OrderBy(p => p.VehicleId)
            .Take(50) // Limit for UI
            .ToList();
    }

    /// <summary>
    /// Converts lat/lon to grid cell index.
    /// </summary>
    public (int Row, int Col) GetCellIndex(double lat, double lon)
    {
        var row = (int)((lat - MinLat) / _cellLatSize);
        var col = (int)((lon - MinLon) / _cellLonSize);
        return (Math.Clamp(row, 0, GridRows - 1), Math.Clamp(col, 0, GridCols - 1));
    }

    /// <summary>
    /// Converts grid cell to center lat/lon.
    /// </summary>
    public (double Lat, double Lon) GetCellCenter(int row, int col)
    {
        var lat = MinLat + (row + 0.5) * _cellLatSize;
        var lon = MinLon + (col + 0.5) * _cellLonSize;
        return (lat, lon);
    }

    /// <summary>
    /// Gets all vehicle positions for individual marker rendering.
    /// </summary>
    public IReadOnlyList<VehiclePosition> GetAllVehicles()
    {
        return _latestPositions.Values.ToList();
    }
}
