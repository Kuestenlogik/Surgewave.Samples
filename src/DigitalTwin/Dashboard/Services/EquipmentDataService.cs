using System.Collections.Concurrent;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Anomalies;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Equipment;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Events;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Telemetry;
using Kuestenlogik.Surgewave.Samples.DigitalTwin.Shared.Thresholds;

namespace Kuestenlogik.Surgewave.Samples.DigitalTwin.Dashboard.Services;

public sealed class EquipmentDataService : BackgroundService
{
    private const string BrokerAddress = "localhost:9092";
    private const string TelemetryTopic = "digitaltwin-telemetry";
    private const string EventsTopic = "digitaltwin-events";

    private readonly ILogger<EquipmentDataService> _logger;
    private readonly ConcurrentDictionary<string, EquipmentState> _equipmentStates = new();
    private readonly ConcurrentDictionary<EquipmentType, EquipmentThresholds> _thresholds = new();
    private readonly List<TwinSnapshot> _snapshots = new();
    private readonly List<Anomaly> _recentAnomalies = new();
    private readonly object _snapshotLock = new();

    private ISurgewaveClient? _client;
    private IConsumer<string, TelemetryReading>? _telemetryConsumer;
    private IConsumer<string, EquipmentEvent>? _eventConsumer;
    private bool _isConnected;
    private long _telemetryOffset;
    private long _eventOffset;
    private long _telemetryCount;
    private int _snapshotInterval = 100;

    public event Action? OnStateChanged;

    public EquipmentDataService(ILogger<EquipmentDataService> logger)
    {
        _logger = logger;
        foreach (EquipmentType type in Enum.GetValues<EquipmentType>())
            _thresholds[type] = EquipmentThresholds.GetDefaults(type);
    }

    public bool IsConnected => _isConnected;
    public IReadOnlyDictionary<string, EquipmentState> EquipmentStates => _equipmentStates;
    public IReadOnlyList<Anomaly> RecentAnomalies => _recentAnomalies;
    public IReadOnlyList<TwinSnapshot> Snapshots { get { lock (_snapshotLock) return _snapshots.ToList(); } }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Equipment Data Service starting...");
        try
        {
            _client = await SurgewaveClient.Create(BrokerAddress)
                .WithClientId("digitaltwin-dashboard")
                .UseSurgewaveProtocol()
                .BuildAsync();

            _isConnected = true;
            _logger.LogInformation("Connected to Surgewave broker");

            _telemetryConsumer = _client.CreateConsumer<string, TelemetryReading>(options =>
            {
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.ValueDeserializer = Serializers.JsonDeserializer<TelemetryReading>();
            });

            _eventConsumer = _client.CreateConsumer<string, EquipmentEvent>(options =>
            {
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.ValueDeserializer = Serializers.JsonDeserializer<EquipmentEvent>();
            });

            await _telemetryConsumer.SubscribeAsync(stoppingToken, TelemetryTopic);
            await _eventConsumer.SubscribeAsync(stoppingToken, EventsTopic);

            var telemetryTask = ConsumeTelemetryAsync(stoppingToken);
            var eventTask = ConsumeEventsAsync(stoppingToken);
            await Task.WhenAll(telemetryTask, eventTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in Equipment Data Service");
            _isConnected = false;
        }
    }

    private async Task ConsumeTelemetryAsync(CancellationToken ct)
    {
        if (_telemetryConsumer == null) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _telemetryConsumer.ConsumeAsync(ct);
                if (result?.Value != null)
                {
                    ProcessTelemetry(result.Value);
                    _telemetryOffset = result.Offset;
                    _telemetryCount++;
                    if (_telemetryCount % _snapshotInterval == 0) CreateSnapshot();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error consuming telemetry"); await Task.Delay(1000, ct); }
        }
    }

