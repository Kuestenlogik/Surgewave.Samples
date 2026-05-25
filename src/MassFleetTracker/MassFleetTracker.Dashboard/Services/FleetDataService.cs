using System.Text.Json;
using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Native;
using MassFleetTracker.Shared;

namespace MassFleetTracker.Dashboard.Services;

/// <summary>
/// Background service that consumes vehicle positions from Surgewave at high throughput.
/// Optimized for 100k msg/s processing using parallel partition consumption.
/// </summary>
public sealed class FleetDataService : BackgroundService
{
    private readonly ILogger<FleetDataService> _logger;
    private readonly AggregationService _aggregation;
    private readonly TimeSeriesBuffer _timeSeriesBuffer;

    private const string TopicName = "mass-fleet-positions";
    private const string BrokerAddress = "localhost:9092";
    private const int AggregationIntervalMs = 500;
    private const int SnapshotIntervalMs = 1000; // Save snapshot every second

    private bool _isConnected;
    private long _consumedCount;

    public FleetDataService(
        ILogger<FleetDataService> logger,
        AggregationService aggregation,
        TimeSeriesBuffer timeSeriesBuffer)
    {
        _logger = logger;
        _aggregation = aggregation;
        _timeSeriesBuffer = timeSeriesBuffer;
    }

    public bool IsConnected => _isConnected;

    public event EventHandler? DataUpdated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MassFleetTracker Data Service starting...");
        _logger.LogInformation("Connecting to Surgewave broker at {Broker}", BrokerAddress);

        ISurgewaveClient? client = null;

        try
        {
            client = await SurgewaveClient.Create(BrokerAddress)
                .WithClientId("mass-fleet-dashboard")
                .UseSurgewaveProtocol()
                .BuildAsync();

            _isConnected = true;
            _logger.LogInformation("Connected to Surgewave. Protocol: {Protocol}", client.Protocol);

            // Get partition count for the topic
            var nativeClient = client.NativeClient!;
            var topics = await nativeClient.Topics.ListAsync(stoppingToken);
            var topicInfo = topics.Find(t => t.Name == TopicName);

            if (topicInfo == null)
            {
                _logger.LogWarning("Topic {Topic} not found. Waiting for it to be created...", TopicName);
                while (topicInfo == null && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                    topics = await nativeClient.Topics.ListAsync(stoppingToken);
                    topicInfo = topics.Find(t => t.Name == TopicName);
                }
            }

            if (topicInfo == null)
            {
                _logger.LogError("Topic {Topic} never appeared", TopicName);
                return;
            }

            var partitionCount = topicInfo.PartitionCount;
            _logger.LogInformation("Topic {Topic} has {Count} partitions", TopicName, partitionCount);

            // Aggregation timer - runs independently
            using var aggregationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(AggregationIntervalMs));
            var lastSnapshotTime = DateTimeOffset.UtcNow;

            // Start aggregation task
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await aggregationTimer.WaitForNextTickAsync(stoppingToken);
                        _aggregation.RebuildGrid();
                        _logger.LogDebug("Aggregation rebuilt. Consumed: {Count:N0}", _consumedCount);

                        // Save snapshot for time-travel (every second)
                        var now = DateTimeOffset.UtcNow;
                        if ((now - lastSnapshotTime).TotalMilliseconds >= SnapshotIntervalMs)
                        {
                            _timeSeriesBuffer.AddSnapshot(
                                _aggregation.GetActiveCells(),
                                _aggregation.Statistics);
                            lastSnapshotTime = now;
                        }

                        DataUpdated?.Invoke(this, EventArgs.Empty);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, stoppingToken);

            // Start a parallel consumer task for each partition
            var consumerTasks = new List<Task>();
            for (int partition = 0; partition < partitionCount; partition++)
            {
                var p = partition;
                consumerTasks.Add(Task.Run(async () =>
                {
                    await ConsumePartitionAsync(nativeClient, p, stoppingToken);
                }, stoppingToken));
            }

            _logger.LogInformation("Started {Count} parallel partition consumers", partitionCount);

            // Wait for all consumers to complete
            await Task.WhenAll(consumerTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Surgewave broker");
            _isConnected = false;
        }
        finally
        {
            if (client != null) await client.DisposeAsync();
            _isConnected = false;
        }

        _logger.LogInformation("MassFleetTracker Data Service stopped. Total consumed: {Count:N0}", _consumedCount);
    }

    private async Task ConsumePartitionAsync(SurgewaveNativeClient nativeClient, int partition, CancellationToken stoppingToken)
    {
        try
        {
            // Start from latest offset (only consume new messages)
            var offset = await nativeClient.Messaging.GetLatestOffsetAsync(TopicName, partition, stoppingToken);

            _logger.LogDebug("Partition {Partition}: starting at offset {Offset}", partition, offset);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Fetch a batch of messages
                    var result = await nativeClient.Messaging.ReceiveAsync(
                        TopicName, partition, offset,
                        maxBytes: 1024 * 1024, // 1MB per fetch
                        maxWaitMs: 5000,
                        stoppingToken);

                    if (result.Messages.Count > 0)
                    {
                        // Process all messages in the batch
                        foreach (var msg in result.Messages)
                        {
                            if (msg.Offset < offset) continue; // Skip already processed

                            try
                            {
                                var position = JsonSerializer.Deserialize<VehiclePosition>(msg.Value);
                                if (position != null)
                                {
                                    _aggregation.Update(position);
                                    Interlocked.Increment(ref _consumedCount);
                                }
                            }
                            catch (JsonException)
                            {
                                // Skip invalid JSON
                            }
                        }

                        offset = result.Messages[^1].Offset + 1;
                    }
                    else if (result.HighWatermark > offset)
                    {
                        // No data at this offset but newer data exists - jump to latest
                        offset = await nativeClient.Messaging.GetLatestOffsetAsync(TopicName, partition, stoppingToken);
                    }
                    else
                    {
                        // No new data, brief wait
                        await Task.Delay(10, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error consuming partition {Partition}", partition);
                    await Task.Delay(100, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in partition {Partition} consumer", partition);
        }
    }
}
