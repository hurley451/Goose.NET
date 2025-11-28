namespace Goose.Core.Models;

/// <summary>
/// Describes the capabilities of an AI provider
/// </summary>
public record ProviderCapabilities
{
    /// <summary>
    /// Whether the provider supports streaming responses
    /// </summary>
    public bool SupportsStreaming { get; init; } = true;

    /// <summary>
    /// Whether the provider supports tool/function calling
    /// </summary>
    public bool SupportsTools { get; init; } = true;

    /// <summary>
    /// Whether the provider supports vision/image inputs
    /// </summary>
    public bool SupportsVision { get; init; } = false;

    /// <summary>
    /// Maximum tokens supported by the provider
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Maximum context window size
    /// </summary>
    public int? MaxContextTokens { get; init; }
}
