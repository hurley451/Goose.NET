using Goose.Core.Models.Permissions;

namespace Goose.Core.Configuration;

/// <summary>
/// Configuration options for the permission system
/// </summary>
public class PermissionOptions
{
    /// <summary>
    /// The permission mode to use
    /// </summary>
    public PermissionMode Mode { get; set; } = PermissionMode.SmartApprove;

    /// <summary>
    /// Whether to auto-approve ReadWrite operations when in SmartApprove mode
    /// </summary>
    public bool AutoApproveReadWrite { get; set; } = false;

    /// <summary>
    /// Whether to remember permission decisions for future requests
    /// </summary>
    public bool RememberDecisions { get; set; } = true;

    /// <summary>
    /// Maximum number of remembered permissions per session
    /// </summary>
    public int MaxRememberedPermissions { get; set; } = 100;
}
