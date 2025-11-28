using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Goose.Benchmarks;

/// <summary>
/// Benchmarks for message processing performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class MessageProcessingBenchmarks
{
    private ConversationAgent _agent = null!;
    private ConversationContext _context = null!;
    private Mock<IProvider> _mockProvider = null!;
    private Mock<IToolRegistry> _mockToolRegistry = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mockProvider = new Mock<IProvider>();
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockToolRegistry.Setup(r => r.GetAllTools()).Returns(new List<ITool>());

        _agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            NullLogger<ConversationAgent>.Instance);

        _context = new ConversationContext
        {
            SessionId = "benchmark-session",
            WorkingDirectory = "/tmp"
        };

        // Setup simple provider response
        var response = new AgentResponse
        {
            Content = "Benchmark response",
            ToolResults = new List<ToolResult>()
        };

        _mockProvider.Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<ProviderOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    [Benchmark]
    public async Task ProcessSingleMessage()
    {
        await _agent.ProcessMessageAsync("Hello", _context);
    }

    [Benchmark]
    public async Task ProcessMessage_WithExistingContext()
    {
        // Context already has 10 messages
        var contextWithHistory = new ConversationContext
        {
            SessionId = "benchmark-session",
            WorkingDirectory = "/tmp",
            Messages = Enumerable.Range(0, 10).Select(i => new Message
            {
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i}"
            }).ToList()
        };

        await _agent.ProcessMessageAsync("New message", contextWithHistory);
    }

    [Benchmark]
    public async Task ProcessMessage_WithLargeContext()
    {
        // Context with 100 messages
        var largeContext = new ConversationContext
        {
            SessionId = "benchmark-session",
            WorkingDirectory = "/tmp",
            Messages = Enumerable.Range(0, 100).Select(i => new Message
            {
                Role = i % 2 == 0 ? MessageRole.User : MessageRole.Assistant,
                Content = $"Message {i} with some longer content to simulate real conversation"
            }).ToList()
        };

        await _agent.ProcessMessageAsync("New message", largeContext);
    }

    [Benchmark]
    public void CreateConversationContext()
    {
        var ctx = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/tmp"
        };
    }

    [Benchmark]
    public void AddMessageToContext()
    {
        _context.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Content = "Test message"
        });
    }

    [Benchmark]
    public void CreateMessage()
    {
        var msg = new Message
        {
            Role = MessageRole.User,
            Content = "Test message",
            Timestamp = DateTime.UtcNow
        };
    }
}
