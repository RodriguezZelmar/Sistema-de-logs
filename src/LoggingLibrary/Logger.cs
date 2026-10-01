using LoggingLibrary.Sinks;

namespace LoggingLibrary;

/// <summary>
/// Default <see cref="ILogger"/> implementation: discards entries below the
/// minimum level, timestamps the rest and forwards them to a sink.
/// </summary>
/// <remarks>
/// The logger holds no mutable state, so it is safe to use from multiple
/// threads as long as its sink is (all built-in sinks are).
/// </remarks>
public sealed class Logger : ILogger
{
    private readonly ILogSink _sink;
    private readonly LogLevel _minimumLevel;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a logger that writes to a custom sink.</summary>
    /// <param name="sink">Destination of the entries. The caller keeps ownership and disposes it.</param>
    /// <param name="minimumLevel">Entries below this level are discarded.</param>
    /// <param name="timeProvider">Clock used for timestamps. Defaults to <see cref="TimeProvider.System"/>.</param>
    public Logger(ILogSink sink, LogLevel minimumLevel = LogLevel.Debug, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sink);

        _sink = sink;
        _minimumLevel = minimumLevel;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        var entry = new LogEntry(_timeProvider.GetUtcNow(), level, message, exception);
        _sink.Write(entry);
    }
}
