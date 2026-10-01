namespace LoggingLibrary.Sinks;

/// <summary>
/// A destination for log entries (a file, the console, a remote service...).
/// <see cref="Logger"/> only depends on this interface, so new destinations
/// can be added without changing the logger.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe: a logger can call <see cref="Write"/>
/// from several threads at the same time.
/// </remarks>
public interface ILogSink : IDisposable
{
    /// <summary>Writes one entry to the destination.</summary>
    /// <param name="entry">The entry to write.</param>
    void Write(LogEntry entry);
}
