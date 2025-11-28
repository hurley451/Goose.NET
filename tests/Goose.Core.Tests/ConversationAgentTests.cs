using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Goose.Core.Tests;

public class ConversationAgentTests
{
    private readonly Mock<ILogger<ConversationAgent>> _mockLogger;
    private readonly Mock<IProvider> _mockProvider;
    private readonly Mock<IToolRegistry> _mockToolRegistry;
    private readonly Mock<IPermissionSystem> _mockPermissionSystem;
    private readonly Mock<IPermissionStore> _mockPermissionStore;
    private readonly Mock<IPermissionInspector> _mockPermissionInspector;

    public ConversationAgentTests()
    {
        _mockLogger = new Mock<ILogger<ConversationAgent>>();
        _mockProvider = new Mock<IProvider>();
        _mockToolRegistry = new Mock<IToolRegistry>();
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
    }

    [Fact]
    public async Task ProcessMessageAsync_ReturnsResponse_WhenProviderReturnsContent()
    {
        // Arrange
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");

        var mockResponse = new ProviderResponse
        {
            Content = "Test response",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 20 },
            ToolCalls = null,
            StopReason = "end_turn"
        };

        _mockProvider.Setup(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(mockResponse);

        var agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = Environment.CurrentDirectory
        };
        context.ProviderOptions = new ProviderOptions();

        // Act
        var result = await agent.ProcessMessageAsync("Hello", context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test response", result.Content);
        Assert.Empty(result.ToolResults);

        // Verify messages were added to context (user + assistant)
        Assert.Equal(2, context.Messages.Count);
        Assert.Equal(MessageRole.User, context.Messages[0].Role);
        Assert.Equal("Hello", context.Messages[0].Content);
        Assert.Equal(MessageRole.Assistant, context.Messages[1].Role);
        Assert.Equal("Test response", context.Messages[1].Content);
    }

    [Fact]
    public async Task ProcessMessageAsync_ExecutesToolCalls()
    {
        // Arrange
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("file-tool");
        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new ToolResult
        {
            ToolCallId = "tool-call-1",
            Success = true,
            Output = "File content"
        });

        _mockToolRegistry.Setup(tr => tr.TryGetTool("file-tool", out It.Ref<ITool>.IsAny))
            .Returns((string name, out ITool tool) =>
            {
                tool = mockTool.Object;
                return true;
            });

        _mockProvider.Setup(p => p.Name).Returns("TestProvider");

        // First call returns tool call, second call returns final response
        var responseSequence = _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()));

        responseSequence.ReturnsAsync(new ProviderResponse
        {
            Content = "I'll use the file tool",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 20 },
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "tool-call-1",
                    Name = "file-tool",
                    Parameters = "{\"path\":\"/test/file.txt\"}"
                }
            },
            StopReason = "tool_calls"
        });

        responseSequence.ReturnsAsync(new ProviderResponse
        {
            Content = "Here is the file content: File content",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 30, OutputTokens = 40 },
            ToolCalls = null,
            StopReason = "end_turn"
        });

        var agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = Environment.CurrentDirectory
        };
        context.ProviderOptions = new ProviderOptions();

        // Act
        var result = await agent.ProcessMessageAsync("Read the file", context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Here is the file content: File content", result.Content);
        Assert.Single(result.ToolResults);
        Assert.Equal("tool-call-1", result.ToolResults[0].ToolCallId);
        Assert.True(result.ToolResults[0].Success);

        // Verify tool was executed
        mockTool.Verify(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_HandlesToolNotFound()
    {
        // Arrange
        _mockToolRegistry.Setup(tr => tr.TryGetTool(It.IsAny<string>(), out It.Ref<ITool>.IsAny))
            .Returns(false);

        var responseSequence = _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()));

        responseSequence.ReturnsAsync(new ProviderResponse
        {
            Content = "I'll use a non-existent tool",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 20 },
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "tool-call-1",
                    Name = "non-existent-tool",
                    Parameters = "{}"
                }
            }
        });

        responseSequence.ReturnsAsync(new ProviderResponse
        {
            Content = "Tool not found, sorry",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 20, OutputTokens = 30 },
            ToolCalls = null
        });

        var agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = Environment.CurrentDirectory
        };
        context.ProviderOptions = new ProviderOptions();

        // Act
        var result = await agent.ProcessMessageAsync("Use unknown tool", context);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.ToolResults);
        Assert.False(result.ToolResults[0].Success);
        Assert.Contains("not found", result.ToolResults[0].Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_ThrowsArgumentNullException_WhenMessageIsNull()
    {
        // Arrange
        var agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "test",
            WorkingDirectory = "/test"
        };
        context.ProviderOptions = new ProviderOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.ProcessMessageAsync(null!, context));
    }

    [Fact]
    public async Task ProcessMessageAsync_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var agent = new ConversationAgent(
            _mockProvider.Object,
            _mockToolRegistry.Object,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => agent.ProcessMessageAsync("Hello", null!));
    }
}
