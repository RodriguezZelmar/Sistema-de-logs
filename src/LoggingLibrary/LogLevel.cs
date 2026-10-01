namespace LoggingLibrary;

/// <summary>
/// Severity of a log entry. Values are ordered from least to most severe, so a
/// minimum level lets through that level and every level above it.
/// </summary>
public enum LogLevel
{
    /// <summary>Detailed diagnostic information, usually enabled only while developing or troubleshooting.</summary>
    Debug = 0,

    /// <summary>Normal application events, such as startup or a completed operation.</summary>
    Info = 1,

    /// <summary>An unexpected situation the application can recover from.</summary>
    Warning = 2,

    /// <summary>An operation failed.</summary>
    Error = 3,

    /// <summary>A failure that leaves the application unable to keep working.</summary>
    Critical = 4,
}
