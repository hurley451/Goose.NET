using Goose.Core.Models;

namespace Goose.Core.Abstractions;

/// <summary>
/// Manages conversation sessions including persistence and retrieval
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Creates a new session
    /// </summary>
    /// <param name="session">The session to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session with any generated values</returns>
    Task<Session> CreateSessionAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by ID
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The session if found, null otherwise</returns>
    Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing session
    /// </summary>
    /// <param name="session">The session with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated session</returns>
    Task<Session> UpdateSessionAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session
    /// </summary>
    /// <param name="sessionId">The session ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all sessions matching the query options
    /// </summary>
    /// <param name="options">Query options for filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of session summaries</returns>
    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a session (soft delete)
    /// </summary>
    /// <param name="sessionId">The session ID to archive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if archived, false if not found</returns>
    Task<bool> ArchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an archived session
    /// </summary>
    /// <param name="sessionId">The session ID to restore</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if restored, false if not found</returns>
    Task<bool> RestoreSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a session to JSON
    /// </summary>
    /// <param name="sessionId">The session ID to export</param>
    /// <param name="filePath">The file path to export to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportSessionAsync(string sessionId, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a session from JSON
    /// </summary>
    /// <param name="filePath">The file path to import from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The imported session</returns>
    Task<Session> ImportSessionAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session exists
    /// </summary>
    /// <param name="sessionId">The session ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the session exists, false otherwise</returns>
    Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of sessions
    /// </summary>
    /// <param name="includeArchived">Whether to include archived sessions in the count</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of sessions</returns>
    Task<int> GetSessionCountAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the conversation context for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="context">The conversation context to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveContextAsync(string sessionId, ConversationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the conversation context for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The conversation context if found, null otherwise</returns>
    Task<ConversationContext?> LoadContextAsync(string sessionId, CancellationToken cancellationToken = default);
}
