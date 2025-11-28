using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Microsoft.Extensions.Logging;

namespace Goose.Core.Services;

/// <summary>
/// Registry for managing available tools
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;
    private readonly object _lock = new();

    // Tool name validation pattern: alphanumeric, underscores, hyphens only
    private static readonly Regex ToolNamePattern = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    
    /// <summary>
    /// Creates a new ToolRegistry instance
    /// </summary>
    /// <param name="tools">Enumerable of tools to register initially</param>
    /// <param name="logger">Logger instance</param>
    public ToolRegistry(
        IEnumerable<ITool> tools,
        ILogger<ToolRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }
    
    /// <summary>
    /// Registers a new tool
    /// </summary>
    /// <param name="tool">The tool to register</param>
    /// <exception cref="ArgumentNullException">Thrown when tool is null</exception>
    /// <exception cref="ArgumentException">Thrown when tool name is invalid</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // Validate tool name format
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new ArgumentException("Tool name cannot be null or whitespace", nameof(tool));
        }

        if (!ToolNamePattern.IsMatch(tool.Name))
        {
            throw new ArgumentException(
                $"Tool name '{tool.Name}' contains invalid characters. Only alphanumeric, underscores, and hyphens are allowed.",
                nameof(tool));
        }

        // Validate required properties
        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            _logger.LogWarning("Tool {ToolName} has no description", tool.Name);
        }

        if (string.IsNullOrWhiteSpace(tool.ParameterSchema))
        {
            _logger.LogWarning("Tool {ToolName} has no parameter schema", tool.Name);
        }
        else
        {
            // Validate parameter schema is valid JSON
            try
            {
                JsonDocument.Parse(tool.ParameterSchema);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException(
                    $"Tool '{tool.Name}' has invalid JSON parameter schema: {ex.Message}",
                    nameof(tool),
                    ex);
            }
        }

        lock (_lock)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                _logger.LogWarning(
                    "Tool {ToolName} is already registered, replacing",
                    tool.Name);
            }

            _tools[tool.Name] = tool;
            _logger.LogDebug("Registered tool: {ToolName}", tool.Name);
        }
    }

    /// <summary>
    /// Unregisters a tool by name
    /// </summary>
    /// <param name="toolName">The name of the tool to unregister</param>
    /// <returns>True if the tool was found and removed, false otherwise</returns>
    public bool Unregister(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        lock (_lock)
        {
            if (_tools.Remove(toolName))
            {
                _logger.LogDebug("Unregistered tool: {ToolName}", toolName);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a tool is registered
    /// </summary>
    /// <param name="toolName">The name of the tool to check</param>
    /// <returns>True if the tool is registered, false otherwise</returns>
    public bool IsToolRegistered(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        lock (_lock)
        {
            return _tools.ContainsKey(toolName);
        }
    }

    /// <summary>
    /// Gets the count of registered tools
    /// </summary>
    /// <returns>The number of registered tools</returns>
    public int GetToolCount()
    {
        lock (_lock)
        {
            return _tools.Count;
        }
    }
    
    /// <summary>
    /// Gets a registered tool by name
    /// </summary>
    /// <param name="name">The name of the tool to get</param>
    /// <returns>The tool if found, otherwise null</returns>
    public ITool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        lock (_lock)
        {
            _tools.TryGetValue(name, out var tool);
            return tool;
        }
    }

    /// <summary>
    /// Gets all registered tools
    /// </summary>
    /// <returns>List of all available tools</returns>
    public IReadOnlyList<ITool> GetAllTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Attempts to get a tool by name
    /// </summary>
    /// <param name="name">The name of the tool to get</param>
    /// <param name="tool">When this method returns true, contains the tool; otherwise null</param>
    /// <returns>True if a tool with the specified name was found, otherwise false</returns>
    public bool TryGetTool(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ITool? tool)
    {
        tool = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        lock (_lock)
        {
            return _tools.TryGetValue(name, out tool);
        }
    }
    
    /// <summary>
    /// Loads tools from assemblies in the specified directory
    /// </summary>
    /// <param name="directoryPath">The path to the directory containing assemblies</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tools successfully loaded</returns>
    public async Task<int> LoadToolsFromDirectoryAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading tools from directory: {DirectoryPath}", directoryPath);
        
        int loadedCount = 0;
        
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
                return 0;
            }
            
            // Find all DLL files in the directory
            var dllFiles = Directory.GetFiles(directoryPath, "*.dll");
            
            foreach (var dllFile in dllFiles)
            {
                try
                {
                    _logger.LogDebug("Loading tools from assembly: {AssemblyPath}", dllFile);
                    
                    var assembly = Assembly.LoadFrom(dllFile);
                    await LoadToolsFromAssemblyAsync(assembly, cancellationToken);
                    loadedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tools from assembly: {AssemblyPath}", dllFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tools from directory: {DirectoryPath}", directoryPath);
        }
        
        _logger.LogInformation("Loaded {Count} tool assemblies from directory", loadedCount);
        return loadedCount;
    }
    
    /// <summary>
    /// Loads tools from a specific assembly
    /// </summary>
    /// <param name="assembly">The assembly to load tools from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tools successfully loaded from the assembly</returns>
    public async Task<int> LoadToolsFromAssemblyAsync(
        Assembly assembly,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading tools from assembly: {AssemblyName}", assembly.GetName().Name);
        
        int loadedCount = 0;
        
        try
        {
            // Find all types that implement ITool
            var toolTypes = assembly.GetTypes()
                .Where(t => typeof(ITool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            
            foreach (var toolType in toolTypes)
            {
                try
                {
                    // Create instance of the tool (assuming parameterless constructor)
                    var tool = (ITool)Activator.CreateInstance(toolType)!;
                    
                    Register(tool);
                    loadedCount++;
                    
                    _logger.LogDebug("Loaded tool from assembly: {ToolType}", tool.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create instance of tool: {ToolType}", toolType.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tools from assembly: {AssemblyName}", assembly.GetName().Name);
        }
        
        _logger.LogInformation("Loaded {Count} tools from assembly", loadedCount);
        return loadedCount;
    }
    
    /// <summary>
    /// Gets tool metadata including documentation and parameters
    /// </summary>
    /// <param name="toolName">The tool name to get metadata for</param>
    /// <returns>Tool metadata if found, otherwise null</returns>
    public ToolMetadata? GetToolMetadata(string toolName)
    {
        if (TryGetTool(toolName, out var tool))
        {
            var parametersSchema = ParseParameterSchema(tool.ParameterSchema);

            return new ToolMetadata
            {
                Name = tool.Name,
                Description = tool.Description ?? "No description available",
                ParametersSchema = parametersSchema,
                IsAsync = true // Assuming all tools support async operations
            };
        }

        return null;
    }

    /// <summary>
    /// Parses a JSON parameter schema string into a dictionary
    /// </summary>
    /// <param name="schemaJson">The JSON schema string</param>
    /// <returns>Dictionary representation of the schema</returns>
    private Dictionary<string, object> ParseParameterSchema(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(schemaJson);
            return JsonElementToDictionary(jsonElement);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse parameter schema: {Schema}", schemaJson);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Converts a JsonElement to a Dictionary recursively
    /// </summary>
    private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object>();

        if (element.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonElement(property.Value);
        }

        return result;
    }

    /// <summary>
    /// Converts a JsonElement to its appropriate .NET type
    /// </summary>
    private object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()
        };
    }
    
    /// <summary>
    /// Validates a tool configuration
    /// </summary>
    /// <param name="toolName">The tool name to validate</param>
    /// <returns>Validation result with success status and error messages if validation fails</returns>
    public ValidationResult ValidateTool(string toolName)
    {
        // Validate tool name
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Tool name cannot be null or whitespace"
            };
        }

        if (!ToolNamePattern.IsMatch(toolName))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool name '{toolName}' contains invalid characters. Only alphanumeric, underscores, and hyphens are allowed."
            };
        }

        // Check if tool exists
        if (!TryGetTool(toolName, out var tool))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool '{toolName}' not found in registry"
            };
        }

        // Validate tool properties
        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool '{toolName}' has no description"
            };
        }

        if (string.IsNullOrWhiteSpace(tool.ParameterSchema))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool '{toolName}' has no parameter schema"
            };
        }

        // Validate parameter schema is valid JSON
        try
        {
            JsonDocument.Parse(tool.ParameterSchema);
        }
        catch (JsonException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool '{toolName}' has invalid JSON parameter schema: {ex.Message}"
            };
        }

        return new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Validates tool parameters against the tool's schema
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="parameters">The parameters JSON string to validate</param>
    /// <param name="context">The execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    public async Task<ValidationResult> ValidateToolParametersAsync(
        string toolName,
        string parameters,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        // First validate the tool itself
        var toolValidation = ValidateTool(toolName);
        if (!toolValidation.IsValid)
        {
            return toolValidation;
        }

        // Get the tool
        if (!TryGetTool(toolName, out var tool))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Tool '{toolName}' not found"
            };
        }

        // Validate parameters JSON is valid
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Parameters cannot be null or whitespace"
            };
        }

        try
        {
            JsonDocument.Parse(parameters);
        }
        catch (JsonException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid JSON parameters: {ex.Message}"
            };
        }

        // Delegate to the tool's own validation logic
        try
        {
            return await tool.ValidateAsync(parameters, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating parameters for tool {ToolName}", toolName);
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Validation error: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Creates a plugin manifest for the tool registry
    /// </summary>
    /// <returns>Plugin manifest describing the current state of tools</returns>
    public PluginManifest CreatePluginManifest()
    {
        lock (_lock)
        {
            var manifest = new PluginManifest
            {
                Tools = _tools.Values.Select(t => new ToolInfo
                {
                    Name = t.Name,
                    Description = t.Description ?? "No description",
                    Version = "1.0.0", // In a real implementation, this would come from assembly metadata
                    IsAsync = true,
                    ParametersSchema = ParseParameterSchema(t.ParameterSchema)
                }).ToList(),

                Metadata = new Dictionary<string, string>
                {
                    ["TotalTools"] = _tools.Count.ToString(),
                    ["LastUpdated"] = DateTime.UtcNow.ToString("o")
                }
            };

            return manifest;
        }
    }
}

/// <summary>
/// Metadata for a tool including documentation and parameter schema
/// </summary>
public class ToolMetadata
{
    /// <summary>
    /// The tool name
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The tool description
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// The parameter schema for the tool
    /// </summary>
    public required Dictionary<string, object> ParametersSchema { get; set; }
    
    /// <summary>
    /// Whether the tool supports async operations
    /// </summary>
    public bool IsAsync { get; set; } = true;
}

/// <summary>
/// Manifest describing plugin information
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// List of available tools in the plugin system
    /// </summary>
    public required List<ToolInfo> Tools { get; set; }
    
    /// <summary>
    /// Metadata about the plugin manifest
    /// </summary>
    public required Dictionary<string, string> Metadata { get; set; }
}

/// <summary>
/// Information about a specific tool in the plugin system
/// </summary>
public class ToolInfo
{
    /// <summary>
    /// The tool name
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The tool description
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// The tool version
    /// </summary>
    public required string Version { get; set; }
    
    /// <summary>
    /// Whether the tool supports async operations
    /// </summary>
    public bool IsAsync { get; set; } = true;
    
    /// <summary>
    /// The parameter schema for the tool
    /// </summary>
    public required Dictionary<string, object> ParametersSchema { get; set; }
}
