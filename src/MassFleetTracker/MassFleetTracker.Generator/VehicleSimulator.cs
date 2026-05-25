using MassFleetTracker.Shared;

namespace MassFleetTracker.Generator;

/// <summary>
/// Lightweight vehicle simulator optimized for 100k+ concurrent instances.
/// Uses struct-based state to minimize allocations.
/// </summary>
public sealed class VehicleSimulator
{
    // Pre-generated driver name pool to avoid string allocations
    private static readonly string[] DriverNames = GenerateDriverNames(1000);

    private readonly string _vehicleId;
    private readonly string _driverName;
    private readonly int _seed;

    // Mutable state - use value types for cache efficiency
    private double _latitude;
    private double _longitude;
    private double _heading;
    private double _speed;
    private VehicleStatus _status;
    private int _ticksSinceStatusChange;
    private uint _randomState;

    /// <summary>
    /// Creates a new vehicle simulator.
    /// </summary>
    public VehicleSimulator(int vehicleIndex, double startLatitude, double startLongitude)
    {
        _vehicleId = $"V{vehicleIndex:D6}"; // Compact ID format
        _seed = vehicleIndex;
        _randomState = (uint)(vehicleIndex + 1) * 2654435761u; // Knuth multiplicative hash

        _latitude = startLatitude;
        _longitude = startLongitude;
        _heading = NextRandomDouble() * 360;
        _speed = 30 + NextRandomDouble() * 40;
        _status = VehicleStatus.Moving;
        _ticksSinceStatusChange = 0;
        _driverName = DriverNames[vehicleIndex % DriverNames.Length];
    }

    /// <summary>
    /// Vehicle ID for partitioning.
    /// </summary>
    public string VehicleId => _vehicleId;

    /// <summary>
    /// Advances simulation by one tick and returns the position.
    /// </summary>
    public VehiclePosition Tick()
    {
        _ticksSinceStatusChange++;

        // Status change with low probability
        if (_ticksSinceStatusChange > 10 && NextRandomDouble() < 0.05)
        {
            _status = _status switch
            {
                VehicleStatus.Moving => NextRandomDouble() < 0.7 ? VehicleStatus.Stopped : VehicleStatus.Idling,
                VehicleStatus.Stopped => VehicleStatus.Moving,
                VehicleStatus.Idling => VehicleStatus.Moving,
                _ => VehicleStatus.Moving
            };
            _ticksSinceStatusChange = 0;
        }

        if (_status == VehicleStatus.Moving)
        {
            // Heading changes (turns)
            _heading += (NextRandomDouble() - 0.5) * 30;
            _heading = (_heading % 360 + 360) % 360;

            // Speed variation
            _speed += (NextRandomDouble() - 0.5) * 10;
            _speed = Math.Clamp(_speed, 20, 80);

            // Position update
            var speedMs = _speed / 3.6;
            var latChange = speedMs * Math.Cos(_heading * Math.PI / 180) / 111000;
            var lonChange = speedMs * Math.Sin(_heading * Math.PI / 180) / (111000 * Math.Cos(_latitude * Math.PI / 180));

            _latitude += latChange;
            _longitude += lonChange;

            // Clamp to Berlin area and bounce
            if (_latitude <= 52.35) { _latitude = 52.35; _heading = 180 - _heading; }
            if (_latitude >= 52.65) { _latitude = 52.65; _heading = 180 - _heading; }
            if (_longitude <= 13.1) { _longitude = 13.1; _heading = 360 - _heading; }
            if (_longitude >= 13.7) { _longitude = 13.7; _heading = 360 - _heading; }
        }
        else
        {
            _speed = 0;
        }

        return new VehiclePosition
        {
            VehicleId = _vehicleId,
            Latitude = _latitude,
            Longitude = _longitude,
            Speed = Math.Round(_speed, 1),
            Heading = Math.Round(_heading, 1),
            Timestamp = DateTimeOffset.UtcNow,
            Status = _status,
            DriverName = _driverName
        };
    }

    // Fast xorshift random - avoids System.Random overhead
    private double NextRandomDouble()
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 17;
        _randomState ^= _randomState << 5;
        return (_randomState & 0x7FFFFFFF) / (double)0x7FFFFFFF;
    }

    private static string[] GenerateDriverNames(int count)
    {
        var firstNames = new[] { "Anna", "Max", "Sophie", "Lukas", "Emma", "Leon", "Mia", "Paul", "Lena", "Felix" };
        var lastNames = new[] { "Mueller", "Schmidt", "Weber", "Fischer", "Bauer", "Wagner", "Becker", "Hoffmann", "Schulz", "Koch" };

        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = $"{firstNames[i % firstNames.Length]} {lastNames[i / firstNames.Length % lastNames.Length]}";
        }
        return names;
    }
}
