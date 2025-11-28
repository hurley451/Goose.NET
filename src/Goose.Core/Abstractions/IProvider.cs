using Goose.Core.Models;

namespace Goose.Core.Abstractions;

/// <summary>
/// Defines the contract for AI model providers (Anthropic, OpenAI, etc.)
/// </summary>
public interface IProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "anthropic", "openai")
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Generates a completion from the given messages
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The provider's response</returns>
    Task<ProviderResponse> GenerateAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Streams a completion with incremental updates
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the tool definitions supported by this provider
    /// </summary>
    IReadOnlyList<ToolDefinition> GetToolDefinitions();
}
