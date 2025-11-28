using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end conversation flow with tools
/// </summary>
public class ConversationFlowIntegrationTests
{
    private readonly Mock<IProvider> _mockProvider;
    private readonly Mock<ILogger<ConversationAgent>> _mockLogger;
    private readonly IToolRegistry _toolRegistry;
    private readonly Mock<IPermissionSystem> _mockPermissionSystem;
    private readonly Mock<IPermissionStore> _mockPermissionStore;
    private readonly Mock<IPermissionInspector> _mockPermissionInspector;
    private readonly ConversationAgent _agent;

    public ConversationFlowIntegrationTests()
    {
        _mockProvider = new Mock<IProvider>();
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

        // Create real tool registry with mock tool
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.Description).Returns("A test tool");
        mockTool.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "call_1",
                Success = true,
                Output = "Tool executed successfully",
                Duration = TimeSpan.FromMilliseconds(100)
            });

        var mockToolRegistryLogger = new Mock<ILogger<ToolRegistry>>();
        _toolRegistry = new ToolRegistry(new[] { mockTool.Object }, mockToolRegistryLogger.Object);
        _agent = new ConversationAgent(
            _mockProvider.Object,
            _toolRegistry,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);
    }

    [Fact]
    public async Task FullConversationFlow_WithoutTools_CompletesSuccessfully()
    {
        // Arrange
        var mockResponse = new ProviderResponse
        {
            Content = "Hello! How can I help you today?",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 8 },
            ToolCalls = null,
            StopReason = "end_turn"
        };

        _mockProvider.Setup(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var context = new ConversationContext
        {
            SessionId = "integration-test-1",
            WorkingDirectory = Environment.CurrentDirectory
        };

        // Act
        var response = await _agent.ProcessMessageAsync("Hello", context);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Hello! How can I help you today?", response.Content);
        Assert.Equal(2, context.Messages.Count); // User + Assistant
        Assert.Equal(MessageRole.User, context.Messages[0].Role);
        Assert.Equal(MessageRole.Assistant, context.Messages[1].Role);
    }

    [Fact]
    public async Task FullConversationFlow_WithToolCall_ExecutesToolAndContinues()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "integration-test-2",
            WorkingDirectory = Environment.CurrentDirectory
        };

        // First response: Provider wants to use a tool
        var firstResponse = new ProviderResponse
        {
            Content = "I'll use the test tool",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 5 },
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_1",
                    Name = "test_tool",
                    Parameters = "{}"
                }
            },
            StopReason = "tool_use"
        };

        // Second response: After tool execution
        var secondResponse = new ProviderResponse
        {
            Content = "The tool executed successfully and returned the result.",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 15, OutputTokens = 12 },
            ToolCalls = null,
            StopReason = "end_turn"
        };

        _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstResponse)
            .ReturnsAsync(secondResponse);

        // Act
        var response = await _agent.ProcessMessageAsync("Use the test tool", context);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("The tool executed successfully and returned the result.", response.Content);
        Assert.NotNull(response.ToolResults);
        Assert.Single(response.ToolResults);
        Assert.True(response.ToolResults[0].Success);
        Assert.Equal("Tool executed successfully", response.ToolResults[0].Output);

        // Verify message history includes tool call and result
        Assert.True(context.Messages.Count >= 3); // User, Assistant with tool call, Tool result, Final assistant
    }

    [Fact]
    public async Task FullConversationFlow_MultiTurnConversation_MaintainsContext()
    {
        // Arrange
        var context = new ConversationContext
        {
            SessionId = "integration-test-3",
            WorkingDirectory = Environment.CurrentDirectory
        };

        var responses = new[]
        {
            new ProviderResponse
            {
                Content = "My name is Goose!",
                Model = "test-model",
                Usage = new ProviderUsage { InputTokens = 5, OutputTokens = 4 },
                StopReason = "end_turn"
            },
            new ProviderResponse
            {
                Content = "I just told you - my name is Goose!",
                Model = "test-model",
                Usage = new ProviderUsage { InputTokens = 15, OutputTokens = 8 },
                StopReason = "end_turn"
            }
        };

        _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(responses[0])
            .ReturnsAsync(responses[1]);

        // Act - Turn 1
        var response1 = await _agent.ProcessMessageAsync("What's your name?", context);

        // Act - Turn 2
        var response2 = await _agent.ProcessMessageAsync("What did you just say?", context);

        // Assert
        Assert.Equal("My name is Goose!", response1.Content);
        Assert.Equal("I just told you - my name is Goose!", response2.Content);
        Assert.Equal(4, context.Messages.Count); // 2 user messages + 2 assistant responses

        // Verify context is passed to provider on second call
        _mockProvider.Verify(p => p.GenerateAsync(
            It.Is<IReadOnlyList<Message>>(m => m.Count >= 3), // Should include previous conversation
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task FullConversationFlow_ToolExecutionFails_ReturnsErrorGracefully()
    {
        // Arrange
        var mockFailingTool = new Mock<ITool>();
        mockFailingTool.Setup(t => t.Name).Returns("failing_tool");
        mockFailingTool.Setup(t => t.Description).Returns("A tool that fails");
        mockFailingTool.Setup(t => t.ParameterSchema).Returns("{}");
        mockFailingTool.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "call_fail",
                Success = false,
                Error = "Tool execution failed intentionally",
                Duration = TimeSpan.FromMilliseconds(50)
            });

        var mockToolRegistryLogger2 = new Mock<ILogger<ToolRegistry>>();
        var toolRegistryWithFailingTool = new ToolRegistry(new[] { mockFailingTool.Object }, mockToolRegistryLogger2.Object);
        var agentWithFailingTool = new ConversationAgent(
            _mockProvider.Object,
            toolRegistryWithFailingTool,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "integration-test-4",
            WorkingDirectory = Environment.CurrentDirectory
        };

        var providerResponse = new ProviderResponse
        {
            Content = "Let me try the tool",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 5 },
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_fail",
                    Name = "failing_tool",
                    Parameters = "{}"
                }
            },
            StopReason = "tool_use"
        };

        var finalResponse = new ProviderResponse
        {
            Content = "The tool failed, but I handled it gracefully.",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 20, OutputTokens = 10 },
            StopReason = "end_turn"
        };

        _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResponse)
            .ReturnsAsync(finalResponse);

        // Act
        var response = await agentWithFailingTool.ProcessMessageAsync("Try the failing tool", context);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.ToolResults);
        Assert.Single(response.ToolResults);
        Assert.False(response.ToolResults[0].Success);
        Assert.Equal("Tool execution failed intentionally", response.ToolResults[0].Error);
    }

    [Fact]
    public async Task FullConversationFlow_MultipleToolCalls_ExecutesAllTools()
    {
        // Arrange
        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("tool_1");
        mockTool1.Setup(t => t.Description).Returns("First tool");
        mockTool1.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool1.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "call_1",
                Success = true,
                Output = "Result from tool 1",
                Duration = TimeSpan.FromMilliseconds(100)
            });

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("tool_2");
        mockTool2.Setup(t => t.Description).Returns("Second tool");
        mockTool2.Setup(t => t.ParameterSchema).Returns("{}");
        mockTool2.Setup(t => t.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<ToolContext>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResult
            {
                ToolCallId = "call_2",
                Success = true,
                Output = "Result from tool 2",
                Duration = TimeSpan.FromMilliseconds(150)
            });

        var mockToolRegistryLogger3 = new Mock<ILogger<ToolRegistry>>();
        var multiToolRegistry = new ToolRegistry(new[] { mockTool1.Object, mockTool2.Object }, mockToolRegistryLogger3.Object);
        var multiToolAgent = new ConversationAgent(
            _mockProvider.Object,
            multiToolRegistry,
            _mockLogger.Object,
            _mockPermissionSystem.Object,
            _mockPermissionStore.Object,
            _mockPermissionInspector.Object);

        var context = new ConversationContext
        {
            SessionId = "integration-test-5",
            WorkingDirectory = Environment.CurrentDirectory
        };

        var providerResponse = new ProviderResponse
        {
            Content = "Using multiple tools",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 5 },
            ToolCalls = new List<ToolCall>
            {
                new ToolCall { Id = "call_1", Name = "tool_1", Parameters = "{}" },
                new ToolCall { Id = "call_2", Name = "tool_2", Parameters = "{}" }
            },
            StopReason = "tool_use"
        };

        var finalResponse = new ProviderResponse
        {
            Content = "Both tools executed successfully",
            Model = "test-model",
            Usage = new ProviderUsage { InputTokens = 25, OutputTokens = 10 },
            StopReason = "end_turn"
        };

        _mockProvider.SetupSequence(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerResponse)
            .ReturnsAsync(finalResponse);

        // Act
        var response = await multiToolAgent.ProcessMessageAsync("Use both tools", context);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.ToolResults);
        Assert.Equal(2, response.ToolResults.Count);
        Assert.All(response.ToolResults, tr => Assert.True(tr.Success));
        Assert.Contains(response.ToolResults, tr => tr.Output == "Result from tool 1");
        Assert.Contains(response.ToolResults, tr => tr.Output == "Result from tool 2");
    }
}
