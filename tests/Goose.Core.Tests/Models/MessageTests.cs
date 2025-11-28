using Goose.Core.Models;
using Xunit;

namespace Goose.Core.Tests.Models;

public class MessageTests
{
    [Fact]
    public void Message_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = MessageRole.User,
            Content = "Hello, world!"
        };

        // Assert
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Equal("Hello, world!", message.Content);
        Assert.NotEqual(default, message.Timestamp);
        Assert.Null(message.ToolCalls);
        Assert.Null(message.ToolCallId);
    }

    [Fact]
    public void Message_WithToolCalls_StoresCorrectly()
    {
        // Arrange
        var toolCalls = new List<ToolCall>
        {
            new ToolCall
            {
                Id = "call_1",
                Name = "test_tool",
                Parameters = "{\"arg\":\"value\"}"
            }
        };

        // Act
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Content = "I'll use a tool",
            ToolCalls = toolCalls
        };

        // Assert
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.NotNull(message.ToolCalls);
        Assert.Single(message.ToolCalls);
        Assert.Equal("call_1", message.ToolCalls[0].Id);
    }

    [Fact]
    public void Message_WithToolCallId_StoresCorrectly()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = MessageRole.Tool,
            Content = "Tool result",
            ToolCallId = "call_1"
        };

        // Assert
        Assert.Equal(MessageRole.Tool, message.Role);
        Assert.Equal("call_1", message.ToolCallId);
    }

    [Fact]
    public void Message_TimestampDefaults_ToCurrentTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var message = new Message
        {
            Role = MessageRole.System,
            Content = "System message"
        };

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(message.Timestamp >= beforeCreation);
        Assert.True(message.Timestamp <= afterCreation);
    }

    [Fact]
    public void Message_Records_SupportWithSyntax()
    {
        // Arrange
        var original = new Message
        {
            Role = MessageRole.User,
            Content = "Original content"
        };

        // Act
        var modified = original with { Content = "Modified content" };

        // Assert
        Assert.Equal("Original content", original.Content);
        Assert.Equal("Modified content", modified.Content);
        Assert.Equal(original.Role, modified.Role);
        Assert.Equal(original.Timestamp, modified.Timestamp);
    }

    [Fact]
    public void MessageRole_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)MessageRole.System);
        Assert.Equal(1, (int)MessageRole.User);
        Assert.Equal(2, (int)MessageRole.Assistant);
        Assert.Equal(3, (int)MessageRole.Tool);
    }
}
