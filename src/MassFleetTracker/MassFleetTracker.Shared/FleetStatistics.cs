namespace MassFleetTracker.Shared;

/// <summary>
/// Aggregated statistics for the entire fleet.
/// </summary>
public sealed class FleetStatistics
{
    /// <summary>
    /// Total number of vehicles being tracked.
    /// </summary>
    public int TotalVehicles { get; set; }

    /// <summary>
    /// Number of vehicles currently moving.
    /// </summary>
    public int MovingVehicles { get; set; }

    /// <summary>
    /// Number of vehicles currently stopped.
    /// </summary>
    public int StoppedVehicles { get; set; }

    /// <summary>
    /// Number of vehicles currently idling.
    /// </summary>
    public int IdlingVehicles { get; set; }

    /// <summary>
    /// Number of offline vehicles.
    /// </summary>
    public int OfflineVehicles { get; set; }

    /// <summary>
    /// Average speed of all moving vehicles (km/h).
    /// </summary>
    public double AverageSpeed { get; set; }

    /// <summary>
    /// Maximum speed observed across all vehicles (km/h).
    /// </summary>
    public double MaxSpeed { get; set; }

    /// <summary>
    /// Total messages processed per second.
    /// </summary>
    public double MessagesPerSecond { get; set; }

    /// <summary>
    /// Total messages processed since start.
    /// </summary>
    public long TotalMessagesProcessed { get; set; }

    /// <summary>
    /// Current consumer lag (messages behind).
    /// </summary>
    public long ConsumerLag { get; set; }

    /// <summary>
    /// Processing latency P50 in milliseconds.
    /// </summary>
    public double LatencyP50Ms { get; set; }

    /// <summary>
    /// Processing latency P99 in milliseconds.
    /// </summary>
    public double LatencyP99Ms { get; set; }

    /// <summary>
    /// Timestamp of this statistics snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a deep copy of this statistics object.
    /// </summary>
    public FleetStatistics Clone() => new()
    {
        TotalVehicles = TotalVehicles,
        MovingVehicles = MovingVehicles,
        StoppedVehicles = StoppedVehicles,
        IdlingVehicles = IdlingVehicles,
        OfflineVehicles = OfflineVehicles,
        AverageSpeed = AverageSpeed,
        MaxSpeed = MaxSpeed,
        MessagesPerSecond = MessagesPerSecond,
        TotalMessagesProcessed = TotalMessagesProcessed,
        ConsumerLag = ConsumerLag,
        LatencyP50Ms = LatencyP50Ms,
        LatencyP99Ms = LatencyP99Ms,
        Timestamp = Timestamp
    };
}
