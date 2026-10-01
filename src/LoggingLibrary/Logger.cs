using LoggingLibrary.Formatting;
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
public sealed class Logger : ILogger, IDisposable
{
    private readonly ILogSink? _sink;
    private readonly bool _ownsSink;
    private readonly LogLevel _minimumLevel;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a logger that writes to the file described by <paramref name="options"/>.
    /// The file is opened immediately; dispose the logger to close it.
    /// </summary>
    /// <param name="options">Logger settings. They are validated and copied; later changes have no effect.</param>
    /// <exception cref="ArgumentException">An option is invalid.</exception>
    /// <exception cref="IOException">The log directory cannot be created or the file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The process has no permission to write the log file.</exception>
    public Logger(LoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _minimumLevel = options.MinimumLevel;
        _timeProvider = TimeProvider.System;
        if (options.Enabled)
        {
            _sink = new FileLogSink(
                options.FilePath,
                CreateFormatter(options.Format),
                options.MaxFileSizeBytes,
                options.MaxRetainedFiles);
            _ownsSink = true;
        }
    }

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
    public bool IsEnabled(LogLevel level) => _sink is not null && level >= _minimumLevel;

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        var entry = new LogEntry(_timeProvider.GetUtcNow(), level, message, exception);
        _sink!.Write(entry);
    }

    /// <summary>Closes the log file if this logger created it. A sink passed in by the caller is not disposed.</summary>
    public void Dispose()
    {
        if (_ownsSink)
        {
            _sink?.Dispose();
        }
    }

    private static ILogFormatter CreateFormatter(LogFormat format) => format switch
    {
        LogFormat.Json => new JsonLogFormatter(),
        LogFormat.Text => new TextLogFormatter(),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };
}
