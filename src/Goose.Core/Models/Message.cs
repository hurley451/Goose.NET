namespace Goose.Core.Models;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public record Message
{
    /// <summary>
    /// The role of the message sender
    /// </summary>
    public required MessageRole Role { get; init; }
    
    /// <summary>
    /// The content of the message
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Tool calls requested in this message (if any)
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    
    /// <summary>
    /// ID of the tool call this message responds to (for tool result messages)
    /// </summary>
    public string? ToolCallId { get; init; }
}
