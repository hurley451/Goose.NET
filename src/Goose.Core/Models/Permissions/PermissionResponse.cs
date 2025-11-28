namespace Goose.Core.Models.Permissions;

/// <summary>
/// Represents a user's response to a permission request
/// </summary>
public record PermissionResponse
{
    /// <summary>
    /// The decision made
    /// </summary>
    public required PermissionDecision Decision { get; init; }

    /// <summary>
    /// Whether to remember this decision for future requests
    /// </summary>
    public bool RememberDecision { get; init; }

    /// <summary>
    /// User's reason or comment (optional)
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the response was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates an "Allow" response
    /// </summary>
    public static PermissionResponse Allow(bool remember = false) => new()
    {
        Decision = PermissionDecision.Allow,
        RememberDecision = remember
    };

    /// <summary>
    /// Creates a "Deny" response
    /// </summary>
    public static PermissionResponse Deny(bool remember = false, string? reason = null) => new()
    {
        Decision = PermissionDecision.Deny,
        RememberDecision = remember,
        Reason = reason
    };
}
