#pragma warning disable CA1031 // Do not catch general exception types

using Kuestenlogik.Surgewave.AI.Agents;
using Kuestenlogik.Surgewave.AI.Agents.Caching;
using Kuestenlogik.Surgewave.AI.Agents.Memory;
using Kuestenlogik.Surgewave.AI.Agents.Tools;
using Spectre.Console;

AnsiConsole.Write(new FigletText("Agent Memory").Color(Color.Gold1));
AnsiConsole.MarkupLine("[grey]Memory Store | Recall | Summarization | Tool Caching[/]\n");

// ──────────────────────────────────────────────────────────────
// 1. Create an InMemoryAgentMemoryStore
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]1. Memory Store Setup[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Creating in-memory store and agent memory context.[/]\n");

var memoryStore = new InMemoryAgentMemoryStore();
var memoryOptions = new MemoryOptions
{
    Enabled = true,
    MaxMemoriesPerAgent = 100,
    ConversationSummaryThreshold = 5,
    AutoSummarizeConversations = true,
};

const string agentId = "demo-agent";
var memoryContext = new AgentMemoryContext(memoryStore, agentId, memoryOptions);

AnsiConsole.MarkupLine($"  Agent ID:            [cyan]{agentId}[/]");
AnsiConsole.MarkupLine($"  Max memories:        [cyan]{memoryOptions.MaxMemoriesPerAgent}[/]");
AnsiConsole.MarkupLine($"  Summary threshold:   [cyan]{memoryOptions.ConversationSummaryThreshold}[/]");
AnsiConsole.MarkupLine($"  Auto-summarize:      [cyan]{memoryOptions.AutoSummarizeConversations}[/]");
AnsiConsole.MarkupLine("[green]  Store initialized.[/]\n");

// ──────────────────────────────────────────────────────────────
// 2. Save different types of memories
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]2. Saving Memories[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Storing facts, preferences, and episodic memories.[/]\n");

// Save learned facts
var facts = new (string Content, float Importance)[]
{
    ("Surgewave is a high-performance message broker built with .NET 10", 0.9f),
    ("Surgewave supports Kafka-compatible protocols for easy migration", 0.8f),
    ("Guardrails can detect PII, toxicity, and prompt injection", 0.7f),
    ("The Pipeline Chat API supports both sync and streaming modes", 0.6f),
    ("Surgewave uses zero-copy techniques for maximum throughput", 0.85f),
};

foreach (var (content, importance) in facts)
{
    await memoryContext.SaveFactAsync(content, importance);
    AnsiConsole.MarkupLine($"  [green]+[/] Fact (importance={importance:F1}): [grey]{content}[/]");
}

AnsiConsole.WriteLine();

// Save user preferences
var preferences = new[]
{
    "User prefers concise answers over verbose explanations",
    "User likes code examples in C# and TypeScript",
    "User wants performance metrics included when relevant",
};

foreach (var pref in preferences)
{
    await memoryContext.SavePreferenceAsync(pref);
    AnsiConsole.MarkupLine($"  [green]+[/] Preference: [grey]{pref}[/]");
}

AnsiConsole.WriteLine();

// Save episodic memories
var sessionId = Guid.NewGuid().ToString("N");
var episodes = new[]
{
    "User asked about Surgewave vs Kafka performance comparison",
    "User deployed a 3-node Surgewave cluster on Kubernetes",
    "User configured guardrails for a customer-facing chatbot",
};

foreach (var episode in episodes)
{
    await memoryContext.SaveEpisodeAsync(episode, sessionId);
    AnsiConsole.MarkupLine($"  [green]+[/] Episode (session={sessionId[..8]}...): [grey]{episode}[/]");
}

AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 3. Recall memories by text query
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]3. Recall by Text Query[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Searching memories by content relevance.[/]\n");

var queries = new[] { "Surgewave performance", "guardrails", "Kubernetes" };

foreach (var query in queries)
{
    var results = await memoryContext.RecallAsync(query, maxResults: 3);

    AnsiConsole.MarkupLine($"  Query: [yellow]\"{query}\"[/] -> {results.Count} result(s)");

    foreach (var entry in results)
    {
        var typeColor = entry.Type switch
        {
            MemoryType.LearnedFact => "cyan",
            MemoryType.UserPreference => "green",
            MemoryType.Episodic => "yellow",
            _ => "grey"
        };

        AnsiConsole.MarkupLine($"    [{typeColor}]{entry.Type}[/] (importance={entry.Importance:F1}): {entry.Content}");
    }

    AnsiConsole.WriteLine();
}

// ──────────────────────────────────────────────────────────────
// 4. Recall memories by type
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]4. Recall by Memory Type[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Filtering memories by their classification type.[/]\n");

var memoryTypes = new[] { MemoryType.LearnedFact, MemoryType.UserPreference, MemoryType.Episodic };

foreach (var memType in memoryTypes)
{
    var results = await memoryContext.RecallByTypeAsync(memType, maxResults: 10);

    var typeColor = memType switch
    {
        MemoryType.LearnedFact => "cyan",
        MemoryType.UserPreference => "green",
        MemoryType.Episodic => "yellow",
        _ => "grey"
    };

    AnsiConsole.MarkupLine($"  [{typeColor}]{memType}[/]: {results.Count} memor{(results.Count == 1 ? "y" : "ies")}");

    foreach (var entry in results)
    {
        AnsiConsole.MarkupLine($"    - {entry.Content}");
    }

    AnsiConsole.WriteLine();
}

// ──────────────────────────────────────────────────────────────
// 5. Memory Summary
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]5. Memory Summary[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Statistical overview of stored memories.[/]\n");

var summary = await memoryContext.GetSummaryAsync();

var summaryTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Metric")
    .AddColumn("Value");

summaryTable.AddRow("Total Memories", summary.TotalMemories.ToString());

foreach (var (type, count) in summary.ByType)
{
    summaryTable.AddRow($"  {type}", count.ToString());
}

if (summary.OldestMemory.HasValue)
{
    summaryTable.AddRow("Oldest Memory", summary.OldestMemory.Value.ToString("yyyy-MM-dd HH:mm:ss"));
}

if (summary.NewestMemory.HasValue)
{
    summaryTable.AddRow("Newest Memory", summary.NewestMemory.Value.ToString("yyyy-MM-dd HH:mm:ss"));
}

AnsiConsole.Write(summaryTable);
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 6. Conversation Summarization
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]6. Conversation Summarization[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]Extractive summarization of conversation history (no LLM required).[/]\n");

