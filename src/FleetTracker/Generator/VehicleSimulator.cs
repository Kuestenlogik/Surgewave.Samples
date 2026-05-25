using Kuestenlogik.Surgewave.Samples.FleetTracker.Shared;

namespace Kuestenlogik.Surgewave.Samples.FleetTracker.Generator;

/// <summary>
/// Simulates a vehicle moving around a city with realistic GPS updates.
/// </summary>
public sealed class VehicleSimulator
{
    private static readonly string[] DriverNames =
    [
        "Anna Schmidt", "Max Mueller", "Sophie Weber", "Lukas Fischer",
        "Emma Bauer", "Leon Wagner", "Mia Becker", "Paul Hoffmann",
        "Lena Schulz", "Felix Koch", "Laura Richter", "Tim Klein",
        "Julia Wolf", "Niklas Braun", "Sarah Neumann", "Jan Zimmermann",
        "Lisa Krueger", "David Lange", "Hannah Werner", "Moritz Peters"
    ];

    private readonly string _vehicleId;
    private readonly string _driverName;
    private readonly Random _random;

    private double _latitude;
    private double _longitude;
    private double _heading;
    private double _speed;
    private string _status;
    private int _ticksSinceStatusChange;

    /// <summary>
    /// Creates a new vehicle simulator.
    /// </summary>
    /// <param name="vehicleId">Unique vehicle identifier.</param>
    /// <param name="startLatitude">Starting latitude.</param>
    /// <param name="startLongitude">Starting longitude.</param>
    /// <param name="seed">Random seed for reproducible simulation.</param>
    public VehicleSimulator(string vehicleId, double startLatitude, double startLongitude, int seed)
    {
        _vehicleId = vehicleId;
        _random = new Random(seed);
        _latitude = startLatitude;
        _longitude = startLongitude;
        _heading = _random.NextDouble() * 360;
        _speed = 30 + _random.NextDouble() * 40; // 30-70 km/h
        _status = "moving";
        _ticksSinceStatusChange = 0;
        _driverName = DriverNames[seed % DriverNames.Length];
    }

    /// <summary>
    /// Advances the simulation by one tick and returns the new position.
    /// </summary>
    public VehiclePosition Tick()
    {
        _ticksSinceStatusChange++;

        // Randomly change status
        if (_ticksSinceStatusChange > 10 && _random.NextDouble() < 0.05)
        {
            _status = _status switch
            {
                "moving" => _random.NextDouble() < 0.7 ? "stopped" : "idle",
                "stopped" => "moving",
                "idle" => "moving",
                _ => "moving"
            };
            _ticksSinceStatusChange = 0;
        }

        if (_status == "moving")
        {
            // Gradually change heading (simulate turns)
            _heading += (_random.NextDouble() - 0.5) * 30;
            _heading = (_heading + 360) % 360;

            // Vary speed slightly
            _speed += (_random.NextDouble() - 0.5) * 10;
            _speed = Math.Clamp(_speed, 20, 80);

            // Move vehicle (approximate meters per second to degrees)
            var speedMs = _speed / 3.6; // km/h to m/s
            var distanceMeters = speedMs; // 1 second tick

            // Convert to lat/lon change (rough approximation)
            var latChange = distanceMeters * Math.Cos(_heading * Math.PI / 180) / 111000;
            var lonChange = distanceMeters * Math.Sin(_heading * Math.PI / 180) / (111000 * Math.Cos(_latitude * Math.PI / 180));

            _latitude += latChange;
            _longitude += lonChange;

            // Keep within Berlin area bounds (roughly)
            _latitude = Math.Clamp(_latitude, 52.35, 52.65);
            _longitude = Math.Clamp(_longitude, 13.1, 13.7);

            // Bounce off boundaries
            if (_latitude <= 52.35 || _latitude >= 52.65)
                _heading = 180 - _heading;
            if (_longitude <= 13.1 || _longitude >= 13.7)
                _heading = 360 - _heading;
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
}
