using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;

namespace Goose.CLI.Retro;

/// <summary>
/// Retro ASCII GUI interface using Spectre.Console
/// </summary>
public class RetroInterface
{
    private readonly IConversationAgent _conversationAgent;
    private readonly IToolRegistry _toolRegistry;
    private readonly IProvider _provider;
    private readonly ISessionManager _sessionManager;

    private ConversationContext? _context;
    private Session? _session;
    private List<Message> _messages = new();
    private string _currentInput = "";
    private int _scrollOffset = 0;
    private RetroView _currentView = RetroView.Chat;
    private bool _isRunning = true;
    private bool _isProcessing = false;
    private List<SessionSummary> _allSessions = new();

    private enum RetroView
    {
        Chat,
        Sessions,
        Tools,
        Settings
    }

    public RetroInterface(
        IConversationAgent conversationAgent,
        IToolRegistry toolRegistry,
        IProvider provider,
        ISessionManager sessionManager)
    {
        _conversationAgent = conversationAgent;
        _toolRegistry = toolRegistry;
        _provider = provider;
        _sessionManager = sessionManager;
    }

    public async Task RunAsync(string? sessionName)
    {
        // Initialize session
        await InitializeSessionAsync(sessionName);

        // Load all sessions for the sessions view
        _allSessions = (await _sessionManager.ListSessionsAsync()).ToList();

        // Set up console
        Console.CursorVisible = false;
        Console.Clear();

        try
        {
            while (_isRunning)
            {
                RenderInterface();
                await HandleInputAsync();
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.Clear();
            AnsiConsole.MarkupLine("[bold green]Thanks for using Goose.NET Retro![/]");
        }
    }

    private async Task InitializeSessionAsync(string? sessionName)
    {
        var sessionId = sessionName ?? $"retro-session-{DateTime.Now:yyyyMMdd-HHmmss}";

        _session = await _sessionManager.GetSessionAsync(sessionId);
        if (_session == null)
        {
            _session = new Session
            {
                SessionId = sessionId,
                Name = sessionId,
                WorkingDirectory = Environment.CurrentDirectory,
                CreatedAt = DateTime.UtcNow
            };
            await _sessionManager.CreateSessionAsync(_session);
        }

        _context = await _sessionManager.LoadContextAsync(sessionId);
        if (_context == null)
        {
            _context = new ConversationContext
            {
                SessionId = sessionId,
                WorkingDirectory = _session.WorkingDirectory ?? Environment.CurrentDirectory
            };
        }
        else
        {
            _messages = _context.Messages.ToList();
        }
    }

    private void RenderInterface()
    {
        try
        {
            Console.SetCursorPosition(0, 0);

            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(3),
                    new Layout("Main"),
                    new Layout("Footer").Size(3)
                );

            // Header
            layout["Header"].Update(CreateHeader());

            // Main area - split into content and sidebar
            layout["Main"].SplitColumns(
                new Layout("Content"),
                new Layout("Sidebar").Size(30)
            );

            layout["Main"]["Content"].Update(CreateMainContent());
            layout["Main"]["Sidebar"].Update(CreateSidebar());

            // Footer
            layout["Footer"].Update(CreateFooter());

            AnsiConsole.Write(layout);
        }
        catch (Exception ex)
        {
            Console.Clear();
            AnsiConsole.WriteException(ex);
            _isRunning = false;
        }
    }

    private Panel CreateHeader()
    {
        var title = new FigletText("GOOSE.NET")
            .Color(Color.Green);

        var subtitle = new Markup($"[dim green]Retro Terminal v1.0 - {_provider.Name ?? "Default Provider"}[/]");

        var grid = new Grid()
            .AddColumn()
            .AddRow(title)
            .AddRow(subtitle);

        return new Panel(grid)
            .Border(BoxBorder.Double)
            .BorderColor(Color.Green)
            .Padding(0, 0);
    }

