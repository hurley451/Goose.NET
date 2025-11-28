using System.Collections.Concurrent;
using System.Diagnostics;
using Goose.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Goose.Core.Services;

/// <summary>
/// In-memory telemetry implementation for development and testing
/// </summary>
public class InMemoryTelemetry : ITelemetry
{
    private readonly ILogger<InMemoryTelemetry> _logger;
    private readonly ConcurrentDictionary<string, MetricData> _metrics;
    private readonly ConcurrentDictionary<string, long> _counters;
    private readonly ConcurrentBag<EventData> _events;
    private readonly ConcurrentBag<ExceptionData> _exceptions;
    private readonly ConcurrentBag<DependencyData> _dependencies;

    public InMemoryTelemetry(ILogger<InMemoryTelemetry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = new ConcurrentDictionary<string, MetricData>();
        _counters = new ConcurrentDictionary<string, long>();
        _events = new ConcurrentBag<EventData>();
        _exceptions = new ConcurrentBag<ExceptionData>();
        _dependencies = new ConcurrentBag<DependencyData>();
    }

    public void RecordMetric(string name, double value, IDictionary<string, string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = BuildKey(name, tags);
        _metrics.AddOrUpdate(
            key,
            new MetricData { Name = name, Value = value, Count = 1, Sum = value, Min = value, Max = value, Tags = tags },
            (_, existing) => new MetricData
            {
                Name = name,
                Value = value,
                Count = existing.Count + 1,
                Sum = existing.Sum + value,
                Min = Math.Min(existing.Min, value),
                Max = Math.Max(existing.Max, value),
                Tags = tags
            });

        _logger.LogDebug("Metric recorded: {MetricName} = {Value} {Tags}",
            name, value, tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "");
    }

    public void IncrementCounter(string name, long increment = 1, IDictionary<string, string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = BuildKey(name, tags);
        var newValue = _counters.AddOrUpdate(key, increment, (_, existing) => existing + increment);

        _logger.LogDebug("Counter incremented: {CounterName} += {Increment} (total: {Total}) {Tags}",
            name, increment, newValue, tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "");
    }

    public void RecordDuration(string name, TimeSpan duration, IDictionary<string, string>? tags = null)
    {
        RecordMetric($"{name}.duration_ms", duration.TotalMilliseconds, tags);
    }

    public IDisposable BeginTimedOperation(string operationName, IDictionary<string, string>? tags = null)
    {
        return new TimedOperation(this, operationName, tags);
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var eventData = new EventData
        {
            Name = eventName,
            Timestamp = DateTime.UtcNow,
            Properties = properties
        };

        _events.Add(eventData);

        _logger.LogInformation("Event tracked: {EventName} {Properties}",
            eventName, properties != null ? string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}")) : "");
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var exceptionData = new ExceptionData
        {
            Exception = exception,
            Timestamp = DateTime.UtcNow,
            Properties = properties
        };

        _exceptions.Add(exceptionData);

        _logger.LogError(exception, "Exception tracked: {ExceptionType} {Properties}",
            exception.GetType().Name, properties != null ? string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}")) : "");
    }

    public void TrackDependency(
        string dependencyType,
        string target,
        string operation,
        TimeSpan duration,
        bool success,
        IDictionary<string, string>? properties = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var dependencyData = new DependencyData
        {
            Type = dependencyType,
            Target = target,
            Operation = operation,
            Duration = duration,
            Success = success,
            Timestamp = DateTime.UtcNow,
            Properties = properties
        };

        _dependencies.Add(dependencyData);

        var logLevel = success ? LogLevel.Debug : LogLevel.Warning;
        _logger.Log(logLevel,
            "Dependency tracked: {DependencyType} {Target} {Operation} {Duration}ms Success={Success} {Properties}",
            dependencyType, target, operation, duration.TotalMilliseconds, success,
            properties != null ? string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}")) : "");
    }

    /// <summary>
    /// Gets all recorded metrics (for testing/debugging)
    /// </summary>
    public IReadOnlyDictionary<string, MetricData> GetMetrics() => _metrics;

    /// <summary>
    /// Gets all recorded counters (for testing/debugging)
    /// </summary>
    public IReadOnlyDictionary<string, long> GetCounters() => _counters;

    /// <summary>
    /// Gets all recorded events (for testing/debugging)
    /// </summary>
    public IReadOnlyList<EventData> GetEvents() => _events.ToList();

    /// <summary>
    /// Gets all recorded exceptions (for testing/debugging)
    /// </summary>
    public IReadOnlyList<ExceptionData> GetExceptions() => _exceptions.ToList();

    /// <summary>
    /// Gets all recorded dependencies (for testing/debugging)
    /// </summary>
    public IReadOnlyList<DependencyData> GetDependencies() => _dependencies.ToList();

    /// <summary>
    /// Clears all telemetry data (for testing)
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
        _counters.Clear();
        _events.Clear();
        _exceptions.Clear();
        _dependencies.Clear();
    }

    private static string BuildKey(string name, IDictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return name;

        var tagString = string.Join(",", tags.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        return $"{name}[{tagString}]";
    }

    private class TimedOperation : IDisposable
    {
        private readonly InMemoryTelemetry _telemetry;
        private readonly string _operationName;
        private readonly IDictionary<string, string>? _tags;
        private readonly Stopwatch _stopwatch;

        public TimedOperation(InMemoryTelemetry telemetry, string operationName, IDictionary<string, string>? tags)
        {
            _telemetry = telemetry;
            _operationName = operationName;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _telemetry.RecordDuration(_operationName, _stopwatch.Elapsed, _tags);
        }
    }

    public record MetricData
    {
        public required string Name { get; init; }
        public double Value { get; init; }
        public long Count { get; init; }
        public double Sum { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public IDictionary<string, string>? Tags { get; init; }

        public double Average => Count > 0 ? Sum / Count : 0;
    }

    public record EventData
    {
        public required string Name { get; init; }
        public DateTime Timestamp { get; init; }
        public IDictionary<string, string>? Properties { get; init; }
    }

    public record ExceptionData
    {
        public required Exception Exception { get; init; }
        public DateTime Timestamp { get; init; }
        public IDictionary<string, string>? Properties { get; init; }
    }

    public record DependencyData
    {
        public required string Type { get; init; }
        public required string Target { get; init; }
        public required string Operation { get; init; }
        public TimeSpan Duration { get; init; }
        public bool Success { get; init; }
        public DateTime Timestamp { get; init; }
        public IDictionary<string, string>? Properties { get; init; }
    }
}
