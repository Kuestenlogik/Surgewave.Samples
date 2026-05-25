using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Services;

/// <summary>
/// Per-client playback state. Each browser circuit gets its own instance.
/// Demonstrates combining snapshots with Surgewave offset-based queries for time-travel.
/// </summary>
public sealed class ClientPlaybackState : IDisposable
{
    private readonly MessageBuffer _buffer;
    private readonly FleetDataService _fleetService;
    private readonly ILogger<ClientPlaybackState> _logger;
    private readonly object _lock = new();

    private bool _isLiveMode = true;
    private bool _isPaused;
    private double _playbackSpeed = 1.0;
    private long _currentOffset;
    private Dictionary<string, VehiclePosition> _currentPositions = new();
    private System.Threading.Timer? _playbackTimer;
    private bool _disposed;

    public ClientPlaybackState(
        MessageBuffer buffer,
        FleetDataService fleetService,
        ILogger<ClientPlaybackState> logger)
    {
        _buffer = buffer;
        _fleetService = fleetService;
        _logger = logger;

        // Initialize state from existing buffer data
        if (_buffer.LatestOffset >= 0)
        {
            _currentOffset = _buffer.LatestOffset;
            _currentPositions = _buffer.GetStateAtOffset(_currentOffset);
            _logger.LogDebug("Initialized client with {Count} vehicles at offset {Offset}",
                _currentPositions.Count, _currentOffset);
        }

        _buffer.OnMessagesAdded += OnBufferUpdated;
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
    /// Gets the current offset position.
    /// </summary>
    public long CurrentOffset => _currentOffset;

    /// <summary>
    /// Gets the earliest available offset.
    /// </summary>
    public long EarliestOffset => _buffer.EarliestOffset;

    /// <summary>
    /// Gets the latest offset in the buffer.
    /// </summary>
    public long LatestOffset => _buffer.LatestOffset;

    /// <summary>
    /// Gets the current vehicle positions based on playback state.
    /// </summary>
    public IReadOnlyDictionary<string, VehiclePosition> CurrentPositions
    {
        get
        {
            lock (_lock)
            {
                return _currentPositions;
            }
        }
    }

    /// <summary>
    /// Gets the total message count in the buffer.
    /// </summary>
    public int MessageCount => _buffer.Count;

    /// <summary>
    /// Gets whether connected to the broker (buffer has data).
    /// </summary>
    public bool IsConnected => _buffer.LatestOffset >= 0;

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event Action? OnStateChanged;

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
            _currentOffset = _buffer.LatestOffset;
            UpdateCurrentPositions();
            _logger.LogInformation("Client switched to live mode");
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Seek to a specific offset (enters replay mode, paused).
    /// Uses snapshot + Surgewave query pattern:
    /// 1. Find nearest snapshot before target offset
    /// 2. Query Surgewave for messages between snapshot and target
    /// 3. Apply delta messages to snapshot state
    /// </summary>
    public void SeekToOffset(long offset)
    {
        lock (_lock)
        {
            StopPlaybackTimer();
            _isLiveMode = false;
            _isPaused = true;
            _currentOffset = Math.Clamp(offset, _buffer.EarliestOffset, _buffer.LatestOffset);

            // Build state using snapshot + Surgewave query pattern
            // This demonstrates the architecture used in production systems
            BuildStateAtOffsetAsync(_currentOffset).GetAwaiter().GetResult();

            _logger.LogInformation(
                "Client seeked to offset {Offset} using snapshot + Surgewave delta, showing {Count} vehicles",
                _currentOffset, _currentPositions.Count);
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Builds complete fleet state at target offset using snapshot + Surgewave queries.
    /// </summary>
    private async Task BuildStateAtOffsetAsync(long targetOffset)
    {
        // Get the nearest snapshot and the delta messages from Surgewave
        // The MessageBuffer provides the snapshot, FleetDataService provides Surgewave query
        var snapshotState = _buffer.GetStateAtOffset(targetOffset);

        // For the demo, we use the buffer's GetStateAtOffset which internally:
        // 1. Finds the nearest snapshot
        // 2. Gets delta messages
        // 3. Applies them to build complete state
        //
        // In production with larger data, you would:
        // 1. Get snapshot from snapshot store
        // 2. Call: await _fleetService.FetchMessagesFromSurgewaveAsync(snapshotOffset, targetOffset)
        // 3. Apply messages to snapshot

        _currentPositions = snapshotState;

        // Log to show the pattern being used
        _logger.LogDebug(
            "Built state at offset {Offset}: {SnapshotCount} snapshots available, {VehicleCount} vehicles",
            targetOffset, _buffer.SnapshotCount, _currentPositions.Count);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Seek to a percentage of available range.
    /// </summary>
    public void SeekToPercentage(double percentage)
    {
        var range = _buffer.LatestOffset - _buffer.EarliestOffset;
        var offset = _buffer.EarliestOffset + (long)(range * (percentage / 100.0));
        SeekToOffset(offset);
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
                _logger.LogInformation("Client paused at offset {Offset}", _currentOffset);
            }
        }
        OnStateChanged?.Invoke();
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
                _logger.LogInformation("Client resumed playback from offset {Offset}", _currentOffset);
            }
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Set playback speed.
    /// </summary>
    public void SetPlaybackSpeed(double speed)
    {
        lock (_lock)
        {
            _playbackSpeed = Math.Clamp(speed, 0.5, 10.0);

            // Restart timer with new interval if playing
            if (!_isPaused && !_isLiveMode)
            {
                StopPlaybackTimer();
                StartPlaybackTimer();
            }

            _logger.LogInformation("Client playback speed set to {Speed}x", _playbackSpeed);
        }
        OnStateChanged?.Invoke();
    }

    private void OnBufferUpdated()
    {
        if (_isLiveMode && !_disposed)
        {
            lock (_lock)
            {
                _currentOffset = _buffer.LatestOffset;
                UpdateCurrentPositions();
            }
            OnStateChanged?.Invoke();
        }
    }

    private void UpdateCurrentPositions()
    {
        // In live mode, show current state at latest offset
        _currentPositions = _buffer.GetStateAtOffset(_buffer.LatestOffset);
    }

    private void StartPlaybackTimer()
    {
        // Calculate interval based on speed
        // At 1x speed, advance roughly 20 messages per second (matching generator rate)
        var intervalMs = (int)(50 / _playbackSpeed);
        intervalMs = Math.Max(10, intervalMs); // Minimum 10ms

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
            // Advance to next offset
            var nextOffset = _currentOffset + 1;

            if (nextOffset > _buffer.LatestOffset)
            {
                // Reached end of buffer, switch to live mode
                _isLiveMode = true;
                _isPaused = false;
                StopPlaybackTimer();
                _currentOffset = _buffer.LatestOffset;
                UpdateCurrentPositions();
                _logger.LogInformation("Client playback caught up, switching to live mode");
            }
            else
            {
                _currentOffset = nextOffset;

                // Update position if there's a message at this offset
                var message = _buffer.GetMessage(_currentOffset);
                if (message != null)
                {
                    _currentPositions[message.VehicleId] = message;
                }
            }
        }

        OnStateChanged?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        _buffer.OnMessagesAdded -= OnBufferUpdated;
        StopPlaybackTimer();
    }
}
