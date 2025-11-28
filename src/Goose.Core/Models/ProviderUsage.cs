namespace Goose.Core.Models;

/// <summary>
/// Usage statistics for a provider request
/// </summary>
public record ProviderUsage
{
    /// <summary>
    /// Number of input tokens used
    /// </summary>
    public int InputTokens { get; init; }
    
    /// <summary>
    /// Number of output tokens used
    /// </summary>
    public int OutputTokens { get; init; }
    
    /// <summary>
    /// Total tokens used (input + output)
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
}
