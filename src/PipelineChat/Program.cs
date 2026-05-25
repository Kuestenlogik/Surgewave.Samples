#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable CA1812 // Internal classes instantiated via JSON deserialization
#pragma warning disable CA2234 // Pass System.Uri objects instead of strings

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

AnsiConsole.Write(new FigletText("Pipeline Chat").Color(Color.DeepSkyBlue1));
AnsiConsole.MarkupLine("[grey]Interactive chat with Surgewave AI pipelines via the Pipeline Chat API[/]\n");

// Configuration
var brokerUrl = Environment.GetEnvironmentVariable("Surgewave_BROKER_URL") ?? "http://localhost:5000";
var pipelineId = Environment.GetEnvironmentVariable("Surgewave_PIPELINE_ID") ?? "echo-pipeline";

AnsiConsole.MarkupLine("[yellow]Configuration:[/]");
AnsiConsole.MarkupLine($"  Broker URL:  [cyan]{brokerUrl}[/]");
AnsiConsole.MarkupLine($"  Pipeline ID: [cyan]{pipelineId}[/]");
AnsiConsole.MarkupLine("[grey]  Set Surgewave_BROKER_URL and Surgewave_PIPELINE_ID to customize.[/]\n");

using var httpClient = new HttpClient { BaseAddress = new Uri(brokerUrl) };
httpClient.Timeout = TimeSpan.FromSeconds(60);

var sessionId = Guid.NewGuid().ToString("N");
var streamingMode = false;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

// Test connectivity
AnsiConsole.MarkupLine("[yellow]Testing connection to Surgewave broker...[/]");

try
{
    var topicResponse = await httpClient.GetAsync($"/api/pipelines/{pipelineId}/chat/topics");
    if (topicResponse.IsSuccessStatusCode)
    {
        var topicInfo = await topicResponse.Content.ReadFromJsonAsync<ChatTopicInfo>(jsonOptions);
        AnsiConsole.MarkupLine($"[green]Connected! Signal topic: {topicInfo?.SignalTopic}, Response topic: {topicInfo?.ResponseTopic}[/]\n");
    }
    else
    {
        AnsiConsole.MarkupLine($"[yellow]Warning: Pipeline '{pipelineId}' returned {topicResponse.StatusCode}.[/]");
        AnsiConsole.MarkupLine("[grey]The broker may not be running or the pipeline may not exist.[/]");
        AnsiConsole.MarkupLine("[grey]Continuing in demo mode -- commands will be shown but calls may fail.[/]\n");
    }
}
catch (HttpRequestException)
{
    AnsiConsole.MarkupLine("[yellow]Warning: Cannot reach Surgewave broker.[/]");
    AnsiConsole.MarkupLine("[grey]Start the broker with Connect enabled:[/]");
    AnsiConsole.MarkupLine("[grey]  dotnet run --project src/Kuestenlogik.Surgewave.Broker -- --Surgewave:Connect:Enabled=true[/]");
    AnsiConsole.MarkupLine("[grey]Continuing in demo mode -- commands will be shown but calls may fail.[/]\n");
}

ShowHelp();

