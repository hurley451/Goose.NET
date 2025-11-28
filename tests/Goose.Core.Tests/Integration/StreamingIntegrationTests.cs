using System.Text.Json;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Integration;

/// <summary>
/// Integration tests for streaming functionality
/// </summary>
public class StreamingIntegrationTests
{
    private readonly Mock<IProvider> _mockProvider;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<ILogger<ConversationAgent>> _mockLogger;
    private readonly Mock<IPermissionSystem> _mockPermissionSystem;
    private readonly Mock<IPermissionStore> _mockPermissionStore;
    private readonly Mock<IPermissionInspector> _mockPermissionInspector;
    private readonly ConversationAgent _agent;

    public StreamingIntegrationTests()
    {
        _mockProvider = new Mock<IProvider>();
        _mockToolRegistry = new Mock<IToolRegistry>();
        _mockLogger = new Mock<ILogger<ConversationAgent>>();
        _mockPermissionSystem = new Mock<IPermissionSystem>();
        _mockPermissionStore = new Mock<IPermissionStore>();
        _mockPermissionInspector = new Mock<IPermissionInspector>();

        // Setup permission system to always allow for these tests
        _mockPermissionSystem.Setup(ps => ps.RequestPermissionAsync(
            It.IsAny<ToolCall>(),
            It.IsAny<ToolRiskLevel>(),
            It.IsAny<ToolContext>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionResponse
            {
                Decision = PermissionDecision.Allow,
                RememberDecision = false
            });

        _agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);
    }

