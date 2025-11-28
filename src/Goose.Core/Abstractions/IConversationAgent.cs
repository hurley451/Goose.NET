using Goose.Core.Models;

namespace Goose.Core.Abstractions;

/// <summary>
/// Interface for the main conversation agent that orchestrates interactions
/// </summary>
public interface IConversationAgent
{
    /// <summary>
    /// Processes a user message and generates an AI response
    /// </summary>
    /// <param name="message">The user's input message</param>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The agent's response including any tool calls</returns>
    Task<AgentResponse> ProcessMessageAsync(
        string message,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a user message with streaming response
    /// </summary>
    /// <param name="message">The user's input message</param>
    /// <param name="context">The conversation context</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Async stream of response chunks and final agent response</returns>
    IAsyncEnumerable<object> ProcessMessageStreamAsync(
        string message,
        ConversationContext context,
        CancellationToken cancellationToken = default);
}