var conversationHistory = new List<AgentMessage>
{
    new() { Content = "What is Surgewave and how does it compare to Kafka?", Role = "user" },
    new() { Content = "Surgewave is a high-performance message broker built with .NET 10 that aims to be a drop-in replacement for Kafka with lower operational complexity.", Role = "assistant" },
    new() { Content = "What about throughput performance?", Role = "user" },
    new() { Content = "Surgewave uses zero-copy techniques and aims to be competitive with Aeron for transportation throughput and latency.", Role = "assistant" },
    new() { Content = "Can I use my existing Kafka clients?", Role = "user" },
    new() { Content = "Yes, Surgewave provides Kafka-compatible protocol support. You can use Confluent.Kafka clients with minimal code changes.", Role = "assistant" },
    new() { Content = "How do I set up guardrails for AI pipelines?", Role = "user" },
    new() { Content = "Surgewave AI Guardrails provides PII detection, toxicity filtering, and prompt injection detection. You can chain them in a GuardrailPipeline.", Role = "assistant" },
    new() { Content = "What about deployment on Kubernetes?", Role = "user" },
    new() { Content = "Surgewave provides Helm charts and Kubernetes manifests in the Surgewave.Templates repository for easy cluster deployment.", Role = "assistant" },
    new() { Content = "Great, I'll deploy a 3-node cluster this weekend.", Role = "user" },
    new() { Content = "Sounds good! Make sure to configure replication factor and check the monitoring dashboards for cluster health.", Role = "assistant" },
};

AnsiConsole.MarkupLine($"  Conversation: [cyan]{conversationHistory.Count}[/] messages\n");

foreach (var msg in conversationHistory)
{
    var roleColor = msg.Role == "user" ? "yellow" : "green";
    var preview = msg.Content.Length > 70
        ? string.Concat(msg.Content.AsSpan(0, 67), "...")
        : msg.Content;
    AnsiConsole.MarkupLine($"    [{roleColor}][[{msg.Role}]][/] {Markup.Escape(preview)}");
}

AnsiConsole.WriteLine();

var conversationSummary = ConversationSummarizer.Summarize(conversationHistory);