    [Fact]
    public async Task ProcessMessageStreamAsync_WithSimpleResponse_YieldsChunksAndFinalResponse()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/test"
        };

        var chunks = new List<StreamChunk>
        {
            new() { Content = "Hello " },
            new() { Content = "world" },
            new() { Content = "!", IsFinal = true }
        };

        _mockProvider.Setup(p => p.StreamAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<ProviderOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ProcessMessageStreamAsync("Hello", context))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(4, results.Count); // 3 chunks + 1 final response

        // Verify chunks
        Assert.IsType<StreamChunk>(results[0]);
        Assert.Equal("Hello ", ((StreamChunk)results[0]).Content);

        Assert.IsType<StreamChunk>(results[1]);
        Assert.Equal("world", ((StreamChunk)results[1]).Content);

        Assert.IsType<StreamChunk>(results[2]);
        Assert.Equal("!", ((StreamChunk)results[2]).Content);

        // Verify final response
        Assert.IsType<AgentResponse>(results[3]);
        var finalResponse = (AgentResponse)results[3];
        Assert.Equal("Hello world!", finalResponse.Content);
        Assert.Empty(finalResponse.ToolResults);
    }

    [Fact]
    public async Task ProcessMessageStreamAsync_WithToolCall_YieldsChunksToolResultsAndResponse()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/test"
        };

        var toolCall = new ToolCall
        {
            Id = "tool-1",
            Name = "test_tool",
            Parameters = JsonSerializer.Serialize(new { param = "value" })
        };

        var initialChunks = new List<StreamChunk>
        {
            new() { Content = "Let me use a tool", ToolCall = toolCall, IsFinal = true }
        };

        var followUpChunks = new List<StreamChunk>
        {
            new() { Content = "Tool completed", IsFinal = true }
        };

        var setupSequence = _mockProvider.SetupSequence(p => p.StreamAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()));

        setupSequence.Returns(ToAsyncEnumerable(initialChunks));
        setupSequence.Returns(ToAsyncEnumerable(followUpChunks));

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<ToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "tool-1",
                Success = true,
                Output = "Tool output"
            });

        _mockToolRegistry.Setup(r => r.TryGetTool("test_tool", out It.Ref<ITool>.IsAny))
            .Returns((string name, out ITool tool) =>
            {
                tool = mockTool.Object;
                return true;
            });

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ProcessMessageStreamAsync("Do something", context))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal(4, results.Count); // 1 initial chunk + 1 tool result + 1 followup chunk + 1 final response

        Assert.IsType<StreamChunk>(results[0]);
        Assert.IsType<ToolResult>(results[1]);
        Assert.IsType<StreamChunk>(results[2]);
        Assert.IsType<AgentResponse>(results[3]);

        var toolResult = (ToolResult)results[1];
        Assert.Equal("tool-1", toolResult.ToolCallId);
        Assert.True(toolResult.Success);
        Assert.Equal("Tool output", toolResult.Output);

        var finalResponse = (AgentResponse)results[3];
        Assert.Single(finalResponse.ToolResults);
    }

    [Fact]
    public async Task ProcessMessageStreamAsync_WithMultipleToolCalls_ProcessesAllTools()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/test"
        };

        var toolCall1 = new ToolCall
        {
            Id = "tool-1",
            Name = "tool_a",
            Parameters = "{}"
        };

        var toolCall2 = new ToolCall
        {
            Id = "tool-2",
            Name = "tool_b",
            Parameters = "{}"
        };

        var initialChunks = new List<StreamChunk>
        {
            new() { Content = "Using tools", ToolCall = toolCall1 },
            new() { Content = "", ToolCall = toolCall2, IsFinal = true }
        };

        var followUpChunks = new List<StreamChunk>
        {
            new() { Content = "Done", IsFinal = true }
        };

        var setupSequence = _mockProvider.SetupSequence(p => p.StreamAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()));

        setupSequence.Returns(ToAsyncEnumerable(initialChunks));
        setupSequence.Returns(ToAsyncEnumerable(followUpChunks));

        var mockToolA = new Mock<ITool>();
        mockToolA.Setup(t => t.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<ToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "tool-1",
                Success = true,
                Output = "Output A"
            });

        var mockToolB = new Mock<ITool>();
        mockToolB.Setup(t => t.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<ToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "tool-2",
                Success = true,
                Output = "Output B"
            });

        _mockToolRegistry.Setup(r => r.TryGetTool("tool_a", out It.Ref<ITool>.IsAny))
            .Returns((string name, out ITool tool) =>
            {
                tool = mockToolA.Object;
                return true;
            });

        _mockToolRegistry.Setup(r => r.TryGetTool("tool_b", out It.Ref<ITool>.IsAny))
            .Returns((string name, out ITool tool) =>
            {
                tool = mockToolB.Object;
                return true;
            });

        // Act
        var results = new List<object>();
        await foreach (var item in _agent.ProcessMessageStreamAsync("Multi-tool test", context))
        {
            results.Add(item);
        }

        // Assert
        // 2 initial chunks + 2 tool results + 1 followup chunk + 1 final response = 6
        Assert.Equal(6, results.Count);

        var toolResults = results.OfType<ToolResult>().ToList();
        Assert.Equal(2, toolResults.Count);
        Assert.Contains(toolResults, tr => tr.ToolCallId == "tool-1");
        Assert.Contains(toolResults, tr => tr.ToolCallId == "tool-2");

        var finalResponse = (AgentResponse)results[^1];
        Assert.Equal(2, finalResponse.ToolResults.Count);
    }

    [Fact]
    public async Task ProcessMessageStreamAsync_UpdatesContextMessages()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/test"
        };

        var initialMessageCount = context.Messages.Count;

        var chunks = new List<StreamChunk>
        {
            new() { Content = "Response", IsFinal = true }
        };

        _mockProvider.Setup(p => p.StreamAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<ProviderOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(chunks));

        // Act
        await foreach (var item in _agent.ProcessMessageStreamAsync("Test message", context))
        {
            // Process stream
        }

        // Assert
        Assert.Equal(initialMessageCount + 2, context.Messages.Count); // User message + Assistant message
        Assert.Equal(MessageRole.User, context.Messages[^2].Role);
        Assert.Equal("Test message", context.Messages[^2].Content);
        Assert.Equal(MessageRole.Assistant, context.Messages[^1].Role);
        Assert.Equal("Response", context.Messages[^1].Content);
    }

    [Fact]
    public async Task ProcessMessageStreamAsync_WithPreCancelledToken_ThrowsImmediately()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = "/test"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel the token

        var chunks = new List<StreamChunk>
        {
            new() { Content = "Should not see this", IsFinal = true }
        };

        _mockProvider.Setup(p => p.StreamAsync(
                It.IsAny<IReadOnlyList<Message>>(),
                It.IsAny<ProviderOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<Message> _, ProviderOptions _, CancellationToken ct) =>
                ToAsyncEnumerableWithCancellation(chunks, ct));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in _agent.ProcessMessageStreamAsync("Test", context, cts.Token))
            {
                // Should not get here
                Assert.Fail("Should not process any items with cancelled token");
            }
        });
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerableWithCancellation<T>(
        IEnumerable<T> items,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1, cancellationToken); // Small delay to allow cancellation
            yield return item;
        }
    }
}
