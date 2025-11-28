namespace Goose.Core.Abstractions;

/// <summary>
/// Provides telemetry and metrics tracking capabilities
/// </summary>
public interface ITelemetry
{
    /// <summary>
    /// Records a metric value
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Metric value</param>
    /// <param name="tags">Optional tags for dimensional metrics</param>
    void RecordMetric(string name, double value, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records a counter increment
    /// </summary>
    /// <param name="name">Counter name</param>
    /// <param name="increment">Amount to increment by (default 1)</param>
    /// <param name="tags">Optional tags for dimensional metrics</param>
    void IncrementCounter(string name, long increment = 1, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records a duration/timing metric
    /// </summary>
    /// <param name="name">Operation name</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="tags">Optional tags for dimensional metrics</param>
    void RecordDuration(string name, TimeSpan duration, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Starts a timed operation
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="tags">Optional tags for the operation</param>
    /// <returns>A disposable timing scope that records duration on disposal</returns>
    IDisposable BeginTimedOperation(string operationName, IDictionary<string, string>? tags = null);

    /// <summary>
    /// Records an event
    /// </summary>
    /// <param name="eventName">Event name</param>
    /// <param name="properties">Optional event properties</param>
    void TrackEvent(string eventName, IDictionary<string, string>? properties = null);

    /// <summary>
    /// Records an exception
    /// </summary>
    /// <param name="exception">The exception to track</param>
    /// <param name="properties">Optional additional properties</param>
    void TrackException(Exception exception, IDictionary<string, string>? properties = null);

    /// <summary>
    /// Records a dependency call (external service, database, etc.)
    /// </summary>
    /// <param name="dependencyType">Type of dependency (HTTP, Database, etc.)</param>
    /// <param name="target">Target of the dependency (URL, table name, etc.)</param>
    /// <param name="operation">Operation performed</param>
    /// <param name="duration">Duration of the call</param>
    /// <param name="success">Whether the call was successful</param>
    /// <param name="properties">Optional additional properties</param>
    void TrackDependency(
        string dependencyType,
        string target,
        string operation,
        TimeSpan duration,
        bool success,
        IDictionary<string, string>? properties = null);
}
