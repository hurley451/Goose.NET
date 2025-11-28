using Goose.Core.Models;
using Xunit;

namespace Goose.Core.Tests.Models;

public class ProviderResponseTests
{
    [Fact]
    public void ProviderResponse_BasicResponse_CreatesCorrectly()
    {
        // Arrange & Act
        var response = new ProviderResponse
        {
            Content = "Hello, how can I help you?",
            Model = "claude-3-5-sonnet",
            Usage = new ProviderUsage
            {
                InputTokens = 10,
                OutputTokens = 20
            }
        };

        // Assert
        Assert.Equal("Hello, how can I help you?", response.Content);
        Assert.Equal("claude-3-5-sonnet", response.Model);
        Assert.NotNull(response.Usage);
        Assert.Equal(10, response.Usage.InputTokens);
        Assert.Equal(20, response.Usage.OutputTokens);
        Assert.Null(response.ToolCalls);
        Assert.Null(response.StopReason);
    }

    [Fact]
    public void ProviderResponse_WithToolCalls_CreatesCorrectly()
    {
        // Arrange
        var toolCalls = new List<ToolCall>
        {
            new ToolCall
            {
                Id = "call_1",
                Name = "read_file",
                Parameters = "{\"path\":\"/test.txt\"}"
            }
        };

        // Act
        var response = new ProviderResponse
        {
            Content = "I'll read that file for you",
            Model = "gpt-4",
            ToolCalls = toolCalls,
            StopReason = "tool_calls"
        };

        // Assert
        Assert.NotNull(response.ToolCalls);
        Assert.Single(response.ToolCalls);
        Assert.Equal("call_1", response.ToolCalls[0].Id);
        Assert.Equal("tool_calls", response.StopReason);
    }

    [Fact]
    public void ProviderUsage_CalculatesTotalTokens_Automatically()
    {
        // Arrange & Act
        var usage = new ProviderUsage
        {
            InputTokens = 100,
            OutputTokens = 50
        };

        // Assert - TotalTokens is calculated automatically
        Assert.Equal(100, usage.InputTokens);
        Assert.Equal(50, usage.OutputTokens);
        Assert.Equal(150, usage.TotalTokens);
    }

    [Fact]
    public void ProviderUsage_TotalTokens_UpdatesWhenInputOutputChange()
    {
        // Arrange & Act
        var usage1 = new ProviderUsage
        {
            InputTokens = 75,
            OutputTokens = 25
        };

        var usage2 = usage1 with { OutputTokens = 30 };

        // Assert - TotalTokens recalculates with record syntax
        Assert.Equal(100, usage1.TotalTokens);
        Assert.Equal(105, usage2.TotalTokens);
    }
}
