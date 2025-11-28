using System.CommandLine;
using System.CommandLine.Invocation;

namespace Goose.CLI.Commands;

public class HelpCommand : BaseCommand
{
    public HelpCommand() 
        : base("help", "Show help information")
    {
        this.Handler = CommandHandler.Create<InvocationContext>(HandleAsync);
    }

    protected override async Task ExecuteAsync(InvocationContext context)
    {
        Console.WriteLine("Goose.NET - AI Developer Assistant");
        Console.WriteLine("===================================");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  start     - Start an interactive conversation");
        Console.WriteLine("  help      - Show this help");
        Console.WriteLine("  version   - Show version information");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  goose start     - Start interactive session");
        Console.WriteLine("  goose --help    - Show help");
        Console.WriteLine();
        Console.WriteLine("Documentation: https://github.com/square/goose");
    }
}
