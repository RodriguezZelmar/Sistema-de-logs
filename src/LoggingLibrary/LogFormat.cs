namespace LoggingLibrary;

/// <summary>Output format of the log file.</summary>
public enum LogFormat
{
    /// <summary>One JSON object per line (JSON Lines / NDJSON). Best for tools.</summary>
    Json = 0,

    /// <summary>Human-readable <c>timestamp [Level] message</c> lines.</summary>
    Text = 1,
}
