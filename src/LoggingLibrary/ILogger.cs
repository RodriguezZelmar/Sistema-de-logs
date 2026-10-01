namespace LoggingLibrary;

/// <summary>
/// Writes log entries. Code that logs should depend on this interface rather
/// than on <see cref="Logger"/>, so it can be tested with a fake logger.
/// </summary>
/// <remarks>
/// The convenience methods (<c>Info</c>, <c>Error</c>, ...) are extension
/// methods in <see cref="LoggerExtensions"/>, so an implementation only has to
/// provide these two members.
/// </remarks>
public interface ILogger
{
    /// <summary>
    /// Returns whether entries of <paramref name="level"/> would be written.
    /// Use it to skip building expensive messages that would be discarded.
    /// </summary>
    /// <param name="level">The level to check.</param>
    bool IsEnabled(LogLevel level);

    /// <summary>Writes an entry if <paramref name="level"/> is enabled.</summary>
    /// <param name="level">Severity of the entry.</param>
    /// <param name="message">Human-readable description of the event.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    void Log(LogLevel level, string message, Exception? exception = null);
}
