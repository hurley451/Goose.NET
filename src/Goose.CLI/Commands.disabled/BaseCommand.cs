using System.CommandLine;
using System.CommandLine.Invocation;

namespace Goose.CLI.Commands;

public abstract class BaseCommand : Command
{
    protected BaseCommand(string name, string description) 
        : base(name, description)
    {
    }

    protected async Task<int> HandleAsync(InvocationContext context)
    {
        try
        {
            await ExecuteAsync(context);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    protected abstract Task ExecuteAsync(InvocationContext context);
}
