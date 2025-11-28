using System.CommandLine;
using Goose.CLI.Helpers;
using Goose.Core.Abstractions;
using Goose.Core.Models;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to start an interactive conversation session
/// </summary>
public class StartCommand : BaseCommand
{
    private readonly IConversationAgent _conversationAgent;
    private readonly IToolRegistry _toolRegistry;
    private readonly IProvider _provider;
    private readonly ISessionManager _sessionManager;

    public StartCommand(
        IConversationAgent conversationAgent,
        IToolRegistry toolRegistry,
        IProvider provider,
        ISessionManager sessionManager)
        : base("start", "Start an interactive conversation session")
    {
        _conversationAgent = conversationAgent ?? throw new ArgumentNullException(nameof(conversationAgent));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

        // Add option for session name/ID
        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Session ID or name (optional, creates new if not exists)");

        var resumeOption = new Option<string?>(
            aliases: new[] { "--resume", "-r" },
            description: "Resume existing session by ID");

        AddOption(sessionOption);
        AddOption(resumeOption);

        this.SetHandler(Execute, sessionOption, resumeOption);
    }

    private async Task Execute(string? sessionName, string? resumeSessionId)
    {
        await HandleAsync(async () =>
        {
            WriteInfo("Starting Goose.NET interactive session...");
            Console.WriteLine();

            // Display provider information
            Console.WriteLine($"Provider: {_provider.Name}");
            Console.WriteLine();

            // Display available tools
            var tools = _toolRegistry.GetAllTools();
            Console.WriteLine($"Available Tools ({tools.Count}):");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  • {tool.Name}: {tool.Description}");
            }
            Console.WriteLine();

            // Determine session ID and load/create context
            ConversationContext? context = null;
            Session? session = null;
            string sessionId;

            if (!string.IsNullOrWhiteSpace(resumeSessionId))
            {
                // Resume existing session
                sessionId = resumeSessionId;
                session = await _sessionManager.GetSessionAsync(sessionId);
                if (session == null)
                {
                    WriteError($"Session '{sessionId}' not found.");
                    return;
                }

                context = await _sessionManager.LoadContextAsync(sessionId);
                if (context != null)
                {
                    WriteSuccess($"Resumed session '{session.Name ?? sessionId}' with {context.Messages.Count} messages.");
                }
                else
                {
                    WriteInfo($"Loaded session '{session.Name ?? sessionId}' (no previous context).");
                    context = new ConversationContext
                    {
                        SessionId = sessionId,
                        WorkingDirectory = session.WorkingDirectory ?? Environment.CurrentDirectory
                    };
                }
            }
            else
            {
                // Create new session
                sessionId = sessionName ?? $"session-{DateTime.Now:yyyyMMdd-HHmmss}";
                context = new ConversationContext
                {
                    SessionId = sessionId,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                // Create session metadata
                session = new Session
                {
                    SessionId = sessionId,
                    Name = sessionName,
                    Provider = _provider.Name,
                    WorkingDirectory = Environment.CurrentDirectory,
                    MessageCount = 0,
                    ToolCallCount = 0
                };

                await _sessionManager.CreateSessionAsync(session);
                WriteSuccess("New session created!");
            }

            Console.WriteLine($"Session ID: {sessionId}");
            Console.WriteLine($"Working Directory: {context.WorkingDirectory}");
            Console.WriteLine();
            Console.WriteLine("Type your message (or 'exit' to quit):");
            Console.WriteLine("Use ↑/↓ arrow keys to navigate history");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            // Configure ReadLine with history
            ReadLine.HistoryEnabled = true;
            ReadLine.AutoCompletionHandler = null; // Could add autocomplete later

            // Load history for this session if it exists
            var historyFile = Path.Combine(Path.GetTempPath(), $".goose-history-{sessionId}.txt");
            if (File.Exists(historyFile))
            {
                try
                {
                    var historyLines = await File.ReadAllLinesAsync(historyFile);
                    foreach (var line in historyLines)
                    {
                        ReadLine.AddHistory(line);
                    }
                }
                catch
                {
                    // Ignore history load failures
                }
            }

            // Interactive loop
            while (true)
            {
                string? input;
                try
                {
                    input = ReadLine.Read("You: ");
                }
                catch
                {
                    // Fallback to regular Console.ReadLine if ReadLine fails
                    Console.Write("You: ");
                    input = Console.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    WriteInfo("Ending session...");
                    break;
                }

                try
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Assistant: ");
                    Console.ResetColor();

                    var toolExecutionCount = 0;
                    AgentResponse? finalResponse = null;

                    // Process the message with streaming
                    await foreach (var item in _conversationAgent.ProcessMessageStreamAsync(input, context))
                    {
                        if (item is StreamChunk chunk)
                        {
                            // Display content chunks as they arrive with markdown rendering
                            if (MarkdownRenderer.SupportsAnsi)
                            {
                                MarkdownRenderer.RenderChunk(chunk.Content);
                            }
                            else
                            {
                                // Fallback to plain text for terminals without ANSI support
                                Console.Write(chunk.Content);
                            }
                        }
                        else if (item is ToolResult toolResult)
                        {
                            // Display tool execution indicator
                            toolExecutionCount++;
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"[Executing tool: {toolResult.ToolCallId}]");
                            Console.ResetColor();
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Assistant: ");
                            Console.ResetColor();
                        }
                        else if (item is AgentResponse agentResponse)
                        {
                            // Store final response
                            finalResponse = agentResponse;
                        }
                    }

                    Console.WriteLine();

                    // Display tool execution summary
                    if (toolExecutionCount > 0)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"[{toolExecutionCount} tool(s) executed]");
                        Console.ResetColor();
                    }

                    Console.WriteLine();

                    // Auto-save session context after each message
                    try
                    {
                        await _sessionManager.SaveContextAsync(sessionId, context);
                    }
                    catch
                    {
                        // Silently fail auto-save - will be saved at end
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"Failed to process message: {ex.Message}");
                    Console.WriteLine();
                }
            }

            // Save final session state
            await _sessionManager.SaveContextAsync(sessionId, context);

            // Save command history
            try
            {
                var history = ReadLine.GetHistory();
                await File.WriteAllLinesAsync(historyFile, history);
            }
            catch
            {
                // Ignore history save failures
            }
            WriteSuccess($"Session ended. Total messages: {context.Messages.Count}");
        });
    }
}
