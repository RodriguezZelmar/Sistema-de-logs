using LoggingLibrary.Formatting;
using LoggingLibrary.Sinks;

namespace LoggingLibrary;

/// <summary>
/// Default <see cref="ILogger"/> implementation: discards entries below the
/// minimum level, timestamps the rest and forwards them to a sink.
/// </summary>
/// <remarks>
/// <para>
/// The logger holds no mutable state, so it is safe to use from multiple
/// threads as long as its sink is (all built-in sinks are).
/// </para>
/// <para>
/// Errors raised by the sink while writing do not propagate to the caller:
/// they are passed, with the unwritten entry, to the write-error handler,
/// which by default prints them to the standard error stream.
/// </para>
/// </remarks>
public sealed class Logger : ILogger, IDisposable
{
    private static readonly TextLogFormatter StandardErrorFormatter = new();

    private readonly ILogSink? _sink;
    private readonly bool _ownsSink;
    private readonly LogLevel _minimumLevel;
    private readonly TimeProvider _timeProvider;
    private readonly Action<LogWriteException> _onWriteError;

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
        _onWriteError = options.OnWriteError ?? WriteToStandardError;
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
    /// <param name="onWriteError">
    /// Receives errors raised by the sink. Defaults to writing them to the
    /// standard error stream. See <see cref="LoggerOptions.OnWriteError"/>.
    /// </param>
    public Logger(
        ILogSink sink,
        LogLevel minimumLevel = LogLevel.Debug,
        TimeProvider? timeProvider = null,
        Action<LogWriteException>? onWriteError = null)
    {
        ArgumentNullException.ThrowIfNull(sink);

        _sink = sink;
        _minimumLevel = minimumLevel;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _onWriteError = onWriteError ?? WriteToStandardError;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) => _sink is not null && level >= _minimumLevel;

    /// <inheritdoc />
    /// <remarks>
    /// If the sink fails, the error is passed to the write-error handler
    /// instead of being thrown. Only a handler that throws makes this method throw.
    /// </remarks>
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        var entry = new LogEntry(_timeProvider.GetUtcNow(), level, message, exception);
        try
        {
            _sink!.Write(entry);
        }
        catch (Exception ex)
        {
            _onWriteError(new LogWriteException(entry, ex));
        }
    }

    /// <summary>Closes the log file if this logger created it. A sink passed in by the caller is not disposed.</summary>
    /// <remarks>Entries logged after disposal are reported to the write-error handler.</remarks>
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

    /// <summary>Default write-error handler: prints the error and the lost entry to stderr.</summary>
    private static void WriteToStandardError(LogWriteException error)
    {
        try
        {
            var cause = error.InnerException!;
            Console.Error.WriteLine(
                $"[LoggingLibrary] Failed to write a log entry ({cause.GetType().FullName}: {cause.Message}). Entry:{Environment.NewLine}" +
                StandardErrorFormatter.Format(error.Entry));
        }
        catch (Exception)
        {
            // Standard error is unavailable too: there is nowhere left to report to,
            // and the logging call must not fail because of it.
        }
    }
}
