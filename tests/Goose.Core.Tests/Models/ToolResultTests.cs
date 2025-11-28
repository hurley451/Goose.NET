using Goose.Core.Models;
using Xunit;

namespace Goose.Core.Tests.Models;

public class ToolResultTests
{
    [Fact]
    public void ToolResult_SuccessfulExecution_CreatesCorrectly()
    {
        // Arrange & Act
        var result = new ToolResult
        {
            ToolCallId = "call_1",
            Success = true,
            Output = "File contents here",
            Duration = TimeSpan.FromMilliseconds(150)
        };

        // Assert
        Assert.Equal("call_1", result.ToolCallId);
        Assert.True(result.Success);
        Assert.Equal("File contents here", result.Output);
        Assert.Null(result.Error);
        Assert.Equal(150, result.Duration.TotalMilliseconds);
    }

    [Fact]
    public void ToolResult_FailedExecution_CreatesCorrectly()
    {
        // Arrange & Act
        var result = new ToolResult
        {
            ToolCallId = "call_2",
            Success = false,
            Error = "File not found",
            Duration = TimeSpan.FromMilliseconds(50)
        };

        // Assert
        Assert.Equal("call_2", result.ToolCallId);
        Assert.False(result.Success);
        Assert.Null(result.Output);
        Assert.Equal("File not found", result.Error);
    }

    [Fact]
    public void ToolResult_WithMetadata_StoresCorrectly()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["fileSize"] = 1024,
            ["encoding"] = "utf-8"
        };

        // Act
        var result = new ToolResult
        {
            ToolCallId = "call_3",
            Success = true,
            Output = "Data",
            Metadata = metadata
        };

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal(1024, result.Metadata["fileSize"]);
        Assert.Equal("utf-8", result.Metadata["encoding"]);
    }

    [Fact]
    public void ToolResult_Records_SupportWithSyntax()
    {
        // Arrange
        var original = new ToolResult
        {
            ToolCallId = "call_1",
            Success = true,
            Duration = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var modified = original with { Duration = TimeSpan.FromMilliseconds(200) };

        // Assert
        Assert.Equal(100, original.Duration.TotalMilliseconds);
        Assert.Equal(200, modified.Duration.TotalMilliseconds);
        Assert.Equal(original.ToolCallId, modified.ToolCallId);
        Assert.Equal(original.Success, modified.Success);
    }
}
