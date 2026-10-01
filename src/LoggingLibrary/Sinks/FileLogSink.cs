using System.Text;
using LoggingLibrary.Formatting;

namespace LoggingLibrary.Sinks;

/// <summary>
/// Appends formatted entries to a file, one entry per line (UTF-8, no BOM,
/// <c>\n</c> line endings), rotating it when it reaches a size limit.
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
/// Rotation: before a write that would make the active file larger than the
/// size limit, the file is renamed to <c>name.1.ext</c>, existing archives are
/// shifted (<c>name.1.ext</c> → <c>name.2.ext</c>, ...), the oldest archive
/// beyond the retention limit is deleted, and a new empty active file is
/// started. Entries are never split across files.
/// </para>
/// <para>
/// Thread-safe: writes and rotation are serialized with a lock. Only one sink
/// (in one process) should write to a given file; other processes can open it
/// for reading.
/// </para>
/// </remarks>
public sealed class FileLogSink : ILogSink
{
    /// <summary>Upper bound for the number of retained archives.</summary>
    public const int MaxRetainedFilesLimit = 1000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Lock _sync = new();
    private readonly ILogFormatter _formatter;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxRetainedFiles;
    private readonly string _directory;
    private readonly string _baseName;
    private readonly string _extension;
    private FileStream? _stream;
    private long _currentSize;
    private bool _disposed;

    /// <summary>Creates a sink that never rotates and opens (or creates) the log file.</summary>
    /// <param name="filePath">Path of the log file. Relative paths are resolved against the current directory.</param>
    /// <param name="formatter">Formatter used to turn entries into lines.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or not a valid path.</exception>
    /// <exception cref="IOException">The directory cannot be created or the file cannot be opened (for example, it is locked).</exception>
    /// <exception cref="UnauthorizedAccessException">The process has no permission to create or write the file.</exception>
    public FileLogSink(string filePath, ILogFormatter formatter)
        : this(filePath, formatter, long.MaxValue, 0)
    {
    }

    /// <summary>Creates a rotating sink and opens (or creates) the log file.</summary>
    /// <param name="filePath">Path of the log file. Relative paths are resolved against the current directory.</param>
    /// <param name="formatter">Formatter used to turn entries into lines.</param>
    /// <param name="maxFileSizeBytes">Size the active file must not exceed (unless a single entry is larger). Must be positive.</param>
    /// <param name="maxRetainedFiles">
    /// Number of rotated files to keep besides the active file, from 0 to <see cref="MaxRetainedFilesLimit"/>.
    /// With 0, rotation discards the content of the full file.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or not a valid path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size or count is out of range.</exception>
    /// <exception cref="IOException">The directory cannot be created or the file cannot be opened (for example, it is locked).</exception>
    /// <exception cref="UnauthorizedAccessException">The process has no permission to create or write the file.</exception>
    public FileLogSink(string filePath, ILogFormatter formatter, long maxFileSizeBytes, int maxRetainedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetainedFiles);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRetainedFiles, MaxRetainedFilesLimit);

        FilePath = Path.GetFullPath(filePath);
        _formatter = formatter;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxRetainedFiles = maxRetainedFiles;
        _directory = Path.GetDirectoryName(FilePath) ?? string.Empty;
        _baseName = Path.GetFileNameWithoutExtension(FilePath);
        _extension = Path.GetExtension(FilePath);
        OpenStream();
    }

    /// <summary>Absolute path of the active log file.</summary>
    public string FilePath { get; }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The sink has been disposed.</exception>
    /// <exception cref="IOException">The entry could not be written or the file could not be rotated.</exception>
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
                if (_stream is null)
                {
                    OpenStream();
                }

                // Written as a subtraction so a huge limit cannot overflow.
                if (_currentSize > 0 && bytes.Length > _maxFileSizeBytes - _currentSize)
                {
                    Rotate();
                }

                _stream!.Write(bytes);
                _stream.Flush();
                _currentSize += bytes.Length;
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

    /// <summary>Path of the archive with the given index, e.g. <c>logs/application.2.log</c>.</summary>
    private string ArchivePath(int index) => Path.Combine(_directory, $"{_baseName}.{index}{_extension}");

    private void Rotate()
    {
        CloseStream();

        if (_maxRetainedFiles == 0)
        {
            File.Delete(FilePath);
        }
        else
        {
            // Make room for the shift by deleting the oldest archive, then move
            // each archive one index up, newest last. File.Delete ignores
            // missing files and missing archives are skipped, so a rotation
            // that failed half-way can simply be retried.
            File.Delete(ArchivePath(_maxRetainedFiles));
            for (var index = _maxRetainedFiles - 1; index >= 1; index--)
            {
                var source = ArchivePath(index);
                if (File.Exists(source))
                {
                    File.Move(source, ArchivePath(index + 1));
                }
            }
            File.Move(FilePath, ArchivePath(1));
        }

        OpenStream();
    }

    private void OpenStream()
    {
        if (_directory.Length > 0)
        {
            Directory.CreateDirectory(_directory);
        }
        _stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _currentSize = _stream.Length;
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
