using System.Net.Http.Json;
using Goose.Core.Abstractions;
using Goose.Core.Exceptions;
using Goose.Core.Models;
using Microsoft.Extensions.Logging;

namespace Goose.Providers;

/// <summary>
/// Implementation of the OpenAI AI provider for Goose.NET
/// </summary>
public class OpenAIProvider : IProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIProvider> _logger;

    /// <summary>
    /// Creates a new OpenAI provider instance
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls (configured with policies via DI)</param>
    /// <param name="logger">Logger instance</param>
    public OpenAIProvider(HttpClient httpClient, ILogger<OpenAIProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the provider name (openai)
    /// </summary>
    public string Name => "openai";

    /// <summary>
    /// Gets the capabilities of this provider
    /// </summary>
    public ProviderCapabilities Capabilities => new()
    {
        SupportsStreaming = true,
        SupportsTools = true,
        SupportsVision = false,
        MaxTokens = 100_000
    };

    /// <summary>
    /// Generates a completion from the OpenAI API
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The provider's response</returns>
    public async Task<ProviderResponse> GenerateAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting completion from OpenAI with {MessageCount} messages",
            messages.Count);

        // Map Goose messages to OpenAI format
        var openaiMessages = messages.Select(msg => MapMessage(msg)).ToList();

        // Build request object
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = options.Model ?? "gpt-4-turbo-preview",
            ["messages"] = openaiMessages
        };

        // Add optional parameters
        if (options.MaxTokens.HasValue)
        {
            requestBody["max_tokens"] = options.MaxTokens.Value;
        }

        if (options.Temperature.HasValue)
        {
            requestBody["temperature"] = options.Temperature.Value;
        }

        if (options.TopP.HasValue)
        {
            requestBody["top_p"] = options.TopP.Value;
        }

        // Add tools if provided in options
        if (options.Tools?.Count > 0)
        {
            requestBody["tools"] = options.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = System.Text.Json.JsonSerializer.Deserialize<object>(t.ParametersSchema)
                }
            }).ToList();
            requestBody["tool_choice"] = "auto"; // Let the model decide when to use tools
        }

        try
        {
            // Make API request to OpenAI (policies applied via HttpClient DI configuration)
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/chat/completions",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Parse the response
            var openaiResponse = await response.Content.ReadFromJsonAsync<OpenAIApiResponse>(cancellationToken);

            if (openaiResponse == null || openaiResponse.choices == null || openaiResponse.choices.Count == 0)
            {
                throw new ProviderException("Received empty response from OpenAI API", "openai");
            }

            var choice = openaiResponse.choices[0];
            var content = choice.message?.content ?? string.Empty;

            // Extract tool calls if present
            var toolCalls = choice.message?.tool_calls?.Select(tc => new ToolCall
            {
                Id = tc.id ?? Guid.NewGuid().ToString(),
                Name = tc.function?.name ?? string.Empty,
                Parameters = tc.function?.arguments ?? "{}"
            }).ToList();

            return new ProviderResponse
            {
                Content = content,
                Model = openaiResponse.model ?? "gpt-4-turbo-preview",
                Usage = new ProviderUsage
                {
                    InputTokens = openaiResponse.usage?.prompt_tokens ?? 0,
                    OutputTokens = openaiResponse.usage?.completion_tokens ?? 0
                },
                ToolCalls = toolCalls?.Count > 0 ? toolCalls : null,
                StopReason = choice.finish_reason
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenAI API HTTP request failed");
            throw new ProviderException($"OpenAI API request failed: {ex.Message}", "openai", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            throw new ProviderException($"Failed to call OpenAI: {ex.Message}", "openai", ex);
        }
    }

    /// <summary>
    /// Maps a Goose message to OpenAI message format
    /// </summary>
    private object MapMessage(Message message)
    {
        // Handle tool result messages
        if (message.Role == MessageRole.User && !string.IsNullOrEmpty(message.ToolCallId))
        {
            return new
            {
                role = "tool",
                tool_call_id = message.ToolCallId,
                content = message.Content
            };
        }

        // Handle assistant messages with tool calls
        if (message.Role == MessageRole.Assistant && message.ToolCalls?.Count > 0)
        {
            return new
            {
                role = "assistant",
                content = message.Content,
                tool_calls = message.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.Parameters
                    }
                }).ToList()
            };
        }

        // Handle regular messages
        return new
        {
            role = message.Role.ToString().ToLower(),
            content = message.Content
        };
    }

    /// <summary>
    /// Streams a completion from the OpenAI API
    /// </summary>
    /// <param name="messages">The conversation messages</param>
    /// <param name="options">Provider-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of response chunks</returns>
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Streaming completion from OpenAI with {MessageCount} messages",
            messages.Count);

        IAsyncEnumerable<StreamChunk> streamEnumerable;

        try
        {
            streamEnumerable = StreamAsyncInternal(messages, options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenAI API streaming request failed");
            throw new ProviderException($"OpenAI streaming request failed: {ex.Message}", "openai", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming from OpenAI API");
            throw new ProviderException($"Failed to stream from OpenAI: {ex.Message}", "openai", ex);
        }

        await foreach (var chunk in streamEnumerable.WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Internal streaming implementation
    /// </summary>
    private async IAsyncEnumerable<StreamChunk> StreamAsyncInternal(
        IReadOnlyList<Message> messages,
        ProviderOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Map Goose messages to OpenAI format
        var openaiMessages = messages.Select(msg => MapMessage(msg)).ToList();

        // Build request object
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = options.Model ?? "gpt-4-turbo-preview",
            ["messages"] = openaiMessages,
            ["stream"] = true // Enable streaming
        };

        if (options.MaxTokens.HasValue)
        {
            requestBody["max_tokens"] = options.MaxTokens.Value;
        }

        if (options.Temperature.HasValue)
        {
            requestBody["temperature"] = options.Temperature.Value;
        }

        if (options.TopP.HasValue)
        {
            requestBody["top_p"] = options.TopP.Value;
        }

        if (options.Tools?.Count > 0)
        {
            requestBody["tools"] = options.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = System.Text.Json.JsonSerializer.Deserialize<object>(t.ParametersSchema)
                }
            }).ToList();
            requestBody["tool_choice"] = "auto";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var contentBuilder = new System.Text.StringBuilder();
        var toolCallsDict = new Dictionary<int, (string id, string name, System.Text.StringBuilder arguments)>();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var jsonData = line.Substring(6); // Remove "data: " prefix

            if (jsonData == "[DONE]")
            {
                // Send final chunk with any accumulated content or tool calls
                if (contentBuilder.Length > 0 || toolCallsDict.Count > 0)
                {
                    ToolCall? finalToolCall = null;
                    if (toolCallsDict.Count > 0)
                    {
                        var firstTool = toolCallsDict.Values.First();
                        finalToolCall = new ToolCall
                        {
                            Id = firstTool.id,
                            Name = firstTool.name,
                            Parameters = firstTool.arguments.ToString()
                        };
                    }

                    yield return new StreamChunk
                    {
                        Content = contentBuilder.ToString(),
                        IsFinal = true,
                        ToolCall = finalToolCall
                    };
                }
                yield break;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                continue;

            var choice = choices[0];

            if (!choice.TryGetProperty("delta", out var delta))
                continue;

            // Handle content delta
            if (delta.TryGetProperty("content", out var content))
            {
                var contentText = content.GetString();
                if (!string.IsNullOrEmpty(contentText))
                {
                    contentBuilder.Append(contentText);

                    yield return new StreamChunk
                    {
                        Content = contentText,
                        IsFinal = false
                    };
                }
            }

            // Handle tool calls delta
            if (delta.TryGetProperty("tool_calls", out var toolCalls))
            {
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var index = toolCall.GetProperty("index").GetInt32();

                    if (!toolCallsDict.ContainsKey(index))
                    {
                        var id = toolCall.TryGetProperty("id", out var idProp)
                            ? idProp.GetString() ?? Guid.NewGuid().ToString()
                            : Guid.NewGuid().ToString();

                        var name = string.Empty;
                        if (toolCall.TryGetProperty("function", out var func) &&
                            func.TryGetProperty("name", out var nameProp))
                        {
                            name = nameProp.GetString() ?? string.Empty;
                        }

                        toolCallsDict[index] = (id, name, new System.Text.StringBuilder());
                    }

                    // Append arguments if present
                    if (toolCall.TryGetProperty("function", out var function) &&
                        function.TryGetProperty("arguments", out var args))
                    {
                        var argsText = args.GetString();
                        if (!string.IsNullOrEmpty(argsText))
                        {
                            var current = toolCallsDict[index];
                            current.arguments.Append(argsText);
                            toolCallsDict[index] = current;
                        }
                    }
                }
            }

            // Check for finish reason
            if (choice.TryGetProperty("finish_reason", out var finishReason))
            {
                var reason = finishReason.GetString();
                if (reason == "stop" || reason == "tool_calls")
                {
                    ToolCall? finalToolCall = null;
                    if (toolCallsDict.Count > 0)
                    {
                        var firstTool = toolCallsDict.Values.First();
                        finalToolCall = new ToolCall
                        {
                            Id = firstTool.id,
                            Name = firstTool.name,
                            Parameters = firstTool.arguments.ToString()
                        };
                    }

                    yield return new StreamChunk
                    {
                        Content = contentBuilder.ToString(),
                        IsFinal = true,
                        ToolCall = finalToolCall
                    };
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Gets the tool definitions supported by this provider
    /// </summary>
    public IReadOnlyList<ToolDefinition> GetToolDefinitions()
    {
        // Return tool definitions that this provider supports
        return new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "read_file",
                Description = "Read the contents of a file",
                ParametersSchema = "{ \"type\": \"object\", \"properties\": { \"path\": { \"type\": \"string\" } }, \"required\": [\"path\"] }"
            }
        };
    }
}

