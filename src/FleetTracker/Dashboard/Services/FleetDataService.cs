using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Client.Abstractions;
using Kuestenlogik.Surgewave.Client.Consumer;
using Kuestenlogik.Surgewave.Client.Serialization;
using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Dashboard.Services;

/// <summary>
/// Background service that consumes vehicle positions from Surgewave.
/// Demonstrates the pattern of combining:
/// 1. Real-time consumption for live updates
/// 2. Periodic snapshots for efficient time-travel
/// 3. Surgewave offset-based queries for delta between snapshots
/// </summary>
public sealed class FleetDataService : BackgroundService
{
    private readonly ILogger<FleetDataService> _logger;
    private readonly MessageBuffer _buffer;

    private ISurgewaveClient? _client;
    private IConsumer<string, VehiclePosition>? _consumer;
    private bool _isConnected;

    private const string TopicName = "fleet-positions";
    private const string BrokerAddress = "localhost:9092";

    public FleetDataService(ILogger<FleetDataService> logger, MessageBuffer buffer)
    {
        _logger = logger;
        _buffer = buffer;
    }

    /// <summary>
    /// Gets whether the service is connected to Surgewave.
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Fetches messages from Surgewave for a specific offset range.
    /// This demonstrates querying Surgewave for delta messages between a snapshot and target offset.
    /// In production, this would create a temporary consumer to seek and fetch the range.
    /// </summary>
    /// <param name="fromOffset">Start offset (exclusive)</param>
    /// <param name="toOffset">End offset (inclusive)</param>
    /// <returns>Messages in the specified range</returns>
    public async Task<IReadOnlyList<(long Offset, VehiclePosition Position)>> FetchMessagesFromSurgewaveAsync(
        long fromOffset,
        long toOffset)
    {
        _logger.LogDebug(
            "Fetching messages from Surgewave: offset {From} to {To}",
            fromOffset, toOffset);

        // In a real implementation, this would:
        // 1. Create a temporary consumer (or use a pool)
        // 2. Seek to fromOffset
        // 3. Consume messages until toOffset
        // 4. Return the messages
        //
        // For this demo, we return from the buffer (which mirrors what Surgewave would return).
        // The architecture demonstrates the pattern even though the data source is in-memory.

        var messages = _buffer.GetMessagesInRange(fromOffset + 1, toOffset).ToList();

        _logger.LogDebug("Fetched {Count} messages from Surgewave", messages.Count);

        return await Task.FromResult<IReadOnlyList<(long Offset, VehiclePosition Position)>>(messages);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const string groupId = "fleet-dashboard";

        _logger.LogInformation("Fleet Data Service starting...");
        _logger.LogInformation("Connecting to Surgewave broker at {Broker}", BrokerAddress);

        try
        {
            // Create Surgewave client
            _client = await SurgewaveClient.Create(BrokerAddress)
                .WithClientId("fleet-dashboard")
                .UseSurgewaveProtocol()
                .BuildAsync();

            _isConnected = true;
            _logger.LogInformation("Connected to Surgewave broker. Protocol: {Protocol}", _client.Protocol);

            // Create consumer
            _consumer = _client.CreateConsumer<string, VehiclePosition>(options =>
            {
                options.GroupId = groupId;
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.EnableAutoCommit = false;
                options.ValueDeserializer = Serializers.JsonDeserializer<VehiclePosition>();
            });

            // Subscribe to topic
            await _consumer.SubscribeAsync(stoppingToken, TopicName);
            _logger.LogInformation("Subscribed to topic: {Topic} with {PartitionCount} partitions",
                TopicName, _consumer.Assignment.Count);

            // Consume messages into buffer
            // Messages are stored for:
            // 1. Real-time live updates
            // 2. Creating periodic snapshots
            // 3. Serving delta queries for time-travel
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _consumer.ConsumeAsync(
                        timeout: TimeSpan.FromSeconds(1),
                        cancellationToken: stoppingToken);

                    if (result?.Value != null)
                    {
                        _logger.LogDebug("Received message at offset {Offset}: VehicleId={VehicleId}",
                            result.Offset, result.Value.VehicleId);
                        _buffer.Add(result.Offset, result.Value);
                    }
                    else if (result != null)
                    {
                        _logger.LogWarning("Received message at offset {Offset} with NULL value", result.Offset);
                    }
                    else
                    {
                        // Log every 10 seconds when no messages (to verify loop is still running)
                        if (_buffer.LatestOffset >= 0 && DateTime.UtcNow.Second % 10 == 0)
                        {
                            _logger.LogDebug("Consumer polling... Latest offset: {Latest}", _buffer.LatestOffset);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error consuming message");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Surgewave broker");
            _isConnected = false;
        }
        finally
        {
            if (_consumer != null)
            {
                await _consumer.DisposeAsync();
            }
            if (_client != null)
            {
                await _client.DisposeAsync();
            }
            _isConnected = false;
        }

        _logger.LogInformation("Fleet Data Service stopped");
    }
}
