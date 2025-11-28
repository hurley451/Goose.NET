namespace Goose.Core.Models;

/// <summary>
/// Represents a response from an AI provider
/// </summary>
public record ProviderResponse
{
    /// <summary>
    /// The content of the response
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// The model that generated the response
    /// </summary>
    public required string Model { get; init; }
    
    /// <summary>
    /// Usage statistics for the request
    /// </summary>
    public ProviderUsage? Usage { get; init; }
    
    /// <summary>
    /// Tool calls that were requested in the response
    /// </summary>
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    
    /// <summary>
    /// Reason why the completion ended
    /// </summary>
    public string? StopReason { get; init; }
}
