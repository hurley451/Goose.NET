using Goose.Core.Models;
using System.Text.Json;
using Xunit;

namespace Goose.Core.Tests.Models;

public class ToolCallTests
{
    [Fact]
    public void ToolCall_WithRequiredProperties_CreatesSuccessfully()
    {
        // Arrange
        var parameters = "{\"path\":\"/test/file.txt\"}";

        // Act
        var toolCall = new ToolCall
        {
            Id = "call_123",
            Name = "read_file",
            Parameters = parameters
        };

        // Assert
        Assert.Equal("call_123", toolCall.Id);
        Assert.Equal("read_file", toolCall.Name);
        Assert.NotNull(toolCall.Parameters);
        Assert.Equal(parameters, toolCall.Parameters);
    }

    [Fact]
    public void ToolCall_ParametersAsJson_CanBeParsed()
    {
        // Arrange
        var json = "{\"arg1\":\"value1\",\"arg2\":42}";

        // Act
        var toolCall = new ToolCall
        {
            Id = "call_456",
            Name = "test_tool",
            Parameters = json
        };

        // Assert - Verify parameters can be parsed as JSON
        Assert.NotNull(toolCall.Parameters);
        using var doc = JsonDocument.Parse(toolCall.Parameters);
        var root = doc.RootElement;
        Assert.Equal("value1", root.GetProperty("arg1").GetString());
        Assert.Equal(42, root.GetProperty("arg2").GetInt32());
    }

    [Fact]
    public void ToolCall_Records_SupportWithSyntax()
    {
        // Arrange
        var original = new ToolCall
        {
            Id = "call_1",
            Name = "original_tool",
            Parameters = "{}"
        };

        // Act
        var modified = original with { Name = "modified_tool" };

        // Assert
        Assert.Equal("original_tool", original.Name);
        Assert.Equal("modified_tool", modified.Name);
        Assert.Equal(original.Id, modified.Id);
    }
}
