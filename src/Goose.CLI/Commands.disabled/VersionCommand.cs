using System.CommandLine;
using System.CommandLine.Invocation;

namespace Goose.CLI.Commands;

public class VersionCommand : BaseCommand
{
    public VersionCommand() 
        : base("version", "Show version information")
    {
        this.Handler = CommandHandler.Create<InvocationContext>(HandleAsync);
    }

    protected override async Task ExecuteAsync(InvocationContext context)
    {
        Console.WriteLine("Goose.NET v1.0 - AI Developer Assistant");
        Console.WriteLine("Based on the original Goose project (Python)");
        Console.WriteLine("Ported to .NET 8.0 with C#");
    }
}
