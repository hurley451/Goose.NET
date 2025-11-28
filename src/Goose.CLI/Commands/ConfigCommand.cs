using System.CommandLine;
using System.Text.Json;
using Goose.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to view and manage configuration
/// </summary>
public class ConfigCommand : BaseCommand
{
    private readonly IOptions<GooseOptions> _options;

    public ConfigCommand(IOptions<GooseOptions> options)
        : base("config", "View and manage configuration")
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Add subcommands
        AddCommand(CreateShowCommand());
        AddCommand(CreateListProvidersCommand());
    }

    private Command CreateShowCommand()
    {
        var showCommand = new Command("show", "Show current configuration");

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output as JSON");

        showCommand.AddOption(jsonOption);

        showCommand.SetHandler(async (bool asJson) =>
        {
            await HandleAsync(async () =>
            {
                var config = _options.Value;

                if (asJson)
                {
                    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    Console.WriteLine(json);
                }
                else
                {
                    Console.WriteLine("\nGoose.NET Configuration:");
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine();

                    Console.WriteLine($"Default Provider:    {config.DefaultProvider}");
                    Console.WriteLine($"Session Directory:   {config.SessionDirectory}");
                    Console.WriteLine($"Max Tokens:          {config.MaxTokens}");
                    Console.WriteLine($"Temperature:         {config.Temperature}");
                    Console.WriteLine();

                    Console.WriteLine("Configured Providers:");
                    Console.WriteLine(new string('-', 60));
                    foreach (var provider in config.Providers)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n{provider.Key}:");
                        Console.ResetColor();
                        Console.WriteLine($"  Model:        {provider.Value.Model ?? "(default)"}");
                        Console.WriteLine($"  Max Tokens:   {provider.Value.MaxTokens?.ToString() ?? "(default)"}");
                        Console.WriteLine($"  Temperature:  {provider.Value.Temperature?.ToString() ?? "(default)"}");
                        Console.WriteLine($"  Top P:        {provider.Value.TopP?.ToString() ?? "(default)"}");
                    }
                    Console.WriteLine();
                }
            });
        }, jsonOption);

        return showCommand;
    }

    private Command CreateListProvidersCommand()
    {
        var listCommand = new Command("providers", "List configured providers");

        listCommand.SetHandler(async () =>
        {
            await HandleAsync(async () =>
            {
                var config = _options.Value;

                Console.WriteLine("\nConfigured Providers:");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine();

                Console.WriteLine($"{"Provider",-15} {"Model",-30} {"Status",-15}");
                Console.WriteLine(new string('-', 60));

                foreach (var provider in config.Providers)
                {
                    var isDefault = provider.Key == config.DefaultProvider;
                    var hasModel = !string.IsNullOrEmpty(provider.Value.Model);
                    var status = hasModel ? "Configured" : "Not configured";
                    var statusColor = hasModel ? ConsoleColor.Green : ConsoleColor.Yellow;

                    if (isDefault)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{provider.Key + " (default)",-15} ");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write($"{provider.Key,-15} ");
                    }

                    Console.Write($"{provider.Value.Model ?? "(not set)",-30} ");

                    Console.ForegroundColor = statusColor;
                    Console.Write($"{status,-15}");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                Console.WriteLine();
                Console.WriteLine("Note: Provider-specific settings (API keys, base URLs) should be");
                Console.WriteLine("configured via environment variables or appsettings.json");
                Console.WriteLine();
            });
        });

        return listCommand;
    }
}
