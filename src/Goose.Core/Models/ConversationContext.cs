namespace Goose.Core.Models;

/// <summary>
/// Context information for a conversation session
/// </summary>
public class ConversationContext
{
    /// <summary>
    /// The collection of messages in this conversation
    /// </summary>
    public List<Message> Messages { get; set; } = new();
    
    /// <summary>
    /// Provider-specific options for this conversation
    /// </summary>
    public ProviderOptions ProviderOptions { get; set; } = new();
    
    /// <summary>
    /// The ID of this conversation session
    /// </summary>
    public required string SessionId { get; init; }
    
    /// <summary>
    /// The working directory for file operations
    /// </summary>
    public required string WorkingDirectory { get; init; }
}
