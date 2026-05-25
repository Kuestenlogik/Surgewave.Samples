using Kuestenlogik.Surgewave.Client;
using Kuestenlogik.Surgewave.Samples.SurgewaveChat;
using Spectre.Console;

// =====================================================================
// Surgewave CHAT -- Real-Time CLI Chat Application
// =====================================================================
// Multi-room chat using Surgewave topics as message channels. Each room
// is a separate topic (e.g., "chat-general"). Unique consumer group
// IDs per user enable broadcast delivery -- every user receives
// every message. Demonstrates pub/sub patterns with Surgewave.
// =====================================================================

const string brokerAddress = "localhost:9092";

AnsiConsole.Write(new FigletText("Surgewave Chat").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[grey]Real-time CLI chat using Surgewave Native Protocol[/]\n");

// Get username
var username = AnsiConsole.Prompt(
    new TextPrompt<string>("[cyan]Enter your username:[/]")
        .PromptStyle("green")
        .Validate(name =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return ValidationResult.Error("[red]Username cannot be empty[/]");
            if (name.Length > 20)
                return ValidationResult.Error("[red]Username too long (max 20 chars)[/]");
            if (name.Contains(' '))
                return ValidationResult.Error("[red]Username cannot contain spaces[/]");
            return ValidationResult.Success();
        }));

AnsiConsole.MarkupLine($"\n[green]Welcome, {username}![/]");
AnsiConsole.MarkupLine("[grey]Connecting to Surgewave broker...[/]");

try
{
    // ============= STEP 1: Connect to Surgewave =============
    // Each chat client gets a unique client ID to avoid conflicts.
    // UseSurgewaveProtocol() provides sub-millisecond message delivery
    // for instant chat experience.
    await using var client = await SurgewaveClient.Create(brokerAddress)
        .WithClientId($"chat-{username}-{Guid.NewGuid():N}"[..32])
        .UseSurgewaveProtocol()
        .BuildAsync();

    AnsiConsole.MarkupLine($"[green]Connected![/] Protocol: {client.Protocol}\n");

    // Create chat client
    await using var chat = new ChatClient(client, username);

    // Subscribe to messages
    chat.OnMessageReceived += message =>
    {
        // Don't show own messages (we already see them when typing)
        if (message.Username == username && message.Type == "message")
            return;

        var color = message.Type switch
        {
            "join" => "green",
            "leave" => "yellow",
            "system" => "grey",
            _ => GetUserColor(message.Username)
        };

        var prefix = message.Type switch
        {
            "join" => "[green]+[/]",
            "leave" => "[yellow]-[/]",
            "system" => "[grey]*[/]",
            _ => $"[{color}]{message.Username}[/]"
        };

        var roomIndicator = chat.CurrentRoom?.Name != message.Room
            ? $"[grey]#{message.Room}[/] "
            : "";

        var timestamp = message.Timestamp.ToLocalTime().ToString("HH:mm");

        AnsiConsole.MarkupLine($"\r[grey]{timestamp}[/] {roomIndicator}{prefix}: {Markup.Escape(message.Content)}");
        WritePrompt(chat);
    };

    // Show help
    ShowHelp();

    // Main input loop
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    while (!cts.Token.IsCancellationRequested)
    {
        WritePrompt(chat);

        string? input;
        try
        {
            input = Console.ReadLine();
        }
        catch (OperationCanceledException)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(input))
            continue;

        // Handle commands
        if (input.StartsWith('/'))
        {
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : null;

            switch (command)
            {
                case "/join" or "/j":
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        AnsiConsole.MarkupLine("[red]Usage: /join <room>[/]");
                    }
                    else
                    {
                        try
                        {
                            await chat.JoinRoomAsync(arg);
                            AnsiConsole.MarkupLine($"[green]Joined room #{arg}[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Failed to join: {ex.Message}[/]");
                        }
                    }
                    break;

                case "/leave" or "/l":
                    var roomToLeave = arg ?? chat.CurrentRoom?.Name;
                    if (string.IsNullOrWhiteSpace(roomToLeave))
                    {
                        AnsiConsole.MarkupLine("[red]Not in any room[/]");
                    }
                    else
                    {
                        await chat.LeaveRoomAsync(roomToLeave);
                        AnsiConsole.MarkupLine($"[yellow]Left room #{roomToLeave}[/]");
                    }
                    break;

                case "/switch" or "/s":
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        AnsiConsole.MarkupLine("[red]Usage: /switch <room>[/]");
                    }
                    else if (chat.SwitchRoom(arg))
                    {
                        AnsiConsole.MarkupLine($"[cyan]Switched to #{arg}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Not joined to room #{arg}. Use /join first.[/]");
                    }
                    break;

                case "/rooms" or "/r":
                    var rooms = chat.JoinedRooms;
                    if (rooms.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[grey]Not in any rooms. Use /join <room> to join one.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[cyan]Joined rooms:[/]");
                        foreach (var room in rooms)
                        {
                            var current = room == chat.CurrentRoom ? " [green](active)[/]" : "";
                            AnsiConsole.MarkupLine($"  [white]#{room.Name}[/]{current}");
                        }
                    }
                    break;

                case "/help" or "/h" or "/?":
                    ShowHelp();
                    break;

                case "/quit" or "/q" or "/exit":
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    cts.Cancel();
                    break;

                case "/clear" or "/c":
                    AnsiConsole.Clear();
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]Unknown command: {command}. Type /help for commands.[/]");
                    break;
            }
        }
        else
        {
            // Send message
            if (chat.CurrentRoom == null)
            {
                AnsiConsole.MarkupLine("[red]Not in any room. Use /join <room> first.[/]");
            }
            else
            {
                try
                {
                    await chat.SendMessageAsync(input);
                    // Show own message
                    var timestamp = DateTime.Now.ToString("HH:mm");
                    AnsiConsole.MarkupLine($"[grey]{timestamp}[/] [{GetUserColor(username)}]{username}[/]: {Markup.Escape(input)}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to send: {ex.Message}[/]");
                }
            }
        }
    }

    AnsiConsole.MarkupLine("\n[grey]Disconnecting...[/]");
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"\n[red]Error: {ex.Message}[/]");
    return 1;
}

return 0;

void ShowHelp()
{
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[cyan]Command[/]")
        .AddColumn("[grey]Description[/]");

    table.AddRow("/join <room>", "Join a chat room");
    table.AddRow("/leave [room]", "Leave current or specified room");
    table.AddRow("/switch <room>", "Switch to another joined room");
    table.AddRow("/rooms", "List joined rooms");
    table.AddRow("/clear", "Clear the screen");
    table.AddRow("/help", "Show this help");
    table.AddRow("/quit", "Exit the chat");

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

void WritePrompt(ChatClient chat)
{
    var room = chat.CurrentRoom?.Name ?? "no room";
    Console.Write($"[#{room}] {username}> ");
}

string GetUserColor(string name)
{
    // Generate consistent color for each user
    var hash = name.GetHashCode();
    var colors = new[] { "cyan", "magenta", "blue", "green", "yellow", "red" };
    return colors[Math.Abs(hash) % colors.Length];
}
