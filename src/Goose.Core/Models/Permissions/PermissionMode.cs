namespace Goose.Core.Models.Permissions;

/// <summary>
/// Defines how permission requests should be handled
/// </summary>
public enum PermissionMode
{
    /// <summary>
    /// Automatically approve all tool executions (dangerous!)
    /// </summary>
    Auto,

    /// <summary>
    /// Ask user for approval on every tool execution
    /// </summary>
    Ask,

    /// <summary>
    /// Auto-approve read-only tools, ask for everything else
    /// </summary>
    SmartApprove,

    /// <summary>
    /// Deny all tool executions
    /// </summary>
    Deny
}