while (true)
{
    AnsiConsole.Markup($"[cyan][{pipelineId}][/] [yellow]>[/] ");
    var input = Console.ReadLine();

    if (input is null || string.Equals(input.Trim(), "/quit", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[green]Goodbye![/]");
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    // Handle commands
    if (input.StartsWith('/'))
    {
        var parts = input.Split(' ', 2);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/stream":
                streamingMode = !streamingMode;
                AnsiConsole.MarkupLine($"  Streaming mode: [cyan]{(streamingMode ? "ON" : "OFF")}[/]");
                continue;

            case "/sessions":
                await ListSessionsAsync();
                continue;

            case "/history":
                await GetHistoryAsync();
                continue;

            case "/new":
                sessionId = Guid.NewGuid().ToString("N");
                AnsiConsole.MarkupLine($"  New session: [cyan]{sessionId}[/]");
                continue;

            case "/session":
                AnsiConsole.MarkupLine($"  Current session: [cyan]{sessionId}[/]");
                continue;

            case "/pipeline":
                if (parts.Length > 1)
                {
                    pipelineId = parts[1].Trim();
                    sessionId = Guid.NewGuid().ToString("N");
                    AnsiConsole.MarkupLine($"  Switched to pipeline: [cyan]{pipelineId}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  Current pipeline: [cyan]{pipelineId}[/]");
                }
                continue;

            case "/delete":
                await DeleteSessionAsync();
                continue;

            case "/async":
                if (parts.Length > 1)
                {
                    await SendAsyncMessageAsync(parts[1]);
                }
                else
                {
                    AnsiConsole.MarkupLine("  Usage: /async <message>");
                }
                continue;

            case "/help":
                ShowHelp();
                continue;

            default:
                AnsiConsole.MarkupLine($"  Unknown command: {command}. Type /help for available commands.");
                continue;
        }
    }

    // Send chat message
    if (streamingMode)
    {
        await SendStreamingMessageAsync(input);
    }
    else
    {
        await SendMessageAsync(input);
    }
}

// ──────────────────────────────────────────────────────────────
// API helper methods
// ──────────────────────────────────────────────────────────────

async Task SendMessageAsync(string message)
{
    try
    {
        var request = new ChatRequest { Message = message, SessionId = sessionId };
        var response = await httpClient.PostAsJsonAsync(
            $"/api/pipelines/{pipelineId}/chat", request, jsonOptions);

        if (response.IsSuccessStatusCode)
        {
            var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(jsonOptions);
            if (chatResponse is not null)
            {
                AnsiConsole.MarkupLine($"  [green]Assistant:[/] {Markup.Escape(chatResponse.Content)}");
                AnsiConsole.MarkupLine($"  [grey]Message ID: {chatResponse.MessageId} | {chatResponse.Timestamp:HH:mm:ss}[/]");
            }
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            AnsiConsole.MarkupLine($"  [red]Error ({response.StatusCode}):[/] {Markup.Escape(error)}");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

async Task SendStreamingMessageAsync(string message)
{
    try
    {
        var request = new ChatRequest { Message = message, SessionId = sessionId };
        var requestContent = JsonContent.Create(request, options: jsonOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/api/pipelines/{pipelineId}/chat/stream")
        {
            Content = requestContent
        };

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            AnsiConsole.MarkupLine($"  [red]Error ({response.StatusCode}):[/] {Markup.Escape(error)}");
            return;
        }

        AnsiConsole.Markup("  [green]Assistant:[/] ");

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                var jsonData = line[6..];
                var evt = JsonSerializer.Deserialize<ChatStreamEventDto>(jsonData, jsonOptions);

                if (evt?.Type == "token" && evt.Content is not null)
                {
                    AnsiConsole.Markup(Markup.Escape(evt.Content));
                }
                else if (evt?.Type == "done")
                {
                    break;
                }
                else if (evt?.Type == "error")
                {
                    AnsiConsole.MarkupLine($"\n  [red]Stream error:[/] {Markup.Escape(evt.Error ?? "Unknown")}");
                }
            }
        }

        AnsiConsole.WriteLine();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"\n  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

async Task SendAsyncMessageAsync(string message)
{
    try
    {
        var request = new ChatRequest { Message = message, SessionId = sessionId };
        var response = await httpClient.PostAsJsonAsync(
            $"/api/pipelines/{pipelineId}/chat/async", request, jsonOptions);

        if (response.IsSuccessStatusCode)
        {
            var asyncResponse = await response.Content.ReadFromJsonAsync<AsyncChatResponse>(jsonOptions);
            if (asyncResponse is not null)
            {
                AnsiConsole.MarkupLine($"  [green]Message queued.[/] Session: {asyncResponse.SessionId}, Message ID: {asyncResponse.MessageId}");
                AnsiConsole.MarkupLine("  [grey]Use /history to check for the response.[/]");
            }
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            AnsiConsole.MarkupLine($"  [red]Error ({response.StatusCode}):[/] {Markup.Escape(error)}");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

async Task ListSessionsAsync()
{
    try
    {
        var response = await httpClient.GetAsync($"/api/pipelines/{pipelineId}/chat/sessions");

        if (response.IsSuccessStatusCode)
        {
            var sessions = await response.Content.ReadFromJsonAsync<ChatSessionListResponse>(jsonOptions);

            if (sessions?.Sessions is null || sessions.Sessions.Count == 0)
            {
                AnsiConsole.MarkupLine("  [grey]No active sessions.[/]");
            }
            else
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Session ID")
                    .AddColumn("Messages")
                    .AddColumn("Pending")
                    .AddColumn("Created")
                    .AddColumn("Last Activity");

                foreach (var s in sessions.Sessions)
                {
                    var marker = s.SessionId == sessionId ? " *" : "";
                    table.AddRow(
                        $"[cyan]{s.SessionId}{marker}[/]",
                        s.MessageCount.ToString(),
                        s.PendingCount.ToString(),
                        s.CreatedAt.ToString("HH:mm:ss"),
                        s.LastActivityAt.ToString("HH:mm:ss"));
                }

                AnsiConsole.Write(table);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"  [red]Error ({response.StatusCode})[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

async Task GetHistoryAsync()
{
    try
    {
        var response = await httpClient.GetAsync(
            $"/api/pipelines/{pipelineId}/chat/sessions/{sessionId}/history");

        if (response.IsSuccessStatusCode)
        {
            var history = await response.Content.ReadFromJsonAsync<ChatHistoryResponse>(jsonOptions);

            if (history?.Messages is null || history.Messages.Count == 0)
            {
                AnsiConsole.MarkupLine("  [grey]No messages in this session.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"  Session [cyan]{history.SessionId}[/] ({history.Messages.Count} messages):\n");

                foreach (var msg in history.Messages)
                {
                    var roleColor = msg.Role switch
                    {
                        "user" => "yellow",
                        "assistant" => "green",
                        _ => "grey"
                    };

                    AnsiConsole.MarkupLine($"    [{roleColor}][{msg.Role}][/] {Markup.Escape(msg.Content)}");
                    AnsiConsole.MarkupLine($"    [grey]{msg.Timestamp:HH:mm:ss} | {msg.Id}[/]\n");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"  [red]Session not found or error ({response.StatusCode})[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

async Task DeleteSessionAsync()
{
    try
    {
        var response = await httpClient.DeleteAsync(
            $"/api/pipelines/{pipelineId}/chat/sessions/{sessionId}");

        if (response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine($"  [green]Session {sessionId} deleted.[/]");
            sessionId = Guid.NewGuid().ToString("N");
            AnsiConsole.MarkupLine($"  New session: [cyan]{sessionId}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"  [red]Error ({response.StatusCode})[/]");
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
}

static void ShowHelp()
{
    AnsiConsole.Write(new Panel(new Markup(
        "[bold]Pipeline Chat API Demo[/]\n\n" +
        "This sample demonstrates the Surgewave Pipeline Chat REST API.\n" +
        "Type a message to send it to the configured pipeline.\n\n" +
        "[yellow]Commands:[/]\n" +
        "  [cyan]/stream[/]            Toggle streaming mode (SSE)\n" +
        "  [cyan]/async <msg>[/]       Send fire-and-forget message\n" +
        "  [cyan]/sessions[/]          List active chat sessions\n" +
        "  [cyan]/history[/]           Show current session history\n" +
        "  [cyan]/session[/]           Show current session ID\n" +
        "  [cyan]/new[/]               Start a new session\n" +
        "  [cyan]/delete[/]            Delete current session\n" +
        "  [cyan]/pipeline <id>[/]     Switch to a different pipeline\n" +
        "  [cyan]/help[/]              Show this help\n" +
        "  [cyan]/quit[/]              Exit\n\n" +
        "[yellow]Prerequisites:[/]\n" +
        "  Start the Surgewave broker with Connect enabled:\n" +
        "  [grey]dotnet run --project src/Kuestenlogik.Surgewave.Broker -- --Surgewave:Connect:Enabled=true[/]"))
        .Header("[cyan]Help[/]")
        .Border(BoxBorder.Rounded));
    AnsiConsole.WriteLine();
}

// ──────────────────────────────────────────────────────────────
// DTOs for JSON serialization
// ──────────────────────────────────────────────────────────────

internal sealed record ChatRequest
{
    public required string Message { get; init; }
    public string? SessionId { get; init; }
}

internal sealed record ChatResponse
{
    public required string SessionId { get; init; }
    public required string MessageId { get; init; }
    public required string Content { get; init; }
    public required string Role { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

internal sealed record AsyncChatResponse
{
    public required string SessionId { get; init; }
    public required string MessageId { get; init; }
}

internal sealed record ChatTopicInfo
{
    public string? SignalTopic { get; init; }
    public string? ResponseTopic { get; init; }
}

internal sealed record ChatStreamEventDto
{
    public string? Type { get; init; }
    public string? Content { get; init; }
    public string? Error { get; init; }
}

internal sealed record ChatSessionListResponse
{
    public required string PipelineId { get; init; }
    public required IReadOnlyList<ChatSessionSummary> Sessions { get; init; }
}

internal sealed record ChatSessionSummary
{
    public required string SessionId { get; init; }
    public int MessageCount { get; init; }
    public int PendingCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
}

internal sealed record ChatHistoryResponse
{
    public required string SessionId { get; init; }
    public required string PipelineId { get; init; }
    public required IReadOnlyList<ChatMessageDto> Messages { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; init; }
}

internal sealed record ChatMessageDto
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
