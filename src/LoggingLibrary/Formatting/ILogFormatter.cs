namespace LoggingLibrary.Formatting;

/// <summary>
/// Converts a <see cref="LogEntry"/> into text. Sinks that write text use a
/// formatter so the output format can change without changing the sink.
/// </summary>
/// <remarks>Implementations must be thread-safe.</remarks>
public interface ILogFormatter
{
    /// <summary>Formats an entry, without a trailing line break.</summary>
    /// <param name="entry">The entry to format.</param>
    string Format(LogEntry entry);
}
