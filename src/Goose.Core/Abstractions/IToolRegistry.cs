using System.Reflection;
using Goose.Core.Models;
using Goose.Core.Services;

namespace Goose.Core.Abstractions;

/// <summary>
/// Registry for managing available tools
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Registers a new tool
    /// </summary>
    /// <param name="tool">The tool to register</param>
    void Register(ITool tool);

    /// <summary>
    /// Unregisters a tool by name
    /// </summary>
    /// <param name="toolName">The name of the tool to unregister</param>
    /// <returns>True if the tool was found and removed, false otherwise</returns>
    bool Unregister(string toolName);

    /// <summary>
    /// Checks if a tool is registered
    /// </summary>
    /// <param name="toolName">The name of the tool to check</param>
    /// <returns>True if the tool is registered, false otherwise</returns>
    bool IsToolRegistered(string toolName);

    /// <summary>
    /// Gets the count of registered tools
    /// </summary>
    /// <returns>The number of registered tools</returns>
    int GetToolCount();

    /// <summary>
    /// Gets a registered tool by name
    /// </summary>
    /// <param name="name">The name of the tool to get</param>
    /// <returns>The tool if found, otherwise null</returns>
    ITool? GetTool(string name);

    /// <summary>
    /// Gets all registered tools
    /// </summary>
    /// <returns>List of all available tools</returns>
    IReadOnlyList<ITool> GetAllTools();

    /// <summary>
    /// Attempts to get a tool by name
    /// </summary>
    /// <param name="name">The name of the tool to get</param>
    /// <param name="tool">When this method returns true, contains the tool; otherwise null</param>
    /// <returns>True if a tool with the specified name was found, otherwise false</returns>
    bool TryGetTool(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ITool? tool);

    /// <summary>
    /// Loads tools from assemblies in the specified directory
    /// </summary>
    /// <param name="directoryPath">The path to the directory containing assemblies</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tools successfully loaded</returns>
    Task<int> LoadToolsFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads tools from a specific assembly
    /// </summary>
    /// <param name="assembly">The assembly to load tools from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tools successfully loaded from the assembly</returns>
    Task<int> LoadToolsFromAssemblyAsync(Assembly assembly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tool metadata including documentation and parameters
    /// </summary>
    /// <param name="toolName">The tool name to get metadata for</param>
    /// <returns>Tool metadata if found, otherwise null</returns>
    ToolMetadata? GetToolMetadata(string toolName);

    /// <summary>
    /// Validates a tool configuration
    /// </summary>
    /// <param name="toolName">The tool name to validate</param>
    /// <returns>Validation result with success status and error messages if validation fails</returns>
    ValidationResult ValidateTool(string toolName);

    /// <summary>
    /// Validates tool parameters against the tool's schema
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="parameters">The parameters JSON string to validate</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateToolParametersAsync(
        string toolName,
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a plugin manifest for the tool registry
    /// </summary>
    /// <returns>Plugin manifest describing the current state of tools</returns>
    PluginManifest CreatePluginManifest();
}
