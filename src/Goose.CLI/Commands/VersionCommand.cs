using System.CommandLine;
using System.Reflection;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to display version information
/// </summary>
public class VersionCommand : BaseCommand
{
    public VersionCommand()
        : base("version", "Display version information")
    {
        this.SetHandler(Execute);
    }

    private async Task Execute()
    {
        await HandleAsync(async () =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? version;

            Console.WriteLine($"Goose.NET v{informationalVersion}");
            Console.WriteLine("AI-powered developer assistant for .NET");
            Console.WriteLine();
            Console.WriteLine("Based on the original Goose project");
            Console.WriteLine("Built with .NET 8.0 and C# 12");
            Console.WriteLine();
            Console.WriteLine($"Runtime: {Environment.Version}");
            Console.WriteLine($"Platform: {Environment.OSVersion}");
        });
    }
}
