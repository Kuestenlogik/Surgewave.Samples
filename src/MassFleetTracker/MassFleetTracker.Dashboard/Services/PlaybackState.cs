using MassFleetTracker.Shared;

namespace MassFleetTracker.Dashboard.Services;

/// <summary>
/// Per-client playback state for time-travel navigation.
/// Manages live vs. replay mode with aggregated grid data.
/// </summary>
public sealed class PlaybackState : IDisposable
{
    private readonly TimeSeriesBuffer _buffer;
    private readonly AggregationService _aggregation;
    private readonly ILogger<PlaybackState> _logger;
    private readonly object _lock = new();

    private bool _isLiveMode = true;
    private bool _isPaused;
    private double _playbackSpeed = 1.0;
    private DateTimeOffset _currentTime;
    private GridSnapshot? _currentSnapshot;
    private System.Threading.Timer? _playbackTimer;
    private bool _disposed;

    public PlaybackState(
        TimeSeriesBuffer buffer,
        AggregationService aggregation,
        ILogger<PlaybackState> logger)
    {
        _buffer = buffer;
        _aggregation = aggregation;
        _logger = logger;

        // Initialize to live mode
        _currentTime = DateTimeOffset.UtcNow;

        _buffer.OnSnapshotAdded += OnSnapshotAdded;
    }

    /// <summary>
    /// Gets whether this client is in live mode.
    /// </summary>
    public bool IsLiveMode => _isLiveMode;

    /// <summary>
    /// Gets whether playback is paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets the playback speed.
    /// </summary>
    public double PlaybackSpeed => _playbackSpeed;

    /// <summary>
    /// Gets the current playback time.
    /// </summary>
    public DateTimeOffset CurrentTime
    {
        get
        {
            lock (_lock)
            {
                return _isLiveMode ? DateTimeOffset.UtcNow : _currentTime;
            }
        }
    }

    /// <summary>
    /// Gets the earliest available time in the buffer.
    /// </summary>
    public DateTimeOffset EarliestTime => _buffer.EarliestTime;

    /// <summary>
    /// Gets the latest available time in the buffer.
    /// </summary>
    public DateTimeOffset LatestTime => _buffer.LatestTime;

    /// <summary>
    /// Gets the available time range in seconds.
    /// </summary>
    public double AvailableRangeSeconds => (LatestTime - EarliestTime).TotalSeconds;

    /// <summary>
    /// Gets the current position as percentage (0-100).
    /// </summary>
    public double CurrentPositionPercent
    {
        get
        {
            var range = AvailableRangeSeconds;
            if (range <= 0) return 100;

            var current = (CurrentTime - EarliestTime).TotalSeconds;
            return Math.Clamp(current / range * 100, 0, 100);
        }
    }

    /// <summary>
    /// Gets the current grid cells for rendering.
    /// </summary>
    public IReadOnlyList<GridCell> CurrentCells
    {
        get
        {
            lock (_lock)
            {
                if (_isLiveMode)
                {
                    return _aggregation.GetActiveCells();
                }
                return _currentSnapshot?.Cells ?? [];
            }
        }
    }

    /// <summary>
    /// Gets the current statistics.
    /// </summary>
    public FleetStatistics CurrentStatistics
    {
        get
        {
            lock (_lock)
            {
                if (_isLiveMode)
                {
                    return _aggregation.Statistics;
                }
                return _currentSnapshot?.Statistics ?? new FleetStatistics();
            }
        }
    }

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event EventHandler? OnStateChanged;

