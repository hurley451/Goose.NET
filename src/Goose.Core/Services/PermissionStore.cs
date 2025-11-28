using Goose.Core.Abstractions;
using Goose.Core.Models.Permissions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Goose.Core.Services;

/// <summary>
/// In-memory implementation of permission storage
/// </summary>
public class PermissionStore : IPermissionStore
{
    private readonly ILogger<PermissionStore> _logger;

    // Session ID -> Tool Name -> Permission Decision
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PermissionDecision>> _permissions = new();

    /// <summary>
    /// Creates a new permission store instance
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public PermissionStore(ILogger<PermissionStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Saves a permission decision for future use
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="decision">The permission decision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task SavePermissionAsync(
        string sessionId,
        string toolName,
        PermissionDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

        var sessionPermissions = _permissions.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, PermissionDecision>());
        sessionPermissions[toolName] = decision;

        _logger.LogDebug(
            "Saved permission for tool '{ToolName}' in session '{SessionId}': {Decision}",
            toolName,
            sessionId,
            decision);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a previously saved permission decision
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The permission decision if found, null otherwise</returns>
    public Task<PermissionDecision?> GetPermissionAsync(
        string sessionId,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

        if (_permissions.TryGetValue(sessionId, out var sessionPermissions) &&
            sessionPermissions.TryGetValue(toolName, out var decision))
        {
            _logger.LogDebug(
                "Found saved permission for tool '{ToolName}' in session '{SessionId}': {Decision}",
                toolName,
                sessionId,
                decision);

            return Task.FromResult<PermissionDecision?>(decision);
        }

        _logger.LogDebug(
            "No saved permission found for tool '{ToolName}' in session '{SessionId}'",
            toolName,
            sessionId);

        return Task.FromResult<PermissionDecision?>(null);
    }

    /// <summary>
    /// Gets all saved permissions for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of tool names to permission decisions</returns>
    public Task<IDictionary<string, PermissionDecision>> GetAllPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (_permissions.TryGetValue(sessionId, out var sessionPermissions))
        {
            _logger.LogDebug(
                "Retrieved {Count} permissions for session '{SessionId}'",
                sessionPermissions.Count,
                sessionId);

            return Task.FromResult<IDictionary<string, PermissionDecision>>(
                new Dictionary<string, PermissionDecision>(sessionPermissions));
        }

        _logger.LogDebug("No permissions found for session '{SessionId}'", sessionId);
        return Task.FromResult<IDictionary<string, PermissionDecision>>(
            new Dictionary<string, PermissionDecision>());
    }

    /// <summary>
    /// Clears all saved permissions for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task ClearPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (_permissions.TryRemove(sessionId, out var sessionPermissions))
        {
            _logger.LogInformation(
                "Cleared {Count} permissions for session '{SessionId}'",
                sessionPermissions.Count,
                sessionId);
        }
        else
        {
            _logger.LogDebug("No permissions to clear for session '{SessionId}'", sessionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Revokes a specific permission for a tool
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task RevokePermissionAsync(
        string sessionId,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(toolName));

        if (_permissions.TryGetValue(sessionId, out var sessionPermissions) &&
            sessionPermissions.TryRemove(toolName, out var removedDecision))
        {
            _logger.LogInformation(
                "Revoked permission for tool '{ToolName}' in session '{SessionId}' (was: {Decision})",
                toolName,
                sessionId,
                removedDecision);
        }
        else
        {
            _logger.LogDebug(
                "No permission to revoke for tool '{ToolName}' in session '{SessionId}'",
                toolName,
                sessionId);
        }

        return Task.CompletedTask;
    }
}
