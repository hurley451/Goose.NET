using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Goose.Core.Abstractions;
using Goose.Core.Configuration;
using Goose.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Goose.Core.Services;

/// <summary>
/// Optimized file system-based session manager with caching and improved I/O performance
/// </summary>
public class OptimizedFileSystemSessionManager : ISessionManager
{
    private readonly ILogger<OptimizedFileSystemSessionManager> _logger;
    private readonly string _sessionsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _compactJsonOptions;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    private const string CacheKeyPrefix = "session:";
    private const string ContextCacheKeyPrefix = "context:";

    public OptimizedFileSystemSessionManager(
        ILogger<OptimizedFileSystemSessionManager> logger,
        IOptions<GooseOptions> options,
        IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        ArgumentNullException.ThrowIfNull(options);

        // Get sessions directory from configuration or use default
        var sessionDir = options.Value.SessionDirectory ?? "~/.goose/sessions";
        _sessionsDirectory = ExpandPath(sessionDir);

        // Ensure directory exists
        Directory.CreateDirectory(_sessionsDirectory);

        // Per-file locks for better concurrency
        _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        // Configure JSON serialization (user-facing, pretty-printed)
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

        // Compact JSON for internal storage (faster)
        _compactJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        _logger.LogInformation("Optimized session manager initialized with directory: {Directory}", _sessionsDirectory);
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

        // Try cache first
        var cacheKey = GetCacheKey(sessionId);
        if (_cache.TryGetValue<Session>(cacheKey, out var cachedSession))
        {
            _logger.LogDebug("Session retrieved from cache: {SessionId}", sessionId);
            return cachedSession;
        }

        var sessionPath = GetSessionPath(sessionId);
        if (!File.Exists(sessionPath))
        {
            _logger.LogDebug("Session not found: {SessionId}", sessionId);
            return null;
        }

        var fileLock = GetFileLock(sessionId);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(sessionPath, cancellationToken);
            var session = JsonSerializer.Deserialize<Session>(json, _compactJsonOptions);

            // Cache the session
            if (session != null)
            {
                _cache.Set(cacheKey, session, _cacheExpiration);
            }

            _logger.LogDebug("Retrieved session from disk: {SessionId}", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session: {SessionId}", sessionId);
            throw;
        }
        finally
        {
            fileLock.Release();
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

        var fileLock = GetFileLock(sessionId);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            File.Delete(sessionPath);

            // Also delete context file if it exists
            var contextPath = GetContextPath(sessionId);
            if (File.Exists(contextPath))
            {
                File.Delete(contextPath);
            }

            // Invalidate cache
            _cache.Remove(GetCacheKey(sessionId));
            _cache.Remove(GetContextCacheKey(sessionId));

            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
            return true;
        }
        finally
        {
            fileLock.Release();
            // Remove the lock from dictionary after use
            _fileLocks.TryRemove(sessionId, out _);
        }
    }

    public async Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sessions = new List<SessionSummary>();
        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.session.json");