    private async Task ConsumeEventsAsync(CancellationToken ct)
    {
        if (_eventConsumer == null) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _eventConsumer.ConsumeAsync(ct);
                if (result?.Value != null) { ProcessEvent(result.Value); _eventOffset = result.Offset; }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error consuming events"); await Task.Delay(1000, ct); }
        }
    }

    private void ProcessTelemetry(TelemetryReading reading)
    {
        var state = _equipmentStates.GetOrAdd(reading.EquipmentId, id => CreateEquipmentState(id));
        state.UpdateTelemetry(reading);
        CheckThresholds(state, reading);
        OnStateChanged?.Invoke();
    }

    private void ProcessEvent(EquipmentEvent evt)
    {
        var state = _equipmentStates.GetOrAdd(evt.EquipmentId, id => CreateEquipmentState(id));
        state.LatestEvent = evt;
        state.LastUpdated = DateTime.UtcNow;
        state.Mode = evt switch
        {
            EquipmentStartedEvent => OperatingMode.Running,
            EquipmentStoppedEvent => OperatingMode.Stopped,
            MaintenanceStartedEvent => OperatingMode.Maintenance,
            MaintenanceCompletedEvent => OperatingMode.Stopped,
            FaultDetectedEvent => OperatingMode.Fault,
            FaultClearedEvent => OperatingMode.Stopped,
            ModeChangedEvent mce => mce.NewMode,
            _ => state.Mode
        };
        OnStateChanged?.Invoke();
    }

    private void CheckThresholds(EquipmentState state, TelemetryReading reading)
    {
        if (!_thresholds.TryGetValue(state.Type, out var thresholds)) return;

        if (reading.Temperature > thresholds.TemperatureCritical)
            AddAnomaly(state, AnomalySeverity.Critical, TelemetryType.Temperature, reading.Temperature, thresholds.TemperatureCritical, "Temperature critical");
        else if (reading.Temperature > thresholds.TemperatureWarning)
            AddAnomaly(state, AnomalySeverity.Warning, TelemetryType.Temperature, reading.Temperature, thresholds.TemperatureWarning, "Temperature warning");

        if (reading.Vibration > thresholds.VibrationCritical)
            AddAnomaly(state, AnomalySeverity.Critical, TelemetryType.Vibration, reading.Vibration, thresholds.VibrationCritical, "Vibration critical");
    }

    private void AddAnomaly(EquipmentState state, AnomalySeverity severity, TelemetryType metric, double currentValue, double thresholdValue, string description)
    {
        var anomaly = new Anomaly
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            EquipmentId = state.Id,
            DetectedAt = DateTime.UtcNow,
            Type = AnomalyType.ThresholdExceeded,
            Severity = severity,
            AffectedMetric = metric,
            CurrentValue = currentValue,
            ThresholdValue = thresholdValue,
            Description = description
        };
        state.ActiveAnomalies.Add(anomaly);
        _recentAnomalies.Add(anomaly);
        while (_recentAnomalies.Count > 100) _recentAnomalies.RemoveAt(0);
        _logger.LogWarning("Anomaly: {Description} on {Equipment}", description, state.Id);
    }

    private void CreateSnapshot()
    {
        lock (_snapshotLock)
        {
            _snapshots.Add(new TwinSnapshot
            {
                TelemetryOffset = _telemetryOffset,
                EventOffset = _eventOffset,
                Timestamp = DateTime.UtcNow,
                States = _equipmentStates.ToDictionary(k => k.Key, v => CloneState(v.Value))
            });
            while (_snapshots.Count > 100) _snapshots.RemoveAt(0);
        }
    }

    private static EquipmentState CloneState(EquipmentState s) => new()
    {
        Id = s.Id, Name = s.Name, Type = s.Type, Zone = s.Zone,
        PositionX = s.PositionX, PositionY = s.PositionY, PositionZ = s.PositionZ,
        Mode = s.Mode, LatestTelemetry = s.LatestTelemetry, LatestEvent = s.LatestEvent, LastUpdated = s.LastUpdated
    };

    private static EquipmentState CreateEquipmentState(string id)
    {
        var type = id[0] switch { 'P' => EquipmentType.Pump, 'M' => EquipmentType.Motor, 'C' => EquipmentType.Conveyor, 'K' => EquipmentType.Compressor, _ => EquipmentType.Motor };
        var zone = id[0] switch { 'P' => "A", 'M' or 'C' => "B", 'K' => "C", _ => "A" };
        var index = int.TryParse(id[2..], out var i) ? i : 1;

        // Position equipment in zones on factory floor
        var (baseX, baseZ) = zone switch
        {
            "A" => (-8.0, -5.0),  // Pumps zone (left)
            "B" => (0.0, 2.0),    // Motors/Conveyors zone (center)
            "C" => (10.0, 0.0),   // Compressors zone (right)
            _ => (0.0, 0.0)
        };

        // Offset within zone based on index
        var offsetX = (index % 3) * 2.5;
        var offsetZ = (index / 3) * 2.5;

        return new()
        {
            Id = id,
            Name = id[0] switch { 'P' => "Pump", 'M' => "Motor", 'C' => "Conveyor", 'K' => "Compressor", _ => "Equipment" } + " " + id[2..],
            Type = type,
            Zone = zone,
            PositionX = baseX + offsetX,
            PositionY = 0,
            PositionZ = baseZ + offsetZ
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Equipment Data Service stopping...");
        if (_telemetryConsumer != null) await _telemetryConsumer.DisposeAsync();
        if (_eventConsumer != null) await _eventConsumer.DisposeAsync();
        if (_client != null) await _client.DisposeAsync();
        _isConnected = false;
        await base.StopAsync(cancellationToken);
    }
}
