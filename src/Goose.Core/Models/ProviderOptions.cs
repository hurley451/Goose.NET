namespace Goose.Core.Models;

/// <summary>
/// Options for configuring a provider
/// </summary>
public record ProviderOptions
{
    /// <summary>
    /// The model to use for completions
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Top-p value for response generation
    /// </summary>
    public double? TopP { get; init; }

    /// <summary>
    /// Stop sequences to use
    /// </summary>
    public string? StopSequence { get; init; }

    /// <summary>
    /// Tools available to the model for function calling
    /// </summary>
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
}
