using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Goose.Core.Models;
using Goose.Core.Services;
using System.Text.Json;

namespace Goose.Benchmarks;

/// <summary>
/// Benchmarks for session I/O operations
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class SessionIOBenchmarks
{
    private string _tempDirectory = null!;
    private SessionManager _sessionManager = null!;
    private Session _testSession = null!;
    private ConversationContext _smallContext = null!;
    private ConversationContext _mediumContext = null!;
    private ConversationContext _largeContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"goose-benchmarks-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _sessionManager = new SessionManager(_tempDirectory);

        _testSession = new Session
        {
            SessionId = "benchmark-session",
            Name = "Benchmark Session",
            Provider = "anthropic",
            MessageCount = 0,
            ToolCallCount = 0,
            WorkingDirectory = "/tmp"
        };

        // Small context: 10 messages
        _smallContext = new ConversationContext
        {
            SessionId = "small-session",
            WorkingDirectory = "/tmp",
            Messages = Enumerable.Range(0, 10).Select(i => new Message
            {
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i}"
            }).ToList()
        };

        // Medium context: 50 messages
        _mediumContext = new ConversationContext
        {
            SessionId = "medium-session",
            WorkingDirectory = "/tmp",
            Messages = Enumerable.Range(0, 50).Select(i => new Message
            {
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i} with some additional content to make it more realistic"
            }).ToList()
        };

        // Large context: 200 messages
        _largeContext = new ConversationContext
        {
            SessionId = "large-session",
            WorkingDirectory = "/tmp",
            Messages = Enumerable.Range(0, 200).Select(i => new Message
            {
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i} with quite a bit more content to simulate a long conversation with detailed responses and explanations"
            }).ToList()
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Benchmark]
    public async Task CreateSession()
    {
        var session = new Session
        {
            SessionId = $"session-{Guid.NewGuid()}",
            Name = "Test Session",
            Provider = "anthropic",
            MessageCount = 0,
            ToolCallCount = 0
        };
        await _sessionManager.CreateSessionAsync(session);
    }

    [Benchmark]
    public async Task SaveContext_Small()
    {
        await _sessionManager.SaveContextAsync("small-session", _smallContext);
    }

    [Benchmark]
    public async Task SaveContext_Medium()
    {
        await _sessionManager.SaveContextAsync("medium-session", _mediumContext);
    }

    [Benchmark]
    public async Task SaveContext_Large()
    {
        await _sessionManager.SaveContextAsync("large-session", _largeContext);
    }

    [Benchmark]
    public async Task LoadContext_Small()
    {
        await _sessionManager.SaveContextAsync("small-session", _smallContext);
        await _sessionManager.LoadContextAsync("small-session");
    }

    [Benchmark]
    public async Task LoadContext_Medium()
    {
        await _sessionManager.SaveContextAsync("medium-session", _mediumContext);
        await _sessionManager.LoadContextAsync("medium-session");
    }

    [Benchmark]
    public async Task LoadContext_Large()
    {
        await _sessionManager.SaveContextAsync("large-session", _largeContext);
        await _sessionManager.LoadContextAsync("large-session");
    }

    [Benchmark]
    public async Task ListSessions()
    {
        // Create a few sessions first
        for (int i = 0; i < 10; i++)
        {
            await _sessionManager.CreateSessionAsync(new Session
            {
                SessionId = $"list-session-{i}",
                Name = $"Session {i}",
                Provider = "anthropic",
                MessageCount = 0,
                ToolCallCount = 0
            });
        }

        await _sessionManager.ListSessionsAsync();
    }

    [Benchmark]
    public void SerializeContext_Small()
    {
        JsonSerializer.Serialize(_smallContext);
    }

    [Benchmark]
    public void SerializeContext_Medium()
    {
        JsonSerializer.Serialize(_mediumContext);
    }

    [Benchmark]
    public void SerializeContext_Large()
    {
        JsonSerializer.Serialize(_largeContext);
    }

    [Benchmark]
    public void DeserializeContext_Small()
    {
        var json = JsonSerializer.Serialize(_smallContext);
        JsonSerializer.Deserialize<ConversationContext>(json);
    }

    [Benchmark]
    public void DeserializeContext_Medium()
    {
        var json = JsonSerializer.Serialize(_mediumContext);
        JsonSerializer.Deserialize<ConversationContext>(json);
    }

    [Benchmark]
    public void DeserializeContext_Large()
    {
        var json = JsonSerializer.Serialize(_largeContext);
        JsonSerializer.Deserialize<ConversationContext>(json);
    }
}
