using System.Globalization;

namespace LoggingLibrary.Formatting;

/// <summary>Formatting rules shared by the built-in formatters.</summary>
internal static class LogFormatting
{
    /// <summary>
    /// Formats a timestamp as ISO 8601 in UTC with millisecond precision,
    /// e.g. <c>2026-10-01T20:30:15.432Z</c>. Always the same length.
    /// </summary>
    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "Debug",
        LogLevel.Info => "Info",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => level.ToString(),
    };
}