        // Use parallel processing for large session counts
        if (sessionFiles.Length > 20)
        {
            var sessionTasks = sessionFiles.Select(async file =>
            {
                try
                {
                    var sessionId = Path.GetFileNameWithoutExtension(file.Replace(".session", ""));

                    // Fast path: Read minimal data for filtering without full deserialization
                    var summary = await ReadSessionSummaryAsync(sessionId, cancellationToken);
                    if (summary == null)
                        return null;

                    // Apply filters
                    if (!ShouldIncludeSession(summary, options))
                        return null;

                    return summary;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session from file: {File}", file);
                    return null;
                }
            });

            var results = await Task.WhenAll(sessionTasks);
            sessions = results.Where(s => s != null).Cast<SessionSummary>().ToList();
        }
        else
        {
            // Sequential processing for small counts
            foreach (var file in sessionFiles)
            {
                try
                {
                    var sessionId = Path.GetFileNameWithoutExtension(file.Replace(".session", ""));
                    var summary = await ReadSessionSummaryAsync(sessionId, cancellationToken);

                    if (summary != null && ShouldIncludeSession(summary, options))
                    {
                        sessions.Add(summary);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session from file: {File}", file);
                }
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

        // Use pretty JSON for exports
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

    public Task<bool> SessionExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Check cache first
        if (_cache.TryGetValue<Session>(GetCacheKey(sessionId), out _))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(File.Exists(GetSessionPath(sessionId)));
    }

    public async Task<int> GetSessionCountAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        // Optimized: Just count files matching criteria without full deserialization
        var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.session.json");

        if (includeArchived)
        {
            return sessionFiles.Length;
        }

        // Need to check IsArchived flag, but can do it efficiently
        int count = 0;
        foreach (var file in sessionFiles)
        {
            try
            {
                var sessionId = Path.GetFileNameWithoutExtension(file.Replace(".session", ""));
                var summary = await ReadSessionSummaryAsync(sessionId, cancellationToken);
                if (summary != null && !summary.IsArchived)
                {
                    count++;
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return count;
    }

    public async Task SaveContextAsync(string sessionId, ConversationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(context);

        var contextPath = GetContextPath(sessionId);
        var json = JsonSerializer.Serialize(context, _compactJsonOptions);

        var fileLock = GetFileLock(sessionId + "_context");
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(contextPath, json, cancellationToken);

            // Update cache
            _cache.Set(GetContextCacheKey(sessionId), context, _cacheExpiration);

            _logger.LogDebug("Saved context for session: {SessionId}", sessionId);
        }
        finally
        {
            fileLock.Release();
        }

        // Optimize: Update session metadata without re-reading if in cache
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

        // Try cache first
        var cacheKey = GetContextCacheKey(sessionId);
        if (_cache.TryGetValue<ConversationContext>(cacheKey, out var cachedContext))
        {
            _logger.LogDebug("Context retrieved from cache: {SessionId}", sessionId);
            return cachedContext;
        }

        var contextPath = GetContextPath(sessionId);
        if (!File.Exists(contextPath))
        {
            _logger.LogDebug("Context not found for session: {SessionId}", sessionId);
            return null;
        }

        var fileLock = GetFileLock(sessionId + "_context");
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(contextPath, cancellationToken);
            var context = JsonSerializer.Deserialize<ConversationContext>(json, _compactJsonOptions);

            // Cache the context
            if (context != null)
            {
                _cache.Set(cacheKey, context, _cacheExpiration);
            }

            _logger.LogDebug("Loaded context from disk for session: {SessionId}", sessionId);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load context for session: {SessionId}", sessionId);
            throw;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task SaveSessionAsync(Session session, CancellationToken cancellationToken)
    {
        var sessionPath = GetSessionPath(session.SessionId);
        var json = JsonSerializer.Serialize(session, _compactJsonOptions);

        var fileLock = GetFileLock(session.SessionId);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(sessionPath, json, cancellationToken);

            // Update cache
            _cache.Set(GetCacheKey(session.SessionId), session, _cacheExpiration);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<SessionSummary?> ReadSessionSummaryAsync(string sessionId, CancellationToken cancellationToken)
    {
        // Optimized: Try to read from cache or use minimal deserialization
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
            return null;

        return new SessionSummary
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
        };
    }

    private static bool ShouldIncludeSession(SessionSummary session, SessionQueryOptions? options)
    {
        // Apply filters
        bool includeArchived = options?.IncludeArchived ?? false;
        if (!includeArchived && session.IsArchived)
            return false;

        if (options?.Provider != null && session.Provider != options.Provider)
            return false;

        if (options?.Tags?.Any() == true && (session.Tags == null || !session.Tags.Any(t => options.Tags.Contains(t))))
            return false;

        if (options?.CreatedAfter != null && session.CreatedAt < options.CreatedAfter)
            return false;

        if (options?.CreatedBefore != null && session.CreatedAt > options.CreatedBefore)
            return false;

        if (!string.IsNullOrWhiteSpace(options?.SearchTerm))
        {
            var searchTerm = options.SearchTerm.ToLowerInvariant();
            if (!(session.Name?.ToLowerInvariant().Contains(searchTerm) ?? false) &&
                !(session.Description?.ToLowerInvariant().Contains(searchTerm) ?? false))
                return false;
        }

        return true;
    }

    private SemaphoreSlim GetFileLock(string key)
    {
        return _fileLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private static string GetCacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";

    private static string GetContextCacheKey(string sessionId) => $"{ContextCacheKeyPrefix}{sessionId}";

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

        var contextPath = Path.Combine(_sessionsDirectory, $"{sessionId}.context.json");
        var fullPath = Path.GetFullPath(contextPath);
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
