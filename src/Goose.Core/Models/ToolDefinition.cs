namespace Goose.Core.Models;

/// <summary>
/// Defines a tool that can be used by the AI provider
/// </summary>
public record ToolDefinition
{
    /// <summary>
    /// Unique identifier for the tool
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// JSON schema defining the tool's parameters
    /// </summary>
    public required string ParametersSchema { get; init; }
}
