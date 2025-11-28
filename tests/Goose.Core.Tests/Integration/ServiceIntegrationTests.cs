using Goose.Core.Abstractions;
using Goose.Core.Extensions;
using Goose.Core.Models;
using Goose.Core.Models.Permissions;
using Goose.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Goose.Core.Tests.Integration;

/// <summary>
/// Integration tests for service configuration and dependency injection
/// </summary>
public class ServiceIntegrationTests
{
    [Fact]
    public void ServiceCollection_AddGooseCore_RegistersAllCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Goose:DefaultProvider"] = "test",
                ["Goose:SessionDirectory"] = "~/.goose/sessions",
                ["Goose:MaxTokens"] = "4096",
                ["Goose:Temperature"] = "0.7"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddGooseCore(configuration);

        // Register a mock provider so ConversationAgent can be resolved
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("test");
        services.AddScoped<IProvider>(_ => mockProvider.Object);

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var toolRegistry = serviceProvider.GetService<IToolRegistry>();
        var conversationAgent = serviceProvider.GetService<IConversationAgent>();

        Assert.NotNull(toolRegistry);
        Assert.NotNull(conversationAgent);
        Assert.IsType<ToolRegistry>(toolRegistry);
        Assert.IsType<ConversationAgent>(conversationAgent);
    }

    [Fact]
    public void ServiceCollection_AddGooseCoreWithAction_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddGooseCore(options =>
        {
            options.DefaultProvider = "custom-provider";
            options.MaxTokens = 8192;
            options.Temperature = 0.5;
            options.SessionDirectory = "/custom/sessions";
        });

        // Register a mock provider so ConversationAgent can be resolved
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("custom-provider");
        services.AddScoped<IProvider>(_ => mockProvider.Object);

        var serviceProvider = services.BuildServiceProvider();

        // Assert - Services are registered
        var toolRegistry = serviceProvider.GetService<IToolRegistry>();
        var conversationAgent = serviceProvider.GetService<IConversationAgent>();

        Assert.NotNull(toolRegistry);
        Assert.NotNull(conversationAgent);
    }

    [Fact]
    public async Task EndToEnd_ServiceConfiguration_CanProcessMessage()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Goose:DefaultProvider"] = "test",
                ["Goose:MaxTokens"] = "4096"
            })
            .Build();

        services.AddLogging();
        services.AddGooseCore(configuration);

        // Register mock provider
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("test");
        mockProvider.Setup(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderResponse
            {
                Content = "Test response",
                Model = "test-model",
                Usage = new ProviderUsage { InputTokens = 5, OutputTokens = 3 },
                StopReason = "end_turn"
            });

        services.AddScoped<IProvider>(_ => mockProvider.Object);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var agent = serviceProvider.GetRequiredService<IConversationAgent>();
        var context = new ConversationContext
        {
            SessionId = "test-session",
            WorkingDirectory = Environment.CurrentDirectory
        };

        var response = await agent.ProcessMessageAsync("Hello", context);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Test response", response.Content);
        mockProvider.Verify(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ToolRegistry_WithServiceCollection_RegistersAndResolvesTools()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var mockTool1 = new Mock<ITool>();
        mockTool1.Setup(t => t.Name).Returns("tool_1");
        mockTool1.Setup(t => t.Description).Returns("First tool");
        mockTool1.Setup(t => t.ParameterSchema).Returns("{}");

        var mockTool2 = new Mock<ITool>();
        mockTool2.Setup(t => t.Name).Returns("tool_2");
        mockTool2.Setup(t => t.Description).Returns("Second tool");
        mockTool2.Setup(t => t.ParameterSchema).Returns("{}");

        services.AddSingleton<ITool>(mockTool1.Object);
        services.AddSingleton<ITool>(mockTool2.Object);
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
        var tools = toolRegistry.GetAllTools();

        // Assert
        Assert.NotNull(toolRegistry);
        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => t.Name == "tool_1");
        Assert.Contains(tools, t => t.Name == "tool_2");
    }

    [Fact]
    public async Task ConversationAgent_WithMockedDependencies_ProcessesMessagesCorrectly()
    {
        // Arrange
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("mock-provider");
        mockProvider.Setup(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderResponse
            {
                Content = "Mock response",
                Model = "mock-model",
                Usage = new ProviderUsage { InputTokens = 10, OutputTokens = 5 },
                StopReason = "end_turn"
            });

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("mock_tool");
        mockTool.Setup(t => t.Description).Returns("Mock tool");
        mockTool.Setup(t => t.ParameterSchema).Returns("{}");

        var mockPermissionSystem = new Mock<IPermissionSystem>();
        mockPermissionSystem.Setup(ps => ps.RequestPermissionAsync(
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

        var mockToolRegistryLogger = new Mock<ILogger<ToolRegistry>>();
        var toolRegistry = new ToolRegistry(new[] { mockTool.Object }, mockToolRegistryLogger.Object);
        var mockLogger = new Mock<ILogger<ConversationAgent>>();
        var agent = new ConversationAgent(
            mockProvider.Object,
            toolRegistry,
            mockLogger.Object,
            mockPermissionSystem.Object,
            new Mock<IPermissionStore>().Object,
            new Mock<IPermissionInspector>().Object);

        var context = new ConversationContext
        {
            SessionId = "mock-session",
            WorkingDirectory = Environment.CurrentDirectory
        };

        // Act
        var response1 = await agent.ProcessMessageAsync("First message", context);
        var response2 = await agent.ProcessMessageAsync("Second message", context);

        // Assert
        Assert.NotNull(response1);
        Assert.NotNull(response2);
        Assert.Equal(4, context.Messages.Count); // 2 user + 2 assistant messages

        // Verify provider was called twice
        mockProvider.Verify(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void ServiceLifetimes_AreConfiguredCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Goose:DefaultProvider"] = "test"
            })
            .Build();

        services.AddLogging();
        services.AddGooseCore(configuration);

        // Add mock provider
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("test");
        services.AddScoped<IProvider>(_ => mockProvider.Object);

        var serviceProvider = services.BuildServiceProvider();

        // Act - Get services multiple times from same scope
        using var scope1 = serviceProvider.CreateScope();
        var toolRegistry1 = scope1.ServiceProvider.GetRequiredService<IToolRegistry>();
        var toolRegistry2 = scope1.ServiceProvider.GetRequiredService<IToolRegistry>();

        var agent1 = scope1.ServiceProvider.GetRequiredService<IConversationAgent>();
        var agent2 = scope1.ServiceProvider.GetRequiredService<IConversationAgent>();

        // Act - Get services from different scope
        using var scope2 = serviceProvider.CreateScope();
        var toolRegistry3 = scope2.ServiceProvider.GetRequiredService<IToolRegistry>();
        var agent3 = scope2.ServiceProvider.GetRequiredService<IConversationAgent>();

        // Assert - ToolRegistry is singleton (same instance across scopes)
        Assert.Same(toolRegistry1, toolRegistry2);
        Assert.Same(toolRegistry1, toolRegistry3);

        // Assert - ConversationAgent is scoped (same within scope, different across scopes)
        Assert.Same(agent1, agent2);
        Assert.NotSame(agent1, agent3);
    }

    [Fact]
    public async Task MultipleAgents_ShareToolRegistry_ButMaintainSeparateConversations()
    {
        // Arrange
        var mockProvider = new Mock<IProvider>();
        mockProvider.Setup(p => p.Name).Returns("shared-provider");
        mockProvider.Setup(p => p.GenerateAsync(
            It.IsAny<IReadOnlyList<Message>>(),
            It.IsAny<ProviderOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderResponse
            {
                Content = "Response",
                Model = "model",
                Usage = new ProviderUsage { InputTokens = 5, OutputTokens = 3 },
                StopReason = "end_turn"
            });

        var mockToolRegistryLogger = new Mock<ILogger<ToolRegistry>>();
        var sharedToolRegistry = new ToolRegistry(Array.Empty<ITool>(), mockToolRegistryLogger.Object);
        var mockLogger1 = new Mock<ILogger<ConversationAgent>>();
        var mockLogger2 = new Mock<ILogger<ConversationAgent>>();

        var mockPermissionSystem = new Mock<IPermissionSystem>();
        mockPermissionSystem.Setup(ps => ps.RequestPermissionAsync(
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

        var agent1 = new ConversationAgent(
            mockProvider.Object,
            sharedToolRegistry,
            mockLogger1.Object,
            mockPermissionSystem.Object,
            new Mock<IPermissionStore>().Object,
            new Mock<IPermissionInspector>().Object);
        var agent2 = new ConversationAgent(
            mockProvider.Object,
            sharedToolRegistry,
            mockLogger2.Object,
            mockPermissionSystem.Object,
            new Mock<IPermissionStore>().Object,
            new Mock<IPermissionInspector>().Object);

        var context1 = new ConversationContext
        {
            SessionId = "session-1",
            WorkingDirectory = Environment.CurrentDirectory
        };

        var context2 = new ConversationContext
        {
            SessionId = "session-2",
            WorkingDirectory = Environment.CurrentDirectory
        };

        // Act
        await agent1.ProcessMessageAsync("Message for agent 1", context1);
        await agent2.ProcessMessageAsync("Message for agent 2", context2);
        await agent1.ProcessMessageAsync("Another message for agent 1", context1);

        // Assert - Contexts are independent
        Assert.Equal(4, context1.Messages.Count); // 2 user + 2 assistant
        Assert.Equal(2, context2.Messages.Count); // 1 user + 1 assistant

        // Verify each context has its own messages
        Assert.Equal("Message for agent 1", context1.Messages[0].Content);
        Assert.Equal("Another message for agent 1", context1.Messages[2].Content);
        Assert.Equal("Message for agent 2", context2.Messages[0].Content);
    }
}
