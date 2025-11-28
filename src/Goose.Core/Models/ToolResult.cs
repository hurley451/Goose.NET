namespace Goose.Core.Models;

/// <summary>
/// Represents the result of executing a tool
/// </summary>
public record ToolResult
{
    /// <summary>
    /// The ID of the tool call this result corresponds to
    /// </summary>
    public required string ToolCallId { get; init; }
    
    /// <summary>
    /// Whether the tool execution was successful
    /// </summary>
    public required bool Success { get; init; }
    
    /// <summary>
    /// Output from the tool if successful
    /// </summary>
    public string? Output { get; init; }
    
    /// <summary>
    /// Error message if the tool failed
    /// </summary>
    public string? Error { get; init; }
    
    /// <summary>
    /// How long the tool took to execute
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Additional metadata about the execution
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
