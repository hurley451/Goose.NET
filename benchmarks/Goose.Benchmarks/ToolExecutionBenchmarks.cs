using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Goose.Core.Abstractions;
using Goose.Core.Models;
using Goose.Core.Services;
using System.Text.Json;

namespace Goose.Benchmarks;

/// <summary>
/// Benchmarks for tool execution performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ToolExecutionBenchmarks
{
    private ToolRegistry _toolRegistry = null!;
    private ITool _simpleTool = null!;
    private ITool _complexTool = null!;
    private ToolContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        _toolRegistry = new ToolRegistry();
        _context = new ToolContext
        {
            WorkingDirectory = "/tmp",
            SessionId = "benchmark-session"
        };

        // Register a simple test tool
        _simpleTool = new SimpleTestTool();
        _toolRegistry.RegisterTool(_simpleTool);

        // Register a complex test tool
        _complexTool = new ComplexTestTool();
        _toolRegistry.RegisterTool(_complexTool);
    }

    [Benchmark]
    public void RegisterTool()
    {
        var tool = new SimpleTestTool();
        var registry = new ToolRegistry();
        registry.RegisterTool(tool);
    }

    [Benchmark]
    public void GetTool()
    {
        _toolRegistry.GetTool("simple_test");
    }

    [Benchmark]
    public void GetAllTools()
    {
        _toolRegistry.GetAllTools();
    }

    [Benchmark]
    public async Task ExecuteSimpleTool()
    {
        var parameters = JsonSerializer.Serialize(new { value = "test" });
        await _simpleTool.ExecuteAsync(parameters, _context);
    }

    [Benchmark]
    public async Task ExecuteComplexTool()
    {
        var parameters = JsonSerializer.Serialize(new
        {
            operation = "process",
            data = new[] { 1, 2, 3, 4, 5 },
            options = new { verbose = true, timeout = 30 }
        });
        await _complexTool.ExecuteAsync(parameters, _context);
    }

    [Benchmark]
    public void SerializeToolParameters_Simple()
    {
        JsonSerializer.Serialize(new { value = "test" });
    }

    [Benchmark]
    public void SerializeToolParameters_Complex()
    {
        JsonSerializer.Serialize(new
        {
            operation = "process",
            data = Enumerable.Range(0, 100).ToArray(),
            options = new
            {
                verbose = true,
                timeout = 30,
                retries = 3,
                metadata = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 123 },
                    { "key3", true }
                }
            }
        });
    }

    [Benchmark]
    public void DeserializeToolParameters_Simple()
    {
        var json = "{\"value\":\"test\"}";
        JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }

    [Benchmark]
    public void DeserializeToolParameters_Complex()
    {
        var json = JsonSerializer.Serialize(new
        {
            operation = "process",
            data = Enumerable.Range(0, 100).ToArray(),
            options = new { verbose = true, timeout = 30 }
        });
        JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }

    // Simple test tool for benchmarking
    private class SimpleTestTool : ITool
    {
        public string Name => "simple_test";
        public string Description => "A simple test tool for benchmarking";
        public string ParameterSchema => "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"string\"}}}";

        public Task<ToolResult> ExecuteAsync(string parameters, ToolContext context, CancellationToken cancellationToken = default)
        {
            var result = new ToolResult
            {
                ToolCallId = "test",
                Success = true,
                Output = "Simple tool executed"
            };
            return Task.FromResult(result);
        }
    }

    // Complex test tool for benchmarking
    private class ComplexTestTool : ITool
    {
        public string Name => "complex_test";
        public string Description => "A complex test tool for benchmarking";
        public string ParameterSchema => "{\"type\":\"object\"}";

        public async Task<ToolResult> ExecuteAsync(string parameters, ToolContext context, CancellationToken cancellationToken = default)
        {
            // Simulate some work
            await Task.Delay(1, cancellationToken);

            var parsedParams = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(parameters);
            var data = parsedParams?["data"].EnumerateArray().Select(x => x.GetInt32()).ToArray() ?? Array.Empty<int>();

            // Simulate processing
            var sum = data.Sum();
            var avg = data.Length > 0 ? data.Average() : 0;

            var result = new ToolResult
            {
                ToolCallId = "test",
                Success = true,
                Output = JsonSerializer.Serialize(new { sum, avg, count = data.Length })
            };

            return result;
        }
    }
}
