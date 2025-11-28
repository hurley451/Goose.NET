# Goose.NET Performance Benchmarks

This project contains performance benchmarks for Goose.NET core operations using BenchmarkDotNet.

## Benchmark Categories

### 1. Message Processing Benchmarks (`MessageProcessingBenchmarks.cs`)
- **ProcessSingleMessage**: Benchmark for processing a single message
- **ProcessMessage_WithExistingContext**: Processing with 10 existing messages
- **ProcessMessage_WithLargeContext**: Processing with 100 existing messages
- **CreateConversationContext**: Context creation overhead
- **AddMessageToContext**: Adding messages to context
- **CreateMessage**: Message object creation

### 2. Streaming Benchmarks (`StreamingBenchmarks.cs`)
- **StreamMessage**: Basic streaming performance
- **StreamMessage_CollectResults**: Streaming with result collection
- **StreamMessage_ProcessChunks**: Streaming with chunk processing
- Parameterized by chunk count (10, 50, 100 chunks)

### 3. Session I/O Benchmarks (`SessionIOBenchmarks.cs`)
- **CreateSession**: Session creation
- **SaveContext_Small/Medium/Large**: Context persistence (10/50/200 messages)
- **LoadContext_Small/Medium/Large**: Context loading
- **ListSessions**: Session listing performance
- **Serialize/DeserializeContext**: JSON serialization performance

### 4. Tool Execution Benchmarks (`ToolExecutionBenchmarks.cs`)
- **RegisterTool**: Tool registration overhead
- **GetTool**: Tool retrieval performance
- **GetAllTools**: Bulk tool retrieval
- **ExecuteSimpleTool**: Simple tool execution
- **ExecuteComplexTool**: Complex tool with processing
- **Serialize/DeserializeToolParameters**: Parameter handling

## Running the Benchmarks

### Run All Benchmarks
```bash
dotnet run -c Release --project benchmarks/Goose.Benchmarks
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release --project benchmarks/Goose.Benchmarks --filter "*MessageProcessingBenchmarks*"
```

### Run Specific Benchmark Method
```bash
dotnet run -c Release --project benchmarks/Goose.Benchmarks --filter "*ProcessSingleMessage*"
```

### Run with Memory Profiler
```bash
dotnet run -c Release --project benchmarks/Goose.Benchmarks --memory
```

## Understanding Results

BenchmarkDotNet will output:
- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Gen0/Gen1/Gen2**: Garbage collection counts per 1000 operations
- **Allocated**: Allocated memory per operation

### Performance Goals

| Operation | Target | Notes |
|-----------|--------|-------|
| ProcessSingleMessage | < 50ms | Without actual LLM call |
| StreamMessage (100 chunks) | < 100ms | Overhead only |
| SaveContext_Small | < 10ms | 10 messages |
| SaveContext_Large | < 50ms | 200 messages |
| ExecuteSimpleTool | < 1ms | Minimal overhead |
| Tool Registration | < 100μs | One-time cost |

## Interpreting Memory Diagnostics

- **Gen0**: Short-lived allocations (good if low)
- **Gen1/Gen2**: Long-lived allocations (should be 0 for most operations)
- **Allocated**: Total memory per operation (lower is better)

### Memory Goals
- Message processing: < 10 KB per message
- Tool execution: < 5 KB per execution
- Session I/O: Proportional to message count

## Continuous Performance Monitoring

Run benchmarks after:
- Major refactoring
- Adding new features
- Dependency updates
- Before releases

Store results in `BenchmarkDotNet.Artifacts/results/` for trend analysis.

## Baseline Comparison

To compare against a baseline:
```bash
# Run and save baseline
dotnet run -c Release --project benchmarks/Goose.Benchmarks --exporters json

# Run comparison
dotnet run -c Release --project benchmarks/Goose.Benchmarks --baseline Baseline
```

## Troubleshooting

### Benchmarks Running Slowly
- Ensure running in Release mode (`-c Release`)
- Close other applications
- Disable antivirus temporarily
- Run on AC power (not battery)

### Inconsistent Results
- BenchmarkDotNet automatically warms up and runs multiple iterations
- If results vary significantly, check system load
- Consider running with `--job long` for more accurate results

## Additional Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/core/performance/)
