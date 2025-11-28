namespace Goose.Core.Models;

/// <summary>
/// The role of a message in a conversation
/// </summary>
public enum MessageRole
{
    /// <summary>System message (instructions)</summary>
    System,
    
    /// <summary>User message (human input)</summary>
    User,
    
    /// <summary>Assistant message (AI response)</summary>
    Assistant,
    
    /// <summary>Tool result message</summary>
    Tool
}
