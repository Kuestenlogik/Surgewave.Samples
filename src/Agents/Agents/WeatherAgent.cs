using Kuestenlogik.Surgewave.AI.Agents;

namespace Kuestenlogik.Surgewave.Samples.AgentDemo.Agents;

/// <summary>
/// Weather agent that provides mock weather information.
/// Demonstrates skills, metadata, and artifacts.
/// </summary>
public sealed class WeatherAgent : SurgewaveAgentBase
{
    private static readonly Dictionary<string, WeatherData> MockWeather = new(StringComparer.OrdinalIgnoreCase)
    {
        ["New York"] = new("New York", 22, "Partly cloudy", 65),
        ["London"] = new("London", 15, "Rainy", 85),
        ["Tokyo"] = new("Tokyo", 28, "Sunny", 55),
        ["Sydney"] = new("Sydney", 18, "Windy", 40),
        ["Paris"] = new("Paris", 20, "Overcast", 70)
    };

    public override string AgentId => "weather-agent";

    public override string Name => "Weather Agent";

    public override string Description => "Provides weather information for cities around the world.";

    public override IReadOnlyList<AgentSkill> Skills =>
    [
        new AgentSkill
        {
            Id = "get-weather",
            Name = "Get Weather",
            Description = "Gets current weather for a specified city.",
            Tags = ["weather", "forecast"]
        },
        new AgentSkill
        {
            Id = "list-cities",
            Name = "List Cities",
            Description = "Lists available cities with weather data.",
            Tags = ["weather", "utility"]
        }
    ];

    public override Task<AgentResponse> ProcessMessageAsync(
        AgentMessage message,
        SurgewaveAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var content = message.Content.Trim();

        // Handle list cities command
        if (content.Equals("list", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("cities", StringComparison.OrdinalIgnoreCase))
        {
            var cities = string.Join(", ", MockWeather.Keys);
            return Task.FromResult(TextResponse($"Available cities: {cities}"));
        }

        // Try to find weather for the city
        if (MockWeather.TryGetValue(content, out var weather))
        {
            var response = $"""
                Weather for {weather.City}:
                - Temperature: {weather.TemperatureCelsius}°C ({ToFahrenheit(weather.TemperatureCelsius)}°F)
                - Conditions: {weather.Conditions}
                - Humidity: {weather.HumidityPercent}%
                """;

            // Include weather data as an artifact
            return Task.FromResult(ResponseWithArtifacts(
                response,
                new AgentArtifact
                {
                    Name = "weather-data",
                    Type = "application/json",
                    Content = weather
                }));
        }

        // City not found
        return Task.FromResult(RequireInput(
            $"City '{content}' not found. Type 'list' to see available cities, or enter a city name."));
    }

    private static int ToFahrenheit(int celsius) => (celsius * 9 / 5) + 32;

    private sealed record WeatherData(
        string City,
        int TemperatureCelsius,
        string Conditions,
        int HumidityPercent);
}
