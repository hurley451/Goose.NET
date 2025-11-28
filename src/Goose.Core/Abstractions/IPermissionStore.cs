using Goose.Core.Models.Permissions;

namespace Goose.Core.Abstractions;

/// <summary>
/// Stores and retrieves permission decisions
/// </summary>
public interface IPermissionStore
{
    /// <summary>
    /// Saves a permission decision for future use
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="decision">The permission decision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SavePermissionAsync(
        string sessionId,
        string toolName,
        PermissionDecision decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a previously saved permission decision
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The permission decision if found, null otherwise</returns>
    Task<PermissionDecision?> GetPermissionAsync(
        string sessionId,
        string toolName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved permissions for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of tool names to permission decisions</returns>
    Task<IDictionary<string, PermissionDecision>> GetAllPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all saved permissions for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a specific permission for a tool
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RevokePermissionAsync(
        string sessionId,
        string toolName,
        CancellationToken cancellationToken = default);
}
