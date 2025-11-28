using System.Text.Json;
using System.Text.Json.Serialization;
using Goose.Core.Abstractions;
using Goose.Core.Configuration;
using Goose.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goose.Core.Services;

/// <summary>
/// File system-based session manager for persisting conversation sessions
/// </summary>
public class FileSystemSessionManager : ISessionManager
{
    private readonly ILogger<FileSystemSessionManager> _logger;
    private readonly string _sessionsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public FileSystemSessionManager(
        ILogger<FileSystemSessionManager> logger,
        IOptions<GooseOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        // Get sessions directory from configuration or use default
        var sessionDir = options.Value.SessionDirectory ?? "~/.goose/sessions";
        _sessionsDirectory = ExpandPath(sessionDir);

        // Ensure directory exists
        Directory.CreateDirectory(_sessionsDirectory);

        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        _logger.LogInformation("Session manager initialized with directory: {Directory}", _sessionsDirectory);
    }

    public async Task<Session> CreateSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        // Ensure session has an ID
        if (string.IsNullOrWhiteSpace(session.SessionId))
        {
            throw new ArgumentException("Session must have an ID", nameof(session));
        }

        // Check if session already exists
        if (await SessionExistsAsync(session.SessionId, cancellationToken))
        {
            throw new InvalidOperationException($"Session '{session.SessionId}' already exists");
        }

        // Create session with timestamps
        var newSession = session with
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveSessionAsync(newSession, cancellationToken);