AnsiConsole.Write(new Panel(new Markup(Markup.Escape(conversationSummary)))
    .Header("[cyan]Summary[/]")
    .Border(BoxBorder.Rounded)
    .BorderColor(Color.Cyan1));
AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 7. Tool Result Caching
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]7. Tool Result Caching[/]").LeftJustified());
AnsiConsole.MarkupLine("[grey]CachedAgentTool decorator transparently caches tool invocations.[/]\n");

var cacheOptions = new ToolCacheOptions
{
    Enabled = true,
    DefaultTtl = TimeSpan.FromMinutes(5),
    MaxCachedEntries = 100,
};

var cache = new InMemoryToolResultCache(cacheOptions);
var innerTool = new DemoWeatherTool();
var cachedTool = new CachedAgentTool(innerTool, cache, cacheOptions.DefaultTtl);

AnsiConsole.MarkupLine($"  Tool: [cyan]{cachedTool.Name}[/] - {cachedTool.Description}");
AnsiConsole.MarkupLine($"  Cache TTL: [cyan]{cacheOptions.DefaultTtl.TotalMinutes} minutes[/]");
AnsiConsole.MarkupLine($"  Max entries: [cyan]{cacheOptions.MaxCachedEntries}[/]\n");

// Simulate repeated tool calls
var toolCalls = new[]
{
    new Dictionary<string, object?> { ["city"] = "Berlin" },
    new Dictionary<string, object?> { ["city"] = "Tokyo" },
    new Dictionary<string, object?> { ["city"] = "Berlin" },   // cache hit
    new Dictionary<string, object?> { ["city"] = "New York" },
    new Dictionary<string, object?> { ["city"] = "Tokyo" },    // cache hit
    new Dictionary<string, object?> { ["city"] = "Berlin" },   // cache hit
};

foreach (var toolArgs in toolCalls)
{
    var statsBefore = await cache.GetStatsAsync();
    var result = await cachedTool.InvokeAsync(toolArgs);
    var statsAfter = await cache.GetStatsAsync();

    var wasHit = statsAfter.Hits > statsBefore.Hits;
    var hitLabel = wasHit ? "[green]HIT[/]" : "[yellow]MISS[/]";

    AnsiConsole.MarkupLine($"  {hitLabel}  city={toolArgs["city"],-12} -> {result.Content}");
}

AnsiConsole.WriteLine();

// ──────────────────────────────────────────────────────────────
// 8. Cache Statistics
// ──────────────────────────────────────────────────────────────
AnsiConsole.Write(new Rule("[cyan]8. Cache Statistics[/]").LeftJustified());

var finalStats = await cache.GetStatsAsync();

var cacheTable = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("Metric")
    .AddColumn("Value");

cacheTable.AddRow("Total Entries", finalStats.TotalEntries.ToString());
cacheTable.AddRow("Cache Hits", finalStats.Hits.ToString());
cacheTable.AddRow("Cache Misses", finalStats.Misses.ToString());
cacheTable.AddRow("Hit Rate", $"{finalStats.HitRate:P1}");

AnsiConsole.Write(cacheTable);
AnsiConsole.MarkupLine($"\n[green]Demo complete![/]");

// ──────────────────────────────────────────────────────────────
// Demo tool implementation
// ──────────────────────────────────────────────────────────────

/// <summary>
/// A simple demo tool that returns weather data for a city.
/// </summary>
internal sealed class DemoWeatherTool : IAgentTool
{
    private static readonly Dictionary<string, string> WeatherData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Berlin"] = "Partly cloudy, 18C, wind 12 km/h NW",
        ["Tokyo"] = "Clear skies, 24C, wind 5 km/h SE",
        ["New York"] = "Rainy, 15C, wind 20 km/h E",
        ["London"] = "Overcast, 12C, wind 8 km/h SW",
    };

    public string Name => "get_weather";

    public string Description => "Gets current weather for a city";

    public AgentToolSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, AgentToolParameter>
        {
            ["city"] = new() { Type = "string", Description = "The city name" }
        },
        Required = ["city"]
    };

    public Task<AgentToolResult> InvokeAsync(
        IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var city = arguments.TryGetValue("city", out var c) ? c?.ToString() ?? "Unknown" : "Unknown";

        var weather = WeatherData.TryGetValue(city, out var w)
            ? w
            : $"No data for {city}";

        return Task.FromResult(new AgentToolResult
        {
            Content = $"Weather in {city}: {weather}"
        });
    }
}
