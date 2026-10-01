namespace LoggingLibrary;

/// <summary>
/// Settings for a <see cref="Logger"/> that writes to a file.
/// All properties have defaults, so only the ones that differ need to be set.
/// </summary>
/// <example>
/// <code>
/// var logger = new Logger(new LoggerOptions
/// {
///     MinimumLevel = LogLevel.Info,
///     FilePath = "logs/application.log",
/// });
/// </code>
/// </example>
public sealed class LoggerOptions
{
    /// <summary>
    /// Whether logging is on. When <see langword="false"/>, the logger discards
    /// every entry and does not create or open the log file. Default: <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Entries below this level are discarded. Default: <see cref="LogLevel.Info"/>.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Path of the log file. Relative paths are resolved against the current
    /// directory of the process. Missing directories are created.
    /// Default: <c>logs/application.log</c>.
    /// </summary>
    public string FilePath { get; set; } = "logs/application.log";

    /// <summary>Output format. Default: <see cref="LogFormat.Json"/>.</summary>
    public LogFormat Format { get; set; } = LogFormat.Json;

    /// <summary>Checks that every option has a usable value.</summary>
    /// <exception cref="ArgumentException">An option is invalid. The message names the option.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(MinimumLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumLevel), MinimumLevel, $"{nameof(MinimumLevel)} is not a valid {nameof(LogLevel)}.");
        }

        if (!Enum.IsDefined(Format))
        {
            throw new ArgumentOutOfRangeException(nameof(Format), Format, $"{nameof(Format)} is not a valid {nameof(LogFormat)}.");
        }

        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException($"{nameof(FilePath)} must not be empty.", nameof(FilePath));
        }

        try
        {
            Path.GetFullPath(FilePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException($"{nameof(FilePath)} '{FilePath}' is not a valid path: {ex.Message}", nameof(FilePath), ex);
        }
    }
}