        _logger.LogInformation("Created session: {SessionId}", newSession.SessionId);
        return newSession;
    }

    public async Task<Session?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var sessionPath = GetSessionPath(sessionId);
        if (!File.Exists(sessionPath))
        {
            _logger.LogDebug("Session not found: {SessionId}", sessionId);
            return null;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(sessionPath, cancellationToken);
            var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);

            _logger.LogDebug("Retrieved session: {SessionId}", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session: {SessionId}", sessionId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Session> UpdateSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!await SessionExistsAsync(session.SessionId, cancellationToken))
        {
            throw new InvalidOperationException($"Session '{session.SessionId}' does not exist");
        }

        // Update timestamp
        var updatedSession = session with
        {
            UpdatedAt = DateTime.UtcNow
        };

        await SaveSessionAsync(updatedSession, cancellationToken);

        _logger.LogInformation("Updated session: {SessionId}", updatedSession.SessionId);
        return updatedSession;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var sessionPath = GetSessionPath(sessionId);
        if (!File.Exists(sessionPath))
        {
            return false;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            File.Delete(sessionPath);

            // Also delete context file if it exists
            var contextPath = GetContextPath(sessionId);
            if (File.Exists(contextPath))
            {
                File.Delete(contextPath);
            }

            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sessions = new List<SessionSummary>();
        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.session.json");

        foreach (var file in sessionFiles)
        {
            try
            {
                var session = await GetSessionAsync(Path.GetFileNameWithoutExtension(file.Replace(".session", "")), cancellationToken);
                if (session == null)
                    continue;

                // Apply filters - skip archived sessions unless explicitly requested
                bool includeArchived = options?.IncludeArchived ?? false;
                if (!includeArchived && session.IsArchived)
                    continue;

                if (options?.Provider != null && session.Provider != options.Provider)
                    continue;

                if (options?.Tags?.Any() == true && (session.Tags == null || !session.Tags.Any(t => options.Tags.Contains(t))))
                    continue;

                if (options?.CreatedAfter != null && session.CreatedAt < options.CreatedAfter)
                    continue;

                if (options?.CreatedBefore != null && session.CreatedAt > options.CreatedBefore)
                    continue;

                if (!string.IsNullOrWhiteSpace(options?.SearchTerm))
                {
                    var searchTerm = options.SearchTerm.ToLowerInvariant();
                    if (!(session.Name?.ToLowerInvariant().Contains(searchTerm) ?? false) &&
                        !(session.Description?.ToLowerInvariant().Contains(searchTerm) ?? false))
                        continue;
                }

                sessions.Add(new SessionSummary
                {
                    SessionId = session.SessionId,
                    Name = session.Name,
                    Description = session.Description,
                    CreatedAt = session.CreatedAt,
                    UpdatedAt = session.UpdatedAt,
                    Provider = session.Provider,
                    MessageCount = session.MessageCount,
                    ToolCallCount = session.ToolCallCount,
                    Tags = session.Tags,
                    IsArchived = session.IsArchived
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session from file: {File}", file);
            }
        }

        // Sort by updated date (most recent first)
        sessions = sessions.OrderByDescending(s => s.UpdatedAt).ToList();

        // Apply limit
        if (options?.Limit != null)
        {
            sessions = sessions.Take(options.Limit.Value).ToList();
        }

        return sessions;
    }

    public async Task<bool> ArchiveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
            return false;

        var archivedSession = session with { IsArchived = true, UpdatedAt = DateTime.UtcNow };
        await SaveSessionAsync(archivedSession, cancellationToken);

        _logger.LogInformation("Archived session: {SessionId}", sessionId);
        return true;
    }

    public async Task<bool> RestoreSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
            return false;

        var restoredSession = session with { IsArchived = false, UpdatedAt = DateTime.UtcNow };
        await SaveSessionAsync(restoredSession, cancellationToken);

        _logger.LogInformation("Restored session: {SessionId}", sessionId);
        return true;
    }

    public async Task ExportSessionAsync(string sessionId, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found");
        }

        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Exported session {SessionId} to {FilePath}", sessionId, filePath);
    }

    public async Task<Session> ImportSessionAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var session = JsonSerializer.Deserialize<Session>(json, _jsonOptions);

        if (session == null)
        {
            throw new InvalidOperationException($"Failed to deserialize session from {filePath}");
        }

        // Check if session with same ID already exists
        if (await SessionExistsAsync(session.SessionId, cancellationToken))
        {
            throw new InvalidOperationException($"Session '{session.SessionId}' already exists. Delete it first or modify the import.");
        }

        await SaveSessionAsync(session, cancellationToken);

        _logger.LogInformation("Imported session {SessionId} from {FilePath}", session.SessionId, filePath);
        return session;
    }

    public async Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return await Task.FromResult(File.Exists(GetSessionPath(sessionId)));
    }

    public async Task<int> GetSessionCountAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var options = new SessionQueryOptions { IncludeArchived = includeArchived };
        var sessions = await ListSessionsAsync(options, cancellationToken);
        return sessions.Count;
    }

    public async Task SaveContextAsync(string sessionId, ConversationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(context);

        var contextPath = GetContextPath(sessionId);
        var json = JsonSerializer.Serialize(context, _jsonOptions);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(contextPath, json, cancellationToken);
            _logger.LogDebug("Saved context for session: {SessionId}", sessionId);
        }
        finally
        {
            _fileLock.Release();
        }

        // Update session metadata
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session != null)
        {
            var updatedSession = session with
            {
                MessageCount = context.Messages.Count,
                UpdatedAt = DateTime.UtcNow
            };
            await SaveSessionAsync(updatedSession, cancellationToken);
        }
    }

    public async Task<ConversationContext?> LoadContextAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var contextPath = GetContextPath(sessionId);
        if (!File.Exists(contextPath))
        {
            _logger.LogDebug("Context not found for session: {SessionId}", sessionId);
            return null;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(contextPath, cancellationToken);
            var context = JsonSerializer.Deserialize<ConversationContext>(json, _jsonOptions);

            _logger.LogDebug("Loaded context for session: {SessionId}", sessionId);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load context for session: {SessionId}", sessionId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveSessionAsync(Session session, CancellationToken cancellationToken)
    {
        var sessionPath = GetSessionPath(session.SessionId);
        var json = JsonSerializer.Serialize(session, _jsonOptions);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(sessionPath, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private string GetSessionPath(string sessionId)
    {
        // Validate session ID to prevent path traversal attacks
        if (string.IsNullOrWhiteSpace(sessionId) ||
            sessionId.Contains("..") ||
            sessionId.Contains('/') ||
            sessionId.Contains('\\') ||
            Path.GetInvalidFileNameChars().Any(sessionId.Contains))
        {
            throw new ArgumentException(
                "Invalid session ID format. Session IDs must not contain path separators or special characters.",
                nameof(sessionId));
        }

        var sessionPath = Path.Combine(_sessionsDirectory, $"{sessionId}.session.json");
        var fullPath = Path.GetFullPath(sessionPath);
        var normalizedSessionsDir = Path.GetFullPath(_sessionsDirectory);

        // Ensure the resolved path is within the sessions directory
        if (!fullPath.StartsWith(normalizedSessionsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.Equals(normalizedSessionsDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid session ID. Path traversal detected.",
                nameof(sessionId));
        }

        return fullPath;
    }

    private string GetContextPath(string sessionId)
    {
        return Path.Combine(_sessionsDirectory, $"{sessionId}.context.json");
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }
        return path;
    }
}
