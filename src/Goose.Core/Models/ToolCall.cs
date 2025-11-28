namespace Goose.Core.Models;

/// <summary>
/// Represents a tool call in a message
/// </summary>
public record ToolCall
{
    /// <summary>
    /// Unique identifier for this tool call
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Name of the tool to call
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Parameters for the tool call as JSON
    /// </summary>
    public required string Parameters { get; init; }
}
