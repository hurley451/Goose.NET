using System.CommandLine;
using System.Text.Json;
using Goose.Core.Abstractions;

namespace Goose.CLI.Commands;

/// <summary>
/// Command to list and inspect available tools
/// </summary>
public class ToolsCommand : BaseCommand
{
    private readonly IToolRegistry _toolRegistry;

    public ToolsCommand(IToolRegistry toolRegistry)
        : base("tools", "List and inspect available tools")
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));

        // Add subcommands
        AddCommand(CreateListCommand());
        AddCommand(CreateInfoCommand());
        AddCommand(CreateValidateCommand());
    }

    private Command CreateListCommand()
    {
        var listCommand = new Command("list", "List all available tools");

        var detailedOption = new Option<bool>(
            aliases: new[] { "--detailed", "-d" },
            description: "Show detailed information");

        listCommand.AddOption(detailedOption);

        listCommand.SetHandler(async (bool detailed) =>
        {
            await HandleAsync(async () =>
            {
                var tools = _toolRegistry.GetAllTools();

                Console.WriteLine($"\nAvailable Tools ({tools.Count}):\n");

                if (detailed)
                {
                    foreach (var tool in tools)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"● {tool.Name}");
                        Console.ResetColor();

                        Console.WriteLine($"  Description: {tool.Description}");

                        var metadata = _toolRegistry.GetToolMetadata(tool.Name);
                        if (metadata != null && metadata.ParametersSchema.Any())
                        {
                            Console.WriteLine("  Parameters:");
                            foreach (var param in metadata.ParametersSchema)
                            {
                                Console.WriteLine($"    - {param.Key}: {param.Value}");
                            }
                        }

                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine($"{"Name",-20} {"Description",-60}");
                    Console.WriteLine(new string('-', 80));

                    foreach (var tool in tools)
                    {
                        Console.WriteLine($"{tool.Name,-20} {tool.Description,-60}");
                    }
                }

                Console.WriteLine($"\nTotal: {tools.Count} tool(s)");
                Console.WriteLine("\nUse 'tools info <tool-name>' for detailed information about a specific tool.");
            });
        }, detailedOption);

        return listCommand;
    }

    private Command CreateInfoCommand()
    {
        var infoCommand = new Command("info", "Show detailed information about a specific tool");

        var toolNameArg = new Argument<string>("tool-name", "The name of the tool");
        infoCommand.AddArgument(toolNameArg);

        infoCommand.SetHandler(async (string toolName) =>
        {
            await HandleAsync(async () =>
            {
                var tool = _toolRegistry.GetTool(toolName);
                if (tool == null)
                {
                    WriteError($"Tool '{toolName}' not found.");
                    Console.WriteLine("\nAvailable tools:");
                    var allTools = _toolRegistry.GetAllTools();
                    foreach (var t in allTools)
                    {
                        Console.WriteLine($"  - {t.Name}");
                    }
                    return;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Tool: {tool.Name}");
                Console.ResetColor();
                Console.WriteLine(new string('=', 60));
                Console.WriteLine();

                Console.WriteLine($"Description: {tool.Description}");
                Console.WriteLine();

                // Display parameter schema
                Console.WriteLine("Parameter Schema:");
                Console.WriteLine(new string('-', 60));
                try
                {
                    var jsonDoc = JsonDocument.Parse(tool.ParameterSchema);
                    var formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(formattedJson);
                }
                catch
                {
                    Console.WriteLine(tool.ParameterSchema);
                }
                Console.WriteLine();

                // Display metadata if available
                var metadata = _toolRegistry.GetToolMetadata(tool.Name);
                if (metadata != null)
                {
                    if (metadata.ParametersSchema.Any())
                    {
                        Console.WriteLine("Parameter Details:");
                        Console.WriteLine(new string('-', 60));
                        foreach (var param in metadata.ParametersSchema)
                        {
                            Console.WriteLine($"  {param.Key}:");
                            if (param.Value is JsonElement jsonElement)
                            {
                                var formatted = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
                                foreach (var line in formatted.Split('\n'))
                                {
                                    Console.WriteLine($"    {line}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"    {param.Value}");
                            }
                        }
                        Console.WriteLine();
                    }
                }

                // Display usage example
                Console.WriteLine("Usage Example:");
                Console.WriteLine(new string('-', 60));
                Console.WriteLine($"The '{tool.Name}' tool can be used by the AI assistant during conversations.");
                Console.WriteLine("You don't need to invoke it manually - just ask the assistant to perform");
                Console.WriteLine("tasks that require this tool.");
                Console.WriteLine();
            });
        }, toolNameArg);

        return infoCommand;
    }

    private Command CreateValidateCommand()
    {
        var validateCommand = new Command("validate", "Validate a tool's configuration");

        var toolNameArg = new Argument<string>("tool-name", "The name of the tool to validate");
        validateCommand.AddArgument(toolNameArg);

        validateCommand.SetHandler(async (string toolName) =>
        {
            await HandleAsync(async () =>
            {
                if (!_toolRegistry.IsToolRegistered(toolName))
                {
                    WriteError($"Tool '{toolName}' is not registered.");
                    return;
                }

                var validationResult = _toolRegistry.ValidateTool(toolName);

                Console.WriteLine();
                if (validationResult.IsValid)
                {
                    WriteSuccess($"Tool '{toolName}' validation passed!");
                }
                else
                {
                    WriteError($"Tool '{toolName}' validation failed!");
                    Console.WriteLine("\nErrors:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ {error}");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine();
            });
        }, toolNameArg);

        return validateCommand;
    }
}
