using Goose.Core.Abstractions;
using Goose.Core.Exceptions;
using Goose.Core.Models;
using Goose.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Goose.Core.Tests.Integration;

/// <summary>
/// Integration tests for AI provider implementations
/// </summary>
public class ProviderIntegrationTests
{
    private readonly Mock<ILogger<AnthropicProvider>> _anthropicLogger;
    private readonly Mock<ILogger<OpenAIProvider>> _openAILogger;

    public ProviderIntegrationTests()
    {
        _anthropicLogger = new Mock<ILogger<AnthropicProvider>>();
        _openAILogger = new Mock<ILogger<OpenAIProvider>>();
    }

    [Fact]
    public async Task AnthropicProvider_BasicGeneration_ReturnsValidResponse()
    {
        // Arrange
        var mockResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new[]
            {
                new { type = "text", text = "Hello! How can I help you?" }
            },
            model = "claude-3-5-sonnet-20241022",
            stop_reason = "end_turn",
            usage = new
            {
                input_tokens = 10,
                output_tokens = 8
            }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var provider = new AnthropicProvider(httpClient, _anthropicLogger.Object);

        var messages = new List<Message>
        {
            new Message
            {
                Role = MessageRole.User,
                Content = "Hello"
            }
        };

        var options = new ProviderOptions
        {
            Model = "claude-3-5-sonnet-20241022",
            MaxTokens = 1024
        };

        // Act
        var response = await provider.GenerateAsync(messages, options);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Hello! How can I help you?", response.Content);
        Assert.Equal("claude-3-5-sonnet-20241022", response.Model);
        Assert.Equal(10, response.Usage?.InputTokens);
        Assert.Equal(8, response.Usage?.OutputTokens);
        Assert.Equal("end_turn", response.StopReason);
    }