    private Panel CreateMainContent()
    {
        var content = _currentView switch
        {
            RetroView.Chat => CreateChatView(),
            RetroView.Sessions => CreateSessionsView(),
            RetroView.Tools => CreateToolsView(),
            RetroView.Settings => CreateSettingsView(),
            _ => new Markup("[red]Unknown view[/]")
        };

        var header = $"[[ {_currentView.ToString().ToUpper()} ]]";
        return new Panel(content)
            .Header(header)
            .BorderColor(Color.Green)
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private IRenderable CreateChatView()
    {
        var chatContent = new StringBuilder();

        if (_messages.Count == 0)
        {
            chatContent.AppendLine("[dim green]No messages yet. Type your message below and press ENTER to send.[/]");
        }
        else
        {
            // Calculate visible messages based on scroll
            var visibleHeight = Console.WindowHeight - 12; // Account for header, footer, input
            var startIdx = Math.Max(0, _messages.Count - visibleHeight - _scrollOffset);
            var endIdx = Math.Min(_messages.Count, startIdx + visibleHeight);

            for (int i = startIdx; i < endIdx; i++)
            {
                var msg = _messages[i];
                var role = msg.Role switch
                {
                    MessageRole.User => "YOU",
                    MessageRole.Assistant => "GOOSE",
                    MessageRole.System => "SYSTEM",
                    _ => msg.Role.ToString().ToUpper()
                };
                var color = msg.Role switch
                {
                    MessageRole.User => "cyan",
                    MessageRole.Assistant => "green",
                    MessageRole.System => "red",
                    _ => "yellow"
                };
                var content = msg.Content ?? "";

                // Wrap long lines
                var lines = WrapText(content, Console.WindowWidth - 35);
                chatContent.AppendLine($"[bold {color}]{role}:[/]");
                foreach (var line in lines)
                {
                    chatContent.AppendLine($"[{color}]{line.EscapeMarkup()}[/]");
                }
                chatContent.AppendLine();
            }
        }

        if (_isProcessing)
        {
            chatContent.AppendLine("[yellow blink]▌ Processing...[/]");
        }

        return new Markup(chatContent.ToString());
    }

    private IRenderable CreateSessionsView()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn("[green]Session ID[/]")
            .AddColumn("[green]Name[/]")
            .AddColumn("[green]Created[/]");

        foreach (var session in _allSessions.Take(20))
        {
            var isActive = session.SessionId == _session?.SessionId ? "[bold yellow]*[/] " : "  ";
            var sessionId = string.IsNullOrWhiteSpace(session.SessionId) ? "unknown" : session.SessionId;
            var sessionName = string.IsNullOrWhiteSpace(session.Name) ? "-" : session.Name;

            table.AddRow(
                $"{isActive}[cyan]{sessionId.EscapeMarkup()}[/]",
                $"[white]{sessionName.EscapeMarkup()}[/]",
                $"[dim]{session.CreatedAt:MM/dd/yy HH:mm}[/]"
            );
        }

        return table;
    }

    private IRenderable CreateToolsView()
    {
        var tools = _toolRegistry.GetAllTools();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn("[green]Tool Name[/]")
            .AddColumn("[green]Description[/]")
            .AddColumn("[green]Risk[/]");

        foreach (var tool in tools)
        {
            var riskColor = tool.RiskLevel switch
            {
                ToolRiskLevel.ReadOnly => "green",
                ToolRiskLevel.ReadWrite => "yellow",
                ToolRiskLevel.Destructive => "red",
                ToolRiskLevel.Critical => "magenta",
                _ => "white"
            };

            table.AddRow(
                $"[cyan]{(tool.Name ?? "Unknown").EscapeMarkup()}[/]",
                $"[white]{(tool.Description ?? "No description").EscapeMarkup()}[/]",
                $"[{riskColor}]{tool.RiskLevel}[/]"
            );
        }

        return table;
    }

    private IRenderable CreateSettingsView()
    {
        var providerName = string.IsNullOrWhiteSpace(_provider.Name) ? "Default" : _provider.Name;
        var sessionId = _session?.SessionId ?? "N/A";
        var workingDir = _context?.WorkingDirectory ?? "N/A";

        var grid = new Grid()
            .AddColumn()
            .AddRow($"[green]Provider:[/] [white]{providerName.EscapeMarkup()}[/]")
            .AddRow($"[green]Session:[/] [white]{sessionId.EscapeMarkup()}[/]")
            .AddRow($"[green]Working Dir:[/] [white]{workingDir.EscapeMarkup()}[/]")
            .AddRow($"[green]Messages:[/] [white]{_messages.Count}[/]")
            .AddRow("")
            .AddRow("[dim green]Theme: Classic CRT Green[/]")
            .AddRow("[dim green]Display: 80x24 Compatible[/]");

        return grid;
    }

