using System.CommandLine;
using Goose.CLI.Commands;
using Goose.Core.Abstractions;
using Goose.Core.Configuration;
using Goose.Core.Extensions;
using Goose.Providers.Extensions;
using Goose.Tools.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Goose.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Build host with DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register CLI-specific permission prompt before adding Goose Core
                services.AddSingleton<IPermissionPrompt, ConsolePermissionPrompt>();

                // Add Goose Core services
                services.AddGooseCore(configuration);

                // Add default provider (from configuration)
                services.AddDefaultProvider(configuration);

                // Add Goose Tools
                services.AddGooseTools(configuration);
            })
            .Build();

        // Create root command
        var rootCommand = new RootCommand("Goose.NET - AI-powered developer assistant")
        {
            CreateVersionCommand(),
            CreateStartCommand(host),
            CreateRetroCommand(host),
            CreateSessionsCommand(host),
            CreateToolsCommand(host),
            CreateConfigCommand(host)
        };

        // Add global options
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose logging");
        rootCommand.AddGlobalOption(verboseOption);

        // Parse and execute
        return await rootCommand.InvokeAsync(args);
    }

    private static VersionCommand CreateVersionCommand()
    {
        return new VersionCommand();
    }

    private static StartCommand CreateStartCommand(IHost host)
    {
        var conversationAgent = host.Services.GetRequiredService<IConversationAgent>();
        var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
        var provider = host.Services.GetRequiredService<IProvider>();
        var sessionManager = host.Services.GetRequiredService<ISessionManager>();

        return new StartCommand(conversationAgent, toolRegistry, provider, sessionManager);
    }

    private static SessionsCommand CreateSessionsCommand(IHost host)
    {
        var sessionManager = host.Services.GetRequiredService<ISessionManager>();
        return new SessionsCommand(sessionManager);
    }

    private static ToolsCommand CreateToolsCommand(IHost host)
    {
        var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
        return new ToolsCommand(toolRegistry);
    }

    private static ConfigCommand CreateConfigCommand(IHost host)
    {
        var options = host.Services.GetRequiredService<IOptions<GooseOptions>>();
        return new ConfigCommand(options);
    }

    private static RetroCommand CreateRetroCommand(IHost host)
    {
        var conversationAgent = host.Services.GetRequiredService<IConversationAgent>();
        var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
        var provider = host.Services.GetRequiredService<IProvider>();
        var sessionManager = host.Services.GetRequiredService<ISessionManager>();

        return new RetroCommand(conversationAgent, toolRegistry, provider, sessionManager);
    }
}
