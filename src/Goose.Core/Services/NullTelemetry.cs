using Goose.Core.Abstractions;

namespace Goose.Core.Services;

/// <summary>
/// Null object pattern implementation of telemetry that does nothing
/// Use this when telemetry is disabled or not needed
/// </summary>
public class NullTelemetry : ITelemetry
{
    public static readonly NullTelemetry Instance = new();

    private NullTelemetry() { }

    public void RecordMetric(string name, double value, IDictionary<string, string>? tags = null)
    {
        // No-op
    }

    public void IncrementCounter(string name, long increment = 1, IDictionary<string, string>? tags = null)
    {
        // No-op
    }

    public void RecordDuration(string name, TimeSpan duration, IDictionary<string, string>? tags = null)
    {
        // No-op
    }

    public IDisposable BeginTimedOperation(string operationName, IDictionary<string, string>? tags = null)
    {
        return NullDisposable.Instance;
    }

    public void TrackEvent(string eventName, IDictionary<string, string>? properties = null)
    {
        // No-op
    }

    public void TrackException(Exception exception, IDictionary<string, string>? properties = null)
    {
        // No-op
    }

    public void TrackDependency(
        string dependencyType,
        string target,
        string operation,
        TimeSpan duration,
        bool success,
        IDictionary<string, string>? properties = null)
    {
        // No-op
    }

    private class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        private NullDisposable() { }
        public void Dispose() { }
    }
}
