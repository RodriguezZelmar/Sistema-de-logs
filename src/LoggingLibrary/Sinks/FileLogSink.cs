using System.Text;
using LoggingLibrary.Formatting;

namespace LoggingLibrary.Sinks;

/// <summary>
/// Appends formatted entries to a file, one entry per line (UTF-8, no BOM,
/// <c>\n</c> line endings).
/// </summary>
/// <remarks>
/// <para>
/// The file is opened when the sink is created and kept open, so a missing or
/// inaccessible location is reported immediately instead of on the first
/// write. Missing directories are created. Each write is flushed to the
/// operating system, so entries are not lost if the process ends without
/// disposing the sink.
/// </para>
/// <para>
/// Thread-safe: writes are serialized with a lock. Only one sink (in one
/// process) should write to a given file; other processes can open it for
/// reading.
/// </para>
/// </remarks>
public sealed class FileLogSink : ILogSink
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _sync = new();
    private readonly ILogFormatter _formatter;
    private FileStream? _stream;
    private bool _disposed;

    /// <summary>Creates the sink and opens (or creates) the log file.</summary>
    /// <param name="filePath">Path of the log file. Relative paths are resolved against the current directory.</param>
    /// <param name="formatter">Formatter used to turn entries into lines.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or not a valid path.</exception>
    /// <exception cref="IOException">The directory cannot be created or the file cannot be opened (for example, it is locked).</exception>
    /// <exception cref="UnauthorizedAccessException">The process has no permission to create or write the file.</exception>
    public FileLogSink(string filePath, ILogFormatter formatter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(formatter);

        FilePath = Path.GetFullPath(filePath);
        _formatter = formatter;
        _stream = OpenStream();
    }

    /// <summary>Absolute path of the active log file.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The sink has been disposed.</exception>
    /// <exception cref="IOException">The entry could not be written.</exception>
    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Formatting and encoding touch no shared state, so they run outside
        // the lock to keep the critical section short.
        var bytes = Utf8NoBom.GetBytes(_formatter.Format(entry) + "\n");

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            try
            {
                _stream ??= OpenStream();
                _stream.Write(bytes);
                _stream.Flush();
            }
            catch
            {
                // Drop the handle so the next write reopens the file instead
                // of reusing a stream in an unknown state.
                CloseStream();
                throw;
            }
        }
    }

    /// <summary>Flushes and closes the file. Later writes throw <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            CloseStream();
        }
    }

    private FileStream OpenStream()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
    }

    private void CloseStream()
    {
        try
        {
            _stream?.Dispose();
        }
        finally
        {
            _stream = null;
        }
    }
}
