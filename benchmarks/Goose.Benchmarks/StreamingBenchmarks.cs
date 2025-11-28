using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Goose.Benchmarks;

/// <summary>
/// Benchmarks for streaming performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StreamingBenchmarks
{
    private ConversationAgent _agent = null!;
    private ConversationContext _context = null!;
    private Mock<IProvider> _mockProvider = null!;
    private Mock<IToolRegistry> _mockToolRegistry = null!;

    [Params(10, 50, 100)]
    public int ChunkCount { get; set; }

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
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Setup streaming chunks
        var chunks = Enumerable.Range(0, ChunkCount).Select(i => new StreamChunk
        {
            Content = $"Chunk {i} ",
            IsFinal = i == ChunkCount - 1
        }).ToList();

        _mockProvider.Setup(p => p.StreamAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<ProviderOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));
    }

    [Benchmark]
    public async Task StreamMessage()
    {
        await foreach (var item in _agent.ProcessMessageStreamAsync("Test", _context))
        {
            // Process streaming items
        }
    }

    [Benchmark]
    public async Task StreamMessage_CollectResults()
    {
        var results = new List<object>();
        await foreach (var item in _agent.ProcessMessageStreamAsync("Test", _context))
        {
            results.Add(item);
        }
    }

    [Benchmark]
    public async Task StreamMessage_ProcessChunks()
    {
        var content = new System.Text.StringBuilder();
        await foreach (var item in _agent.ProcessMessageStreamAsync("Test", _context))
        {
            if (item is StreamChunk chunk)
            {
                content.Append(chunk.Content);
            }
        }
        var finalContent = content.ToString();
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
