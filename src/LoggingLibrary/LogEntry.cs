namespace LoggingLibrary;

/// <summary>
/// An immutable log event. <see cref="Logger"/> creates one per accepted call
/// and hands it to a sink.
/// </summary>
/// <remarks>
/// This is a class with read-only properties rather than a positional record,
/// so new properties can be added in later versions without breaking callers
/// that construct or deconstruct entries.
/// </remarks>
public sealed class LogEntry
{
    /// <summary>Creates a log entry.</summary>
    /// <param name="timestamp">When the event happened. It is converted to UTC.</param>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Human-readable description of the event.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public LogEntry(DateTimeOffset timestamp, LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        Timestamp = timestamp.ToUniversalTime();
        Level = level;
        Message = message;
        Exception = exception;
    }

    /// <summary>When the event happened, always in UTC (offset zero).</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Severity of the event.</summary>
    public LogLevel Level { get; }

    /// <summary>Human-readable description of the event.</summary>
    public string Message { get; }

    /// <summary>Exception associated with the event, or <see langword="null"/>.</summary>
    public Exception? Exception { get; }
}
