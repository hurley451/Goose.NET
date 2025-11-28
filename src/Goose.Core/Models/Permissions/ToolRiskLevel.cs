namespace Goose.Core.Models.Permissions;

/// <summary>
/// Defines the risk level associated with a tool operation
/// </summary>
public enum ToolRiskLevel
{
    /// <summary>
    /// Read-only operations that cannot modify state
    /// Examples: file reading, listing directories, viewing status
    /// </summary>
    ReadOnly = 0,

    /// <summary>
    /// Operations that modify state but are generally safe
    /// Examples: file writing, creating directories, sending emails
    /// </summary>
    ReadWrite = 1,

    /// <summary>
    /// Potentially destructive operations that could cause data loss
    /// Examples: file deletion, system modifications, running shell commands
    /// </summary>
    Destructive = 2,

    /// <summary>
    /// Extremely dangerous operations that could compromise the system
    /// Examples: privilege escalation, code execution, system shutdown
    /// </summary>
    Critical = 3
}
