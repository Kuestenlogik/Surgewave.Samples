using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.IotDashboard.Shared;

namespace Kuestenlogik.Surgewave.Samples.IotDashboard.Dashboard.Services;

/// <summary>
/// Service that consumes sensor readings and maintains current state.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler - Action is simpler for this sample
#pragma warning disable CA1024 // Methods returning collections are appropriate here
public sealed class SensorDataService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<SensorDataService> _logger;
    private readonly IConfiguration _configuration;

    private ISurgewaveClient? _client;
    private IConsumer<string, SensorReading>? _consumer;
    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    // Current readings per sensor
    private readonly ConcurrentDictionary<string, SensorReading> _latestReadings = new();

    // Historical readings (last 100 per sensor for charting)
    private readonly ConcurrentDictionary<string, LinkedList<SensorReading>> _history = new();
    private const int MaxHistoryPerSensor = 100;

    // Aggregated stats per sensor (1-minute windows)
    private readonly ConcurrentDictionary<string, SensorStats> _stats = new();

    // Active alerts
    private readonly ConcurrentDictionary<string, SensorAlert> _alerts = new();

    // For tracking aggregation windows
    private readonly ConcurrentDictionary<string, List<double>> _windowValues = new();
    private DateTimeOffset _windowStart = DateTimeOffset.UtcNow;
    private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(1);

    public event Action? OnDataUpdated;

    public SensorDataService(ILogger<SensorDataService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public IEnumerable<SensorReading> LatestReadings => _latestReadings.Values;

    public IEnumerable<SensorReading> GetHistory(string sensorId)
    {
        if (_history.TryGetValue(sensorId, out var history))
        {
            lock (history)
            {
                return history.ToList();
            }
        }
        return [];
    }

    public IEnumerable<SensorStats> Stats => _stats.Values;

    public IEnumerable<SensorAlert> ActiveAlerts => _alerts.Values;

    public IEnumerable<SensorReading> GetLatestByType(SensorType type) =>
        _latestReadings.Values.Where(r => r.Type == type);

    public IEnumerable<SensorReading> GetLatestByLocation(string location) =>
        _latestReadings.Values.Where(r => r.Location == location);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = _configuration["Surgewave:BootstrapServers"] ?? "localhost:9092";

        try
        {
            _client = await SurgewaveClient.Create(bootstrapServers)
                .UseSurgewaveProtocol()
                .BuildAsync();

            _consumer = _client.CreateConsumer<string, SensorReading>(options =>
            {
                options.GroupId = $"iot-dashboard-{Guid.NewGuid():N}";
                options.AutoOffsetReset = AutoOffsetReset.Latest;
                options.EnableAutoCommit = true;
                options.ValueDeserializer = Serializers.JsonDeserializer<SensorReading>();
            });

            _consumer.Subscribe("iot-sensors");

            _cts = new CancellationTokenSource();
            _consumeTask = ConsumeLoopAsync(_cts.Token);

            _logger.LogInformation("SensorDataService started, consuming from Surgewave at {Servers}", bootstrapServers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Surgewave broker");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_consumeTask != null)
        {
            try
            {
                await _consumeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        await DisposeAsync();
    }

    private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
    {
        if (_consumer == null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _consumer.ConsumeAsync(
                    timeout: TimeSpan.FromSeconds(1),
                    cancellationToken: cancellationToken);

                if (result?.Value != null)
                {
                    ProcessReading(result.Value);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming sensor reading");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private void ProcessReading(SensorReading reading)
    {
        // Update latest reading
        _latestReadings[reading.SensorId] = reading;

        // Add to history
        if (!_history.TryGetValue(reading.SensorId, out var history))
        {
            history = new LinkedList<SensorReading>();
            _history[reading.SensorId] = history;
        }

        lock (history)
        {
            history.AddLast(reading);
            while (history.Count > MaxHistoryPerSensor)
            {
                history.RemoveFirst();
            }
        }

        // Update aggregation window
        UpdateAggregation(reading);

        // Check for alerts
        CheckAlerts(reading);

        // Notify listeners
        OnDataUpdated?.Invoke();
    }

    private void UpdateAggregation(SensorReading reading)
    {
        var now = DateTimeOffset.UtcNow;

        // Check if we need to close the current window
        if (now - _windowStart >= _windowDuration)
        {
            // Calculate stats for the closed window
            foreach (var (sensorId, values) in _windowValues)
            {
                if (values.Count > 0 && _latestReadings.TryGetValue(sensorId, out var lastReading))
                {
                    _stats[sensorId] = new SensorStats
                    {
                        SensorId = sensorId,
                        Type = lastReading.Type,
                        Location = lastReading.Location,
                        Min = values.Min(),
                        Max = values.Max(),
                        Average = values.Average(),
                        Count = values.Count,
                        Unit = lastReading.Unit,
                        WindowStart = _windowStart,
                        WindowEnd = now
                    };
                }
            }

            // Reset for new window
            _windowValues.Clear();
            _windowStart = now;
        }

        // Add value to current window
        if (!_windowValues.TryGetValue(reading.SensorId, out var sensorValues))
        {
            sensorValues = [];
            _windowValues[reading.SensorId] = sensorValues;
        }
        sensorValues.Add(reading.Value);
    }

    private void CheckAlerts(SensorReading reading)
    {
        var severity = SensorThresholds.CheckThreshold(reading.Type, reading.Value);

        if (severity.HasValue)
        {
            var thresholds = SensorThresholds.Values[reading.Type];
            var threshold = severity == AlertSeverity.Critical
                ? (reading.Value < thresholds.CriticalLow ? thresholds.CriticalLow : thresholds.CriticalHigh)
                : (reading.Value < thresholds.WarningLow ? thresholds.WarningLow : thresholds.WarningHigh);

            var direction = reading.Value < threshold ? "below" : "above";

            _alerts[reading.SensorId] = new SensorAlert
            {
                AlertId = $"alert-{reading.SensorId}-{reading.Timestamp.Ticks}",
                SensorId = reading.SensorId,
                Type = reading.Type,
                Location = reading.Location,
                Severity = severity.Value,
                Message = $"{reading.Type} is {direction} {severity} threshold",
                Value = reading.Value,
                Threshold = threshold,
                Unit = reading.Unit,
                Timestamp = reading.Timestamp
            };
        }
        else
        {
            // Clear alert if value is back to normal
            _alerts.TryRemove(reading.SensorId, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumer != null)
        {
            await _consumer.DisposeAsync();
            _consumer = null;
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _cts?.Dispose();
    }
}
