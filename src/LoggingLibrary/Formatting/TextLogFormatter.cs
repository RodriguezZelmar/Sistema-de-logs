namespace LoggingLibrary.Formatting;

/// <summary>
/// Formats entries as human-readable text:
/// <code>2026-10-01T20:30:15.432Z [Info] Application started</code>
/// </summary>
/// <remarks>
/// When the entry has an exception, its full <see cref="Exception.ToString"/>
/// output follows on the next lines. Because of that (and because messages
/// may contain line breaks) this format is meant for people; use
/// <see cref="JsonLogFormatter"/> when the logs are processed by tools.
/// </remarks>
public sealed class TextLogFormatter : ILogFormatter
{
    /// <inheritdoc />
    public string Format(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var line = $"{LogFormatting.FormatTimestamp(entry.Timestamp)} [{LogFormatting.LevelName(entry.Level)}] {entry.Message}";
        return entry.Exception is null ? line : $"{line}\n{entry.Exception}";
    }
}
