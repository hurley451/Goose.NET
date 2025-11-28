namespace Goose.Core.Models.Permissions;

/// <summary>
/// Represents a decision about whether to allow a tool execution
/// </summary>
public enum PermissionDecision
{
    /// <summary>
    /// Allow the tool to execute
    /// </summary>
    Allow,

    /// <summary>
    /// Deny the tool execution
    /// </summary>
    Deny,

    /// <summary>
    /// Ask the user for permission
    /// </summary>
    Ask
}
