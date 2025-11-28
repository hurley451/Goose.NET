namespace Goose.Core.Models;

/// <summary>
/// Represents a response from the conversation agent
/// </summary>
public record AgentResponse
{
    /// <summary>
    /// The content of the AI's response
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Results from any tool executions
    /// </summary>
    public IReadOnlyList<ToolResult>? ToolResults { get; init; }
}
