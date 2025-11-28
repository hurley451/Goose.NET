using Goose.Core.Configuration;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Services;

/// <summary>
/// Unit tests for FileSystemSessionManager
/// </summary>
public class SessionManagerTests : IDisposable
{
    private readonly Mock<ILogger<FileSystemSessionManager>> _mockLogger;
    private readonly string _tempDirectory;
    private readonly FileSystemSessionManager _sessionManager;

    public SessionManagerTests()
    {
        _mockLogger = new Mock<ILogger<FileSystemSessionManager>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"goose-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        var options = Options.Create(new GooseOptions
        {
            SessionDirectory = _tempDirectory
        });

        _sessionManager = new FileSystemSessionManager(_mockLogger.Object, options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateSessionAsync_WithValidSession_CreatesSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "test-session-1",
            Name = "Test Session",
            Description = "A test session",
            Provider = "anthropic",
            WorkingDirectory = "/test/dir",
            MessageCount = 0,
            ToolCallCount = 0
        };

        // Act
        var created = await _sessionManager.CreateSessionAsync(session);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("test-session-1", created.SessionId);
        Assert.Equal("Test Session", created.Name);
        Assert.True(created.CreatedAt <= DateTime.UtcNow);
        Assert.True(created.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateSessionAsync_WithExistingSession_ThrowsException()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "duplicate-session",
            Name = "Test",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sessionManager.CreateSessionAsync(session);
        });
    }

    [Fact]
    public async Task GetSessionAsync_WithExistingSession_ReturnsSession()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "get-test",
            Name = "Get Test",
            MessageCount = 5,
            ToolCallCount = 2
        };

        await _sessionManager.CreateSessionAsync(session);

        // Act
        var retrieved = await _sessionManager.GetSessionAsync("get-test");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("get-test", retrieved.SessionId);
        Assert.Equal("Get Test", retrieved.Name);
        Assert.Equal(5, retrieved.MessageCount);
        Assert.Equal(2, retrieved.ToolCallCount);
    }

    [Fact]
    public async Task GetSessionAsync_WithNonExistentSession_ReturnsNull()
    {
        // Act
        var result = await _sessionManager.GetSessionAsync("does-not-exist");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateSessionAsync_WithExistingSession_UpdatesSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "update-test",
            Name = "Original Name",
            MessageCount = 0,
            ToolCallCount = 0
        };

        var created = await _sessionManager.CreateSessionAsync(session);
        var originalUpdatedAt = created.UpdatedAt;

        await Task.Delay(10); // Ensure time difference

        var updated = created with
        {
            Name = "Updated Name",
            MessageCount = 10
        };

        // Act
        var result = await _sessionManager.UpdateSessionAsync(updated);

        // Assert
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(10, result.MessageCount);
        Assert.True(result.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateSessionAsync_WithNonExistentSession_ThrowsException()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "does-not-exist",
            Name = "Test",
            MessageCount = 0,
            ToolCallCount = 0
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _sessionManager.UpdateSessionAsync(session);
        });
    }

    [Fact]
    public async Task DeleteSessionAsync_WithExistingSession_DeletesSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "delete-test",
            Name = "Delete Me",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);

        // Act
        var deleted = await _sessionManager.DeleteSessionAsync("delete-test");

        // Assert
        Assert.True(deleted);
        var retrieved = await _sessionManager.GetSessionAsync("delete-test");
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteSessionAsync_WithNonExistentSession_ReturnsFalse()
    {
        // Act
        var result = await _sessionManager.DeleteSessionAsync("does-not-exist");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ListSessionsAsync_WithMultipleSessions_ReturnsAll()
    {
        // Arrange
        var session1 = new Session
        {
            SessionId = "list-1",
            Name = "Session 1",
            Provider = "anthropic",
            MessageCount = 5,
            ToolCallCount = 1
        };

        var session2 = new Session
        {
            SessionId = "list-2",
            Name = "Session 2",
            Provider = "openai",
            MessageCount = 10,
            ToolCallCount = 3
        };

        await _sessionManager.CreateSessionAsync(session1);
        await _sessionManager.CreateSessionAsync(session2);

        // Act
        var sessions = await _sessionManager.ListSessionsAsync();

        // Assert
        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.SessionId == "list-1");
        Assert.Contains(sessions, s => s.SessionId == "list-2");
    }

    [Fact]
    public async Task ListSessionsAsync_WithProviderFilter_ReturnsFilteredResults()
    {
        // Arrange
        var session1 = new Session
        {
            SessionId = "filter-1",
            Provider = "anthropic",
            MessageCount = 0,
            ToolCallCount = 0
        };

        var session2 = new Session
        {
            SessionId = "filter-2",
            Provider = "openai",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session1);
        await _sessionManager.CreateSessionAsync(session2);

        // Act
        var options = new SessionQueryOptions { Provider = "anthropic" };
        var sessions = await _sessionManager.ListSessionsAsync(options);

        // Assert
        Assert.Single(sessions);
        Assert.Equal("filter-1", sessions[0].SessionId);
    }

    [Fact]
    public async Task ListSessionsAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var session = new Session
            {
                SessionId = $"limit-{i}",
                MessageCount = 0,
                ToolCallCount = 0
            };
            await _sessionManager.CreateSessionAsync(session);
        }

        // Act
        var options = new SessionQueryOptions { Limit = 3 };
        var sessions = await _sessionManager.ListSessionsAsync(options);

        // Assert
        Assert.Equal(3, sessions.Count);
    }

    [Fact]
    public async Task ArchiveSessionAsync_WithExistingSession_ArchivesSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "archive-test",
            Name = "Archive Me",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);

        // Act
        var archived = await _sessionManager.ArchiveSessionAsync("archive-test");

        // Assert
        Assert.True(archived);

        var retrieved = await _sessionManager.GetSessionAsync("archive-test");
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsArchived);
    }

    [Fact]
    public async Task RestoreSessionAsync_WithArchivedSession_RestoresSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "restore-test",
            Name = "Restore Me",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);
        await _sessionManager.ArchiveSessionAsync("restore-test");

        // Act
        var restored = await _sessionManager.RestoreSessionAsync("restore-test");

        // Assert
        Assert.True(restored);

        var retrieved = await _sessionManager.GetSessionAsync("restore-test");
        Assert.NotNull(retrieved);
        Assert.False(retrieved.IsArchived);
    }

    [Fact]
    public async Task ExportSessionAsync_CreatesJsonFile()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "export-test",
            Name = "Export Me",
            Description = "Test export",
            MessageCount = 5,
            ToolCallCount = 2
        };

        await _sessionManager.CreateSessionAsync(session);

        var exportPath = Path.Combine(_tempDirectory, "export.json");

        // Act
        await _sessionManager.ExportSessionAsync("export-test", exportPath);

        // Assert
        Assert.True(File.Exists(exportPath));

        var content = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("export-test", content);
        Assert.Contains("Export Me", content);
    }

    [Fact]
    public async Task ImportSessionAsync_FromJsonFile_ImportsSuccessfully()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "import-source",
            Name = "Import Source",
            Description = "Source for import",
            MessageCount = 7,
            ToolCallCount = 3
        };

        await _sessionManager.CreateSessionAsync(session);

        var exportPath = Path.Combine(_tempDirectory, "import-source.json");
        await _sessionManager.ExportSessionAsync("import-source", exportPath);

        // Delete the original
        await _sessionManager.DeleteSessionAsync("import-source");

        // Act
        var imported = await _sessionManager.ImportSessionAsync(exportPath);

        // Assert
        Assert.NotNull(imported);
        Assert.Equal("import-source", imported.SessionId);
        Assert.Equal("Import Source", imported.Name);
        Assert.Equal(7, imported.MessageCount);
        Assert.Equal(3, imported.ToolCallCount);
    }

    [Fact]
    public async Task SessionExistsAsync_WithExistingSession_ReturnsTrue()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "exists-test",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);

        // Act
        var exists = await _sessionManager.SessionExistsAsync("exists-test");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task SessionExistsAsync_WithNonExistentSession_ReturnsFalse()
    {
        // Act
        var exists = await _sessionManager.SessionExistsAsync("does-not-exist");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetSessionCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        for (int i = 0; i < 3; i++)
        {
            var session = new Session
            {
                SessionId = $"count-{i}",
                MessageCount = 0,
                ToolCallCount = 0
            };
            await _sessionManager.CreateSessionAsync(session);
        }

        // Act
        var count = await _sessionManager.GetSessionCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task SaveAndLoadContextAsync_WorksCorrectly()
    {
        // Arrange
        var session = new Session
        {
            SessionId = "context-test",
            MessageCount = 0,
            ToolCallCount = 0
        };

        await _sessionManager.CreateSessionAsync(session);

        var context = new ConversationContext
        {
            SessionId = "context-test",
            WorkingDirectory = "/test/dir"
        };

        context.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = "Hello"
        });

        context.Messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Content = "Hi there!"
        });

        // Act
        await _sessionManager.SaveContextAsync("context-test", context);
        var loaded = await _sessionManager.LoadContextAsync("context-test");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("context-test", loaded.SessionId);
        Assert.Equal("/test/dir", loaded.WorkingDirectory);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal("Hello", loaded.Messages[0].Content);
        Assert.Equal("Hi there!", loaded.Messages[1].Content);
    }

    [Fact]
    public async Task LoadContextAsync_WithNonExistentContext_ReturnsNull()
    {
        // Act
        var context = await _sessionManager.LoadContextAsync("does-not-exist");

        // Assert
        Assert.Null(context);
    }
}
