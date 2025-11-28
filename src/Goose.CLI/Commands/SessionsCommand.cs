using System.CommandLine;
using Goose.Core.Abstractions;
using Goose.Core.Models;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to list and manage conversation sessions
/// </summary>
public class SessionsCommand : BaseCommand
{
    private readonly ISessionManager _sessionManager;

    public SessionsCommand(ISessionManager sessionManager)
        : base("sessions", "List and manage conversation sessions")
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

        // Add subcommands
        AddCommand(CreateListCommand());
        AddCommand(CreateDeleteCommand());
        AddCommand(CreateArchiveCommand());
        AddCommand(CreateRestoreCommand());
        AddCommand(CreateExportCommand());
        AddCommand(CreateImportCommand());
    }

    private Command CreateListCommand()
    {
        var listCommand = new Command("list", "List all sessions");

        var archivedOption = new Option<bool>(
            aliases: new[] { "--archived", "-a" },
            description: "Include archived sessions");

        var providerOption = new Option<string?>(
            aliases: new[] { "--provider", "-p" },
            description: "Filter by provider");

        var limitOption = new Option<int?>(
            aliases: new[] { "--limit", "-n" },
            description: "Maximum number of sessions to show");

        listCommand.AddOption(archivedOption);
        listCommand.AddOption(providerOption);
        listCommand.AddOption(limitOption);

        listCommand.SetHandler(async (bool includeArchived, string? provider, int? limit) =>
        {
            await HandleAsync(async () =>
            {
                var options = new SessionQueryOptions
                {
                    IncludeArchived = includeArchived,
                    Provider = provider,
                    Limit = limit
                };

                var sessions = await _sessionManager.ListSessionsAsync(options);

                if (sessions.Count == 0)
                {
                    WriteInfo("No sessions found.");
                    return;
                }

                Console.WriteLine($"\nFound {sessions.Count} session(s):\n");
                Console.WriteLine($"{"ID",-25} {"Name",-20} {"Provider",-12} {"Messages",-10} {"Updated",-20} {"Status",-10}");
                Console.WriteLine(new string('-', 110));

                foreach (var session in sessions)
                {
                    var status = session.IsArchived ? "Archived" : "Active";
                    var statusColor = session.IsArchived ? ConsoleColor.DarkGray : ConsoleColor.Green;

                    Console.Write($"{session.SessionId,-25} ");
                    Console.Write($"{session.Name ?? "(unnamed)",-20} ");
                    Console.Write($"{session.Provider ?? "N/A",-12} ");
                    Console.Write($"{session.MessageCount,-10} ");
                    Console.Write($"{session.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm}     ");

                    Console.ForegroundColor = statusColor;
                    Console.Write($"{status,-10}");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                Console.WriteLine();
            });
        }, archivedOption, providerOption, limitOption);

        return listCommand;
    }

    private Command CreateDeleteCommand()
    {
        var deleteCommand = new Command("delete", "Delete a session");

        var sessionIdArg = new Argument<string>("session-id", "The session ID to delete");
        deleteCommand.AddArgument(sessionIdArg);

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Skip confirmation prompt");
        deleteCommand.AddOption(forceOption);

        deleteCommand.SetHandler(async (string sessionId, bool force) =>
        {
            await HandleAsync(async () =>
            {
                // Confirm deletion unless force flag is set
                if (!force)
                {
                    Console.Write($"Are you sure you want to delete session '{sessionId}'? (y/N): ");
                    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (response != "y" && response != "yes")
                    {
                        WriteInfo("Deletion cancelled.");
                        return;
                    }
                }

                var deleted = await _sessionManager.DeleteSessionAsync(sessionId);
                if (deleted)
                {
                    WriteSuccess($"Session '{sessionId}' deleted successfully.");
                }
                else
                {
                    WriteError($"Session '{sessionId}' not found.");
                }
            });
        }, sessionIdArg, forceOption);

        return deleteCommand;
    }

    private Command CreateArchiveCommand()
    {
        var archiveCommand = new Command("archive", "Archive a session");

        var sessionIdArg = new Argument<string>("session-id", "The session ID to archive");
        archiveCommand.AddArgument(sessionIdArg);

        archiveCommand.SetHandler(async (string sessionId) =>
        {
            await HandleAsync(async () =>
            {
                var archived = await _sessionManager.ArchiveSessionAsync(sessionId);
                if (archived)
                {
                    WriteSuccess($"Session '{sessionId}' archived successfully.");
                }
                else
                {
                    WriteError($"Session '{sessionId}' not found.");
                }
            });
        }, sessionIdArg);

        return archiveCommand;
    }

    private Command CreateRestoreCommand()
    {
        var restoreCommand = new Command("restore", "Restore an archived session");

        var sessionIdArg = new Argument<string>("session-id", "The session ID to restore");
        restoreCommand.AddArgument(sessionIdArg);

        restoreCommand.SetHandler(async (string sessionId) =>
        {
            await HandleAsync(async () =>
            {
                var restored = await _sessionManager.RestoreSessionAsync(sessionId);
                if (restored)
                {
                    WriteSuccess($"Session '{sessionId}' restored successfully.");
                }
                else
                {
                    WriteError($"Session '{sessionId}' not found.");
                }
            });
        }, sessionIdArg);

        return restoreCommand;
    }

    private Command CreateExportCommand()
    {
        var exportCommand = new Command("export", "Export a session to a JSON file");

        var sessionIdArg = new Argument<string>("session-id", "The session ID to export");
        var filePathArg = new Argument<string>("file-path", "The output file path");

        exportCommand.AddArgument(sessionIdArg);
        exportCommand.AddArgument(filePathArg);

        exportCommand.SetHandler(async (string sessionId, string filePath) =>
        {
            await HandleAsync(async () =>
            {
                await _sessionManager.ExportSessionAsync(sessionId, filePath);
                WriteSuccess($"Session '{sessionId}' exported to '{filePath}'.");
            });
        }, sessionIdArg, filePathArg);

        return exportCommand;
    }

    private Command CreateImportCommand()
    {
        var importCommand = new Command("import", "Import a session from a JSON file");

        var filePathArg = new Argument<string>("file-path", "The file path to import from");
        importCommand.AddArgument(filePathArg);

        importCommand.SetHandler(async (string filePath) =>
        {
            await HandleAsync(async () =>
            {
                var session = await _sessionManager.ImportSessionAsync(filePath);
                WriteSuccess($"Session '{session.SessionId}' imported successfully.");
                Console.WriteLine($"  Name: {session.Name ?? "(unnamed)"}");
                Console.WriteLine($"  Messages: {session.MessageCount}");
                Console.WriteLine($"  Created: {session.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
            });
        }, filePathArg);

        return importCommand;
    }
}
