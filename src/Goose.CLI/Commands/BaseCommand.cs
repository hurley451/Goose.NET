using System.CommandLine;

namespace Goose.CLI.Commands;

/// <summary>
/// Base class for CLI commands with common error handling
/// </summary>
public abstract class BaseCommand : Command
{
    protected BaseCommand(string name, string? description = null)
        : base(name, description)
    {
    }

    /// <summary>
    /// Handles execution with error handling
    /// </summary>
    protected async Task<int> HandleAsync(Func<Task> executeFunc)
    {
        try
        {
            await executeFunc();
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Writes a success message in green (if supported)
    /// </summary>
    protected void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Writes an error message in red (if supported)
    /// </summary>
    protected void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ {message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Writes an info message in cyan (if supported)
    /// </summary>
    protected void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"ℹ {message}");
        Console.ResetColor();
    }
}