    /// <summary>
    /// Switch to live mode.
    /// </summary>
    public void SetLiveMode()
    {
        lock (_lock)
        {
            StopPlaybackTimer();
            _isLiveMode = true;
            _isPaused = false;
            _currentTime = DateTimeOffset.UtcNow;
            _currentSnapshot = null;
            _logger.LogInformation("Switched to live mode");
        }
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Seek to a specific time (enters replay mode, paused).
    /// </summary>
    public void SeekToTime(DateTimeOffset targetTime)
    {
        lock (_lock)
        {
            StopPlaybackTimer();
            _isLiveMode = false;
            _isPaused = true;

            // Clamp to available range
            _currentTime = targetTime < EarliestTime ? EarliestTime
                : targetTime > LatestTime ? LatestTime
                : targetTime;

            _currentSnapshot = _buffer.GetSnapshotAtTime(_currentTime);

            _logger.LogInformation(
                "Seeked to {Time}, showing {Count} cells",
                _currentTime, _currentSnapshot?.Cells.Count ?? 0);
        }
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Seek to a percentage of available range.
    /// </summary>
    public void SeekToPercentage(double percentage)
    {
        var range = LatestTime - EarliestTime;
        var targetTime = EarliestTime + TimeSpan.FromSeconds(range.TotalSeconds * (percentage / 100.0));
        SeekToTime(targetTime);
    }

    /// <summary>
    /// Pause playback.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (!_isLiveMode && !_isPaused)
            {
                StopPlaybackTimer();
                _isPaused = true;
                _logger.LogInformation("Paused at {Time}", _currentTime);
            }
        }
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resume playback.
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            if (!_isLiveMode && _isPaused)
            {
                _isPaused = false;
                StartPlaybackTimer();
                _logger.LogInformation("Resumed from {Time}", _currentTime);
            }
        }
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Set playback speed (0.5x to 10x).
    /// </summary>
    public void SetPlaybackSpeed(double speed)
    {
        lock (_lock)
        {
            _playbackSpeed = Math.Clamp(speed, 0.5, 10.0);

            if (!_isPaused && !_isLiveMode)
            {
                StopPlaybackTimer();
                StartPlaybackTimer();
            }

            _logger.LogInformation("Playback speed set to {Speed}x", _playbackSpeed);
        }
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Step forward by a number of seconds.
    /// </summary>
    public void StepForward(double seconds = 1)
    {
        lock (_lock)
        {
            if (!_isLiveMode)
            {
                SeekToTime(_currentTime.AddSeconds(seconds));
            }
        }
    }

    /// <summary>
    /// Step backward by a number of seconds.
    /// </summary>
    public void StepBackward(double seconds = 1)
    {
        lock (_lock)
        {
            if (!_isLiveMode)
            {
                SeekToTime(_currentTime.AddSeconds(-seconds));
            }
        }
    }

    private void OnSnapshotAdded(object? sender, EventArgs e)
    {
        if (_isLiveMode && !_disposed)
        {
            OnStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StartPlaybackTimer()
    {
        // Timer interval based on speed (at 1x, advance 1 second per real second)
        var intervalMs = (int)(1000 / _playbackSpeed);
        intervalMs = Math.Max(100, intervalMs);

        _playbackTimer = new System.Threading.Timer(
            PlaybackTick,
            null,
            intervalMs,
            intervalMs);
    }

    private void StopPlaybackTimer()
    {
        _playbackTimer?.Dispose();
        _playbackTimer = null;
    }

    private void PlaybackTick(object? state)
    {
        if (_disposed || _isPaused || _isLiveMode) return;

        lock (_lock)
        {
            // Advance time by 1 second * speed
            _currentTime = _currentTime.AddSeconds(1);

            if (_currentTime >= LatestTime)
            {
                // Caught up - switch to live mode
                _isLiveMode = true;
                _isPaused = false;
                StopPlaybackTimer();
                _currentSnapshot = null;
                _logger.LogInformation("Playback caught up, switching to live mode");
            }
            else
            {
                _currentSnapshot = _buffer.GetSnapshotAtTime(_currentTime);
            }
        }

        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _disposed = true;
        _buffer.OnSnapshotAdded -= OnSnapshotAdded;
        StopPlaybackTimer();
    }
}
