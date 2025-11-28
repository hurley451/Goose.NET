using System.CommandLine;
using System.CommandLine.Invocation;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Goose.CLI.Commands;

public class StartCommand : BaseCommand
{
    public StartCommand() 
        : base("start", "Start an interactive conversation session")
    {
        this.Handler = CommandHandler.Create<InvocationContext>(HandleAsync);
    }

    protected override async Task ExecuteAsync(InvocationContext context)
    {
        var host = context.GetService<IHost>();
        
        // Demonstrate conversation capabilities
        Console.WriteLine("Starting Goose.NET interactive session...");
        
        var conversationAgent = host.Services.GetRequiredService<IConversationAgent>();
        Console.WriteLine("Conversation agent ready for interaction");
        
        // Show some system information
        var toolRegistry = host.Services.GetRequiredService<IToolRegistry>();
        
        Console.WriteLine("Available tools:");
        var tools = toolRegistry.GetAllTools();
        foreach (var tool in tools)
        {
            Console.WriteLine($"  - {tool.Name}: {tool.Description}");
        }
        
        Console.WriteLine("\nGoose.NET session started successfully!");
        Console.WriteLine("Type 'exit' to end the session");
    }
}
