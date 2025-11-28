namespace Goose.Core.Models;

/// <summary>
/// Represents a chunk of streaming response data
/// </summary>
public record StreamChunk
{
    /// <summary>
    /// The content of this chunk
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Whether this is the final chunk
    /// </summary>
    public bool IsFinal { get; init; } = false;
    
    /// <summary>
    /// The tool call this chunk might be part of
    /// </summary>
    public ToolCall? ToolCall { get; init; }
}
