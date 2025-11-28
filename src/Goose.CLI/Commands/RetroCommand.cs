using System.CommandLine;
using Goose.CLI.Retro;
using Goose.Core.Abstractions;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to start retro ASCII GUI interface
/// </summary>
public class RetroCommand : BaseCommand
{
    private readonly IConversationAgent _conversationAgent;
    private readonly IToolRegistry _toolRegistry;
    private readonly IProvider _provider;
    private readonly ISessionManager _sessionManager;

    public RetroCommand(
        IConversationAgent conversationAgent,
        IToolRegistry toolRegistry,
        IProvider provider,
        ISessionManager sessionManager)
        : base("retro", "Start retro ASCII GUI interface")
    {
        _conversationAgent = conversationAgent ?? throw new ArgumentNullException(nameof(conversationAgent));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));

        // Add option for session name/ID
        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Session ID or name (optional, creates new if not exists)");

        AddOption(sessionOption);

        this.SetHandler(Execute, sessionOption);
    }

    private async Task Execute(string? sessionName)
    {
        await HandleAsync(async () =>
        {
            var retroInterface = new RetroInterface(
                _conversationAgent,
                _toolRegistry,
                _provider,
                _sessionManager);

            await retroInterface.RunAsync(sessionName);
        });
    }
}
