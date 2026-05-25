using System.Text.Json;

#pragma warning disable CA5394 // Random is not cryptographically secure

namespace Kuestenlogik.Surgewave.Samples.MultiProtocol;

/// <summary>
/// Stock quote message used across all protocols.
/// </summary>
public sealed record StockQuote
{
    public required string Symbol { get; init; }
    public required decimal Price { get; init; }
    public required decimal Change { get; init; }
    public required decimal ChangePercent { get; init; }
    public required long Volume { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Protocol { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static StockQuote? FromJson(string json) =>
        JsonSerializer.Deserialize<StockQuote>(json, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static StockQuote Generate(string symbol, string protocol)
    {
        var basePrice = symbol switch
        {
            "AAPL" => 175.50m,
            "GOOGL" => 140.25m,
            "MSFT" => 378.90m,
            "AMZN" => 178.35m,
            "TSLA" => 248.75m,
            _ => 100.00m
        };

        var change = (decimal)(Random.Shared.NextDouble() * 10 - 5);
        var price = basePrice + change;

        return new StockQuote
        {
            Symbol = symbol,
            Price = Math.Round(price, 2),
            Change = Math.Round(change, 2),
            ChangePercent = Math.Round(change / basePrice * 100, 2),
            Volume = Random.Shared.NextInt64(100_000, 10_000_000),
            Timestamp = DateTimeOffset.UtcNow,
            Protocol = protocol
        };
    }
}