/// <summary>
/// Response model from OpenAI API
/// </summary>
public class OpenAIApiResponse
{
    public string? id { get; set; }
    public string? @object { get; set; }
    public long? created { get; set; }
    public string? model { get; set; }
    public List<OpenAIChoice>? choices { get; set; }
    public OpenAIUsage? usage { get; set; }
}

/// <summary>
/// Choice from OpenAI response
/// </summary>
public class OpenAIChoice
{
    public int? index { get; set; }
    public OpenAIMessage? message { get; set; }
    public string? finish_reason { get; set; }
}

/// <summary>
/// Message from OpenAI response
/// </summary>
public class OpenAIMessage
{
    public string? role { get; set; }
    public string? content { get; set; }
    public List<OpenAIToolCall>? tool_calls { get; set; }
}

/// <summary>
/// Tool call from OpenAI response
/// </summary>
public class OpenAIToolCall
{
    public string? id { get; set; }
    public string? type { get; set; }
    public OpenAIFunction? function { get; set; }
}

/// <summary>
/// Function details from OpenAI tool call
/// </summary>
public class OpenAIFunction
{
    public string? name { get; set; }
    public string? arguments { get; set; }
}

/// <summary>
/// Usage statistics from OpenAI API
/// </summary>
public class OpenAIUsage
{
    public int? prompt_tokens { get; set; }
    public int? completion_tokens { get; set; }
    public int? total_tokens { get; set; }
}