    [Fact]
    public async Task AnthropicProvider_WithSystemMessage_ExtractsSystemParameter()
    {
        // Arrange
        var capturedRequest = string.Empty;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (request, token) =>
            {
                capturedRequest = await request.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    id = "msg_123",
                    type = "message",
                    role = "assistant",
                    content = new[] { new { type = "text", text = "Response" } },
                    model = "claude-3-5-sonnet-20241022",
                    stop_reason = "end_turn",
                    usage = new { input_tokens = 10, output_tokens = 5 }
                }))
            });

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };
        var provider = new AnthropicProvider(httpClient, _anthropicLogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.System, Content = "You are a helpful assistant" },
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions { MaxTokens = 1024 };

        // Act
        var response = await provider.GenerateAsync(messages, options);

        // Assert
        Assert.Contains("\"system\":\"You are a helpful assistant\"", capturedRequest);
        Assert.DoesNotContain("\"role\":\"system\"", capturedRequest);
    }

    [Fact]
    public async Task AnthropicProvider_WithToolCalls_ReturnsToolCallsInResponse()
    {
        // Arrange
        var mockResponse = new
        {
            id = "msg_123",
            type = "message",
            role = "assistant",
            content = new object[]
            {
                new { type = "text", text = "I'll read that file for you." },
                new
                {
                    type = "tool_use",
                    id = "toolu_123",
                    name = "read_file",
                    input = new { path = "/home/user/file.txt" }
                }
            },
            model = "claude-3-5-sonnet-20241022",
            stop_reason = "tool_use",
            usage = new { input_tokens = 50, output_tokens = 20 }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var provider = new AnthropicProvider(httpClient, _anthropicLogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Read the file /home/user/file.txt" }
        };

        var options = new ProviderOptions
        {
            MaxTokens = 1024,
            Tools = new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Read a file",
                    ParametersSchema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}"
                }
            }
        };

        // Act
        var response = await provider.GenerateAsync(messages, options);

        // Assert
        Assert.NotNull(response);
        Assert.Contains("I'll read that file for you.", response.Content);
        Assert.NotNull(response.ToolCalls);
        Assert.Single(response.ToolCalls);
        Assert.Equal("toolu_123", response.ToolCalls[0].Id);
        Assert.Equal("read_file", response.ToolCalls[0].Name);
        Assert.Contains("file.txt", response.ToolCalls[0].Parameters);
        Assert.Equal("tool_use", response.StopReason);
    }

    [Fact]
    public async Task OpenAIProvider_BasicGeneration_ReturnsValidResponse()
    {
        // Arrange
        var mockResponse = new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4-turbo-preview",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "Hello! How can I assist you today?"
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 12,
                completion_tokens = 9,
                total_tokens = 21
            }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var provider = new OpenAIProvider(httpClient, _openAILogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions
        {
            Model = "gpt-4-turbo-preview",
            MaxTokens = 1024
        };

        // Act
        var response = await provider.GenerateAsync(messages, options);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Hello! How can I assist you today?", response.Content);
        Assert.Equal("gpt-4-turbo-preview", response.Model);
        Assert.Equal(12, response.Usage?.InputTokens);
        Assert.Equal(9, response.Usage?.OutputTokens);
        Assert.Equal("stop", response.StopReason);
    }

    [Fact]
    public async Task OpenAIProvider_WithToolCalls_ReturnsToolCallsInResponse()
    {
        // Arrange
        var mockResponse = new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4-turbo-preview",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = (string?)null,
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "call_123",
                                type = "function",
                                function = new
                                {
                                    name = "read_file",
                                    arguments = "{\"path\":\"/home/user/file.txt\"}"
                                }
                            }
                        }
                    },
                    finish_reason = "tool_calls"
                }
            },
            usage = new
            {
                prompt_tokens = 50,
                completion_tokens = 20,
                total_tokens = 70
            }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var provider = new OpenAIProvider(httpClient, _openAILogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Read the file /home/user/file.txt" }
        };

        var options = new ProviderOptions
        {
            MaxTokens = 1024,
            Tools = new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Read a file",
                    ParametersSchema = "{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}"
                }
            }
        };

        // Act
        var response = await provider.GenerateAsync(messages, options);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.ToolCalls);
        Assert.Single(response.ToolCalls);
        Assert.Equal("call_123", response.ToolCalls[0].Id);
        Assert.Equal("read_file", response.ToolCalls[0].Name);
        Assert.Contains("file.txt", response.ToolCalls[0].Parameters);
        Assert.Equal("tool_calls", response.StopReason);
    }

    [Fact]
    public async Task AnthropicProvider_ApiError_ThrowsProviderException()
    {
        // Arrange
        var errorResponse = new
        {
            type = "error",
            error = new
            {
                type = "invalid_request_error",
                message = "Invalid API key"
            }
        };

        var httpClient = CreateMockHttpClient(errorResponse, HttpStatusCode.Unauthorized);
        var provider = new AnthropicProvider(httpClient, _anthropicLogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions { MaxTokens = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProviderException>(
            async () => await provider.GenerateAsync(messages, options));

        Assert.Equal("anthropic", exception.ProviderName);
        Assert.Contains("API request failed", exception.Message);
    }

    [Fact]
    public async Task OpenAIProvider_ApiError_ThrowsProviderException()
    {
        // Arrange
        var errorResponse = new
        {
            error = new
            {
                message = "Invalid API key",
                type = "invalid_request_error",
                code = "invalid_api_key"
            }
        };

        var httpClient = CreateMockHttpClient(errorResponse, HttpStatusCode.Unauthorized);
        var provider = new OpenAIProvider(httpClient, _openAILogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions { MaxTokens = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProviderException>(
            async () => await provider.GenerateAsync(messages, options));

        Assert.Equal("openai", exception.ProviderName);
        Assert.Contains("API request failed", exception.Message);
    }

    [Fact]
    public async Task AnthropicProvider_EmptyResponse_ThrowsProviderException()
    {
        // Arrange
        var httpClient = CreateMockHttpClient<object?>(null, HttpStatusCode.OK);
        var provider = new AnthropicProvider(httpClient, _anthropicLogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions { MaxTokens = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProviderException>(
            async () => await provider.GenerateAsync(messages, options));

        Assert.Contains("null response", exception.Message);
    }

    [Fact]
    public async Task OpenAIProvider_EmptyChoices_ThrowsProviderException()
    {
        // Arrange
        var mockResponse = new
        {
            id = "chatcmpl-123",
            model = "gpt-4-turbo-preview",
            choices = Array.Empty<object>(),
            usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 }
        };

        var httpClient = CreateMockHttpClient(mockResponse, HttpStatusCode.OK);
        var provider = new OpenAIProvider(httpClient, _openAILogger.Object);

        var messages = new List<Message>
        {
            new Message { Role = MessageRole.User, Content = "Hello" }
        };

        var options = new ProviderOptions { MaxTokens = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ProviderException>(
            async () => await provider.GenerateAsync(messages, options));

        Assert.Contains("empty response", exception.Message);
    }

    /// <summary>
    /// Creates a mock HTTP client that returns the specified response
    /// </summary>
    private HttpClient CreateMockHttpClient<T>(T responseObject, HttpStatusCode statusCode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseObject))
            });

        return new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
    }
}
