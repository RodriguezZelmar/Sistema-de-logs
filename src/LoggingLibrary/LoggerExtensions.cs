namespace LoggingLibrary;

/// <summary>
/// Level-specific shortcuts for <see cref="ILogger.Log"/>.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>Writes a <see cref="LogLevel.Debug"/> entry.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">Human-readable description of the event.</param>
    public static void Debug(this ILogger logger, string message)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Debug, message);
    }

    /// <summary>Writes an <see cref="LogLevel.Info"/> entry.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">Human-readable description of the event.</param>
    public static void Info(this ILogger logger, string message)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Info, message);
    }

    /// <summary>Writes a <see cref="LogLevel.Warning"/> entry.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">Human-readable description of the event.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    public static void Warning(this ILogger logger, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Warning, message, exception);
    }

    /// <summary>Writes an <see cref="LogLevel.Error"/> entry.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">Human-readable description of the event.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    public static void Error(this ILogger logger, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Error, message, exception);
    }

    /// <summary>Writes a <see cref="LogLevel.Critical"/> entry.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">Human-readable description of the event.</param>
    /// <param name="exception">Exception associated with the event, if any.</param>
    public static void Critical(this ILogger logger, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Critical, message, exception);
    }
}
