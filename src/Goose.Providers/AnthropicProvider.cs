using System.Net.Http.Json;
using Goose.Core.Abstractions;
using Goose.Core.Exceptions;
using Goose.Core.Models;
using Microsoft.Extensions.Logging;

namespace Goose.Providers;

/// <summary>
/// Implementation of the Anthropic AI provider for Goose.NET
/// </summary>
public class AnthropicProvider : IProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicProvider> _logger;

    /// <summary>
    /// Creates a new Anthropic provider instance
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls (configured with policies via DI)</param>
    /// <param name="logger">Logger instance</param>
    public AnthropicProvider(HttpClient httpClient, ILogger<AnthropicProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the provider name (anthropic)
    /// </summary>
    public string Name => "anthropic";

    /// <summary>
    /// Gets the capabilities of this provider
    /// </summary>
    public ProviderCapabilities Capabilities => new()
    {
        SupportsStreaming = true,
        SupportsTools = true,
        SupportsVision = true,
        MaxTokens = 200_000
    };

    /// <summary>
    /// Generates a completion from the Anthropic API
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
            "Requesting completion from Anthropic with {MessageCount} messages",
            messages.Count);

        // Extract system message if present (Anthropic handles system separately)
        var systemMessage = messages.FirstOrDefault(m => m.Role == MessageRole.System);
        var conversationMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

        // Map Goose messages to Anthropic format
        var anthropicMessages = conversationMessages.Select(msg => MapMessage(msg)).ToList();

        // Build request object
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = options.Model ?? "claude-3-5-sonnet-20241022",
            ["messages"] = anthropicMessages,
            ["max_tokens"] = options.MaxTokens ?? 4096
        };

        // Add optional parameters
        if (systemMessage != null)
        {
            requestBody["system"] = systemMessage.Content;
        }

        if (options.Temperature.HasValue)
        {
            requestBody["temperature"] = options.Temperature.Value;
        }

        // Add tools if provided in options
        if (options.Tools?.Count > 0)
        {
            requestBody["tools"] = options.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = System.Text.Json.JsonSerializer.Deserialize<object>(t.ParametersSchema)
            }).ToList();
        }

        try
        {
            // Make API request to Anthropic (policies applied via HttpClient DI configuration)
            var response = await _httpClient.PostAsJsonAsync(
                "/v1/messages",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Parse the response
            var anthropicResponse = await response.Content.ReadFromJsonAsync<AnthropicApiResponse>(cancellationToken);

            if (anthropicResponse == null)
            {
                throw new ProviderException("Received null response from Anthropic API", "anthropic");
            }

            // Extract content and tool calls from response
            var (content, toolCalls) = ExtractContentAndToolCalls(anthropicResponse);

            return new ProviderResponse
            {
                Content = content,
                Model = anthropicResponse.model ?? "claude-3-5-sonnet-20241022",
                Usage = new ProviderUsage
                {
                    InputTokens = anthropicResponse.usage?.input_tokens ?? 0,
                    OutputTokens = anthropicResponse.usage?.output_tokens ?? 0
                },
                ToolCalls = toolCalls,
                StopReason = anthropicResponse.stop_reason
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Anthropic API HTTP request failed");
            throw new ProviderException($"Anthropic API request failed: {ex.Message}", "anthropic", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Anthropic API");
            throw new ProviderException($"Failed to call Anthropic: {ex.Message}", "anthropic", ex);
        }
    }

    /// <summary>
    /// Maps a Goose message to Anthropic message format
    /// </summary>
    private object MapMessage(Message message)
    {
        // Handle tool result messages
        if (message.Role == MessageRole.User && !string.IsNullOrEmpty(message.ToolCallId))
        {
            return new
            {
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "tool_result",
                        tool_use_id = message.ToolCallId,
                        content = message.Content
                    }
                }
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
    /// Extracts content and tool calls from Anthropic response
    /// </summary>
    private (string content, IReadOnlyList<ToolCall>? toolCalls) ExtractContentAndToolCalls(AnthropicApiResponse response)
    {
        var contentBuilder = new System.Text.StringBuilder();
        var toolCalls = new List<ToolCall>();

        if (response.content != null)
        {
            foreach (var block in response.content)
            {
                if (block.type == "text" && !string.IsNullOrEmpty(block.text))
                {
                    contentBuilder.AppendLine(block.text);
                }
                else if (block.type == "tool_use")
                {
                    toolCalls.Add(new ToolCall
                    {
                        Id = block.id ?? Guid.NewGuid().ToString(),
                        Name = block.name ?? string.Empty,
                        Parameters = System.Text.Json.JsonSerializer.Serialize(block.input ?? new object())
                    });
                }
            }
        }

        return (
            contentBuilder.ToString().TrimEnd(),
            toolCalls.Count > 0 ? toolCalls : null
        );
    }

    /// <summary>
    /// Streams a completion from the Anthropic API
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
            "Streaming completion from Anthropic with {MessageCount} messages",
            messages.Count);

        IAsyncEnumerable<StreamChunk> streamEnumerable;

        try
        {
            streamEnumerable = StreamAsyncInternal(messages, options, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Anthropic API streaming request failed");
            throw new ProviderException($"Anthropic streaming request failed: {ex.Message}", "anthropic", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming from Anthropic API");
            throw new ProviderException($"Failed to stream from Anthropic: {ex.Message}", "anthropic", ex);
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
        // Extract system message if present
        var systemMessage = messages.FirstOrDefault(m => m.Role == MessageRole.System);
        var conversationMessages = messages.Where(m => m.Role != MessageRole.System).ToList();

        // Map Goose messages to Anthropic format
        var anthropicMessages = conversationMessages.Select(msg => MapMessage(msg)).ToList();

        // Build request object
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = options.Model ?? "claude-3-5-sonnet-20241022",
            ["messages"] = anthropicMessages,
            ["max_tokens"] = options.MaxTokens ?? 4096,
            ["stream"] = true // Enable streaming
        };

        if (systemMessage != null)
        {
            requestBody["system"] = systemMessage.Content;
        }

        if (options.Temperature.HasValue)
        {
            requestBody["temperature"] = options.Temperature.Value;
        }

        if (options.Tools?.Count > 0)
        {
            requestBody["tools"] = options.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = System.Text.Json.JsonSerializer.Deserialize<object>(t.ParametersSchema)
            }).ToList();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var contentBuilder = new System.Text.StringBuilder();
        ToolCall? currentToolCall = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var jsonData = line.Substring(6); // Remove "data: " prefix

            if (jsonData == "[DONE]")
            {
                if (contentBuilder.Length > 0)
                {
                    yield return new StreamChunk
                    {
                        Content = contentBuilder.ToString(),
                        IsFinal = true
                    };
                }
                yield break;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            var eventType = root.GetProperty("type").GetString();

            switch (eventType)
            {
                case "content_block_delta":
                    if (root.TryGetProperty("delta", out var delta))
                    {
                        if (delta.TryGetProperty("type", out var deltaType) && deltaType.GetString() == "text_delta")
                        {
                            if (delta.TryGetProperty("text", out var text))
                            {
                                var chunkText = text.GetString() ?? string.Empty;
                                contentBuilder.Append(chunkText);

                                yield return new StreamChunk
                                {
                                    Content = chunkText,
                                    IsFinal = false
                                };
                            }
                        }
                        else if (delta.TryGetProperty("partial_json", out var partialJson))
                        {
                            // Tool call in progress - accumulate JSON
                            if (currentToolCall != null)
                            {
                                // Update tool call with partial data
                                yield return new StreamChunk
                                {
                                    Content = string.Empty,
                                    IsFinal = false,
                                    ToolCall = currentToolCall
                                };
                            }
                        }
                    }
                    break;

                case "content_block_start":
                    if (root.TryGetProperty("content_block", out var contentBlock))
                    {
                        if (contentBlock.TryGetProperty("type", out var blockType) && blockType.GetString() == "tool_use")
                        {
                            var id = contentBlock.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                            var name = contentBlock.GetProperty("name").GetString() ?? string.Empty;

                            currentToolCall = new ToolCall
                            {
                                Id = id,
                                Name = name,
                                Parameters = "{}"
                            };
                        }
                    }
                    break;

                case "content_block_stop":
                    if (currentToolCall != null)
                    {
                        yield return new StreamChunk
                        {
                            Content = string.Empty,
                            IsFinal = false,
                            ToolCall = currentToolCall
                        };
                        currentToolCall = null;
                    }
                    break;

                case "message_stop":
                    if (contentBuilder.Length > 0 || currentToolCall != null)
                    {
                        yield return new StreamChunk
                        {
                            Content = contentBuilder.ToString(),
                            IsFinal = true,
                            ToolCall = currentToolCall
                        };
                    }
                    yield break;
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
/// Response model from Anthropic API
/// </summary>
public class AnthropicApiResponse
{
    public string? id { get; set; }
    public string? type { get; set; }
    public string? role { get; set; }
    public List<AnthropicContentBlock>? content { get; set; }
    public string? model { get; set; }
    public string? stop_reason { get; set; }
    public string? stop_sequence { get; set; }
    public AnthropicUsage? usage { get; set; }
}

/// <summary>
/// Content block from Anthropic API response
/// </summary>
public class AnthropicContentBlock
{
    public string? type { get; set; }
    public string? text { get; set; }
    public string? id { get; set; }
    public string? name { get; set; }
    public object? input { get; set; }
}

/// <summary>
/// Usage statistics from Anthropic API
/// </summary>
public class AnthropicUsage
{
    public int? input_tokens { get; set; }
    public int? output_tokens { get; set; }
}
