namespace LoggingLibrary;

/// <summary>
/// A log entry could not be written by the sink. Passed to the write-error
/// handler (see <see cref="LoggerOptions.OnWriteError"/>) together with the
/// entry that was not written, so it can still be recovered.
/// </summary>
public sealed class LogWriteException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="entry">The entry that could not be written.</param>
    /// <param name="innerException">The error raised by the sink.</param>
    public LogWriteException(LogEntry entry, Exception innerException)
        : base($"Failed to write a log entry: {innerException?.Message}", innerException)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(innerException);
        Entry = entry;
    }

    /// <summary>The entry that could not be written.</summary>
    public LogEntry Entry { get; }
}
