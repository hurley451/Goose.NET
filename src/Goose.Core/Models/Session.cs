namespace Goose.Core.Models;

/// <summary>
/// Represents a conversation session with its metadata and messages
/// </summary>
public record Session
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Display name for the session
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Session description or notes
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the session was last updated
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Provider used for this session
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Working directory for this session
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Conversation context
    /// </summary>
    public ConversationContext? Context { get; init; }

    /// <summary>
    /// Session metadata (custom key-value pairs)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Tags for categorizing sessions
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Total number of messages in the session
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Total number of tool calls in the session
    /// </summary>
    public int ToolCallCount { get; init; }

    /// <summary>
    /// Whether the session is archived
    /// </summary>
    public bool IsArchived { get; init; }
}

/// <summary>
/// Lightweight session summary for listing
/// </summary>
public record SessionSummary
{
    public required string SessionId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? Provider { get; init; }
    public int MessageCount { get; init; }
    public int ToolCallCount { get; init; }
    public List<string>? Tags { get; init; }
    public bool IsArchived { get; init; }
}

/// <summary>
/// Options for querying sessions
/// </summary>
public record SessionQueryOptions
{
    /// <summary>
    /// Include archived sessions
    /// </summary>
    public bool IncludeArchived { get; init; }

    /// <summary>
    /// Filter by provider
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Filter by tags (any match)
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Filter by date range (created after)
    /// </summary>
    public DateTime? CreatedAfter { get; init; }

    /// <summary>
    /// Filter by date range (created before)
    /// </summary>
    public DateTime? CreatedBefore { get; init; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Search term for name/description
    /// </summary>
    public string? SearchTerm { get; init; }
}