    private Panel CreateSidebar()
    {
        var tabs = new StringBuilder();
        var views = Enum.GetValues<RetroView>();

        foreach (var view in views)
        {
            var isActive = view == _currentView;
            var prefix = isActive ? "[black on green]" : "[green]";
            var suffix = isActive ? "[/]" : "[/]";
            tabs.AppendLine($"{prefix} {GetTabKey(view)} - {view,-10}{suffix}");
        }

        tabs.AppendLine();
        tabs.AppendLine("[dim green]────────────────────────[/]");
        tabs.AppendLine();
        tabs.AppendLine("[green]ESC[/]  [dim]- Exit[/]");
        tabs.AppendLine("[green]↑↓[/]   [dim]- Scroll[/]");
        tabs.AppendLine("[green]TAB[/]  [dim]- Next view[/]");

        return new Panel(new Markup(tabs.ToString()))
            .Header("[[ NAVIGATION ]]")
            .BorderColor(Color.Green)
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private Panel CreateFooter()
    {
        var prompt = _isProcessing
            ? "[yellow]Processing... Please wait[/]"
            : $"[green]>[/] [white]{_currentInput.EscapeMarkup()}[/][green]▌[/]";

        var footer = new Markup(prompt);

        return new Panel(footer)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green)
            .Expand();
    }

    private async Task HandleInputAsync()
    {
        if (!Console.KeyAvailable)
        {
            await Task.Delay(50);
            return;
        }

        var key = Console.ReadKey(true);

        // Global keys
        if (key.Key == ConsoleKey.Escape)
        {
            _isRunning = false;
            return;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            var values = Enum.GetValues<RetroView>();
            var currentIndex = Array.IndexOf(values, _currentView);
            _currentView = values[(currentIndex + 1) % values.Length];
            _scrollOffset = 0;
            return;
        }

        if (key.Key == ConsoleKey.F1) _currentView = RetroView.Chat;
        if (key.Key == ConsoleKey.F2) _currentView = RetroView.Sessions;
        if (key.Key == ConsoleKey.F3) _currentView = RetroView.Tools;
        if (key.Key == ConsoleKey.F4) _currentView = RetroView.Settings;

        // Scroll
        if (key.Key == ConsoleKey.UpArrow)
        {
            _scrollOffset = Math.Min(_scrollOffset + 1, _messages.Count);
            return;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            _scrollOffset = Math.Max(_scrollOffset - 1, 0);
            return;
        }

        // Chat input (only in chat view)
        if (_currentView == RetroView.Chat && !_isProcessing)
        {
            if (key.Key == ConsoleKey.Enter && !string.IsNullOrWhiteSpace(_currentInput))
            {
                await SendMessageAsync(_currentInput);
                _currentInput = "";
                _scrollOffset = 0;
            }
            else if (key.Key == ConsoleKey.Backspace && _currentInput.Length > 0)
            {
                _currentInput = _currentInput[..^1];
            }
            else if (!char.IsControl(key.KeyChar))
            {
                _currentInput += key.KeyChar;
            }
        }
    }

    private async Task SendMessageAsync(string message)
    {
        _isProcessing = true;

        try
        {
            // Add user message
            var userMessage = new Message
            {
                Role = MessageRole.User,
                Content = message,
                Timestamp = DateTime.UtcNow
            };
            _messages.Add(userMessage);
            _context!.Messages.Add(userMessage);

            RenderInterface();

            // Get response
            var response = await _conversationAgent.ProcessMessageAsync(message, _context);

            // Add assistant response
            var assistantMessage = new Message
            {
                Role = MessageRole.Assistant,
                Content = response.Content ?? "(no response)",
                Timestamp = DateTime.UtcNow
            };
            _messages.Add(assistantMessage);

            // Save context
            await _sessionManager.SaveContextAsync(_context.SessionId, _context);
        }
        catch (Exception ex)
        {
            // Add error message to chat
            var errorMessage = new Message
            {
                Role = MessageRole.System,
                Content = $"Error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
            _messages.Add(errorMessage);

            // Try to save context even after error
            try
            {
                await _sessionManager.SaveContextAsync(_context!.SessionId, _context!);
            }
            catch
            {
                // Ignore save errors
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private static string GetTabKey(RetroView view)
    {
        return view switch
        {
            RetroView.Chat => "F1",
            RetroView.Sessions => "F2",
            RetroView.Tools => "F3",
            RetroView.Settings => "F4",
            _ => "??"
        };
    }

    private static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            lines.Add("");
            return lines;
        }

        var words = text.Split(' ');
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxWidth)
            {
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                currentLine.Append(word);
            }
            else
            {
                if (currentLine.Length > 0)
                    currentLine.Append(' ');
                currentLine.Append(word);
            }
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        if (lines.Count == 0)
            lines.Add("");

        return lines;
    }
}
