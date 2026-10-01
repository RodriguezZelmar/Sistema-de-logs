using LoggingLibrary.Formatting;
using LoggingLibrary.Sinks;
using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests.Sinks;

public sealed class FileLogSinkRotationTests : IDisposable
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 1, 20, 30, 15, 432, TimeSpan.Zero);
    private static readonly TextLogFormatter Formatter = new();

    // Every test message has the same length ("m00".."m99"), so every line
    // has the same size and the rotation points are exactly predictable.
    private static readonly int LineBytes = Formatter.Format(Entry(0)).Length + 1;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void BelowLimit_DoesNotRotate()
    {
        WriteEntries(count: 3, maxFileSizeBytes: 3 * LineBytes, maxRetainedFiles: 3);

        Assert.Equal(["app.log"], _temp.FileNames());
        Assert.Equal(["m00", "m01", "m02"], Messages("app.log"));
    }

    [Fact]
    public void ExceedingLimit_RotatesToNumberedFile()
    {
        WriteEntries(count: 3, maxFileSizeBytes: 2 * LineBytes, maxRetainedFiles: 3);

        Assert.Equal(["app.1.log", "app.log"], _temp.FileNames());
        Assert.Equal(["m00", "m01"], Messages("app.1.log"));
        Assert.Equal(["m02"], Messages("app.log"));
    }

    [Fact]
    public void ManyRotations_KeepOnlyMaxRetainedFiles_NewestFirst()
    {
        // 2 entries per file, 10 entries => 5 full files; only 3 archives are kept.
        WriteEntries(count: 10, maxFileSizeBytes: 2 * LineBytes, maxRetainedFiles: 3);

        Assert.Equal(["app.1.log", "app.2.log", "app.3.log", "app.log"], _temp.FileNames());
        Assert.Equal(["m08", "m09"], Messages("app.log"));
        Assert.Equal(["m06", "m07"], Messages("app.1.log"));
        Assert.Equal(["m04", "m05"], Messages("app.2.log"));
        Assert.Equal(["m02", "m03"], Messages("app.3.log"));
    }

    [Fact]
    public void Files_NeverExceedLimit()
    {
        var limit = (2 * LineBytes) + (LineBytes / 2); // Not a multiple of the line size.

        WriteEntries(count: 25, maxFileSizeBytes: limit, maxRetainedFiles: 20);

        Assert.All(Directory.GetFiles(_temp.Path), file => Assert.InRange(new FileInfo(file).Length, 1, limit));
        Assert.Equal(25, Directory.GetFiles(_temp.Path).Sum(file => File.ReadAllLines(file).Length));
    }

    [Fact]
    public void MaxRetainedFilesZero_KeepsOnlyActiveFile()
    {
        WriteEntries(count: 5, maxFileSizeBytes: 2 * LineBytes, maxRetainedFiles: 0);

        Assert.Equal(["app.log"], _temp.FileNames());
        Assert.Equal(["m04"], Messages("app.log"));
    }

    [Fact]
    public void EntryLargerThanLimit_IsWrittenWholeInItsOwnFile()
    {
        var path = _temp.Combine("app.log");
        var big = new string('x', 100);

        using (var sink = new FileLogSink(path, Formatter, maxFileSizeBytes: 50, maxRetainedFiles: 5))
        {
            sink.Write(Entry(0));
            sink.Write(new LogEntry(Timestamp, LogLevel.Info, big));
            sink.Write(Entry(2));
        }

        Assert.Equal(["app.1.log", "app.2.log", "app.log"], _temp.FileNames());
        Assert.Equal(["m00"], Messages("app.2.log"));
        Assert.Equal([big], Messages("app.1.log"));
        Assert.Equal(["m02"], Messages("app.log"));
    }

    [Fact]
    public void ExistingFileOverLimit_IsRotatedOnFirstWrite()
    {
        var path = _temp.Combine("app.log");
        File.WriteAllText(path, new string('x', 99) + "\n");

        using (var sink = new FileLogSink(path, Formatter, maxFileSizeBytes: 100, maxRetainedFiles: 2))
        {
            sink.Write(Entry(0));
        }

        Assert.Equal(["app.1.log", "app.log"], _temp.FileNames());
        Assert.Equal(["m00"], Messages("app.log"));
    }

    [Fact]
    public void FileWithoutExtension_AppendsIndex()
    {
        var path = _temp.Combine("app");

        using (var sink = new FileLogSink(path, Formatter, maxFileSizeBytes: LineBytes, maxRetainedFiles: 2))
        {
            sink.Write(Entry(0));
            sink.Write(Entry(1));
        }

        Assert.Equal(["app", "app.1"], _temp.FileNames());
    }

    [Fact]
    public void Rotation_ThroughLoggerOptions()
    {
        var path = _temp.Combine("logs", "application.log");
        var options = new LoggerOptions
        {
            FilePath = path,
            MaxFileSizeBytes = 200,
            MaxRetainedFiles = 2,
        };

        using (var logger = new Logger(options))
        {
            for (var i = 0; i < 50; i++)
            {
                logger.Info($"Message {i:D2}");
            }
        }

        var files = Directory.GetFiles(_temp.Combine("logs")).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["application.1.log", "application.2.log", "application.log"], files.Select(f => Path.GetFileName(f)));
        Assert.All(files, f => Assert.InRange(new FileInfo(f).Length, 1, 200));
        Assert.Contains("Message 49", File.ReadAllLines(path)[^1]);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(100, -1)]
    [InlineData(100, FileLogSink.MaxRetainedFilesLimit + 1)]
    public void Constructor_InvalidLimits_Throw(long maxFileSizeBytes, int maxRetainedFiles)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileLogSink(_temp.Combine("app.log"), Formatter, maxFileSizeBytes, maxRetainedFiles));
    }

    private static LogEntry Entry(int index) => new(Timestamp, LogLevel.Info, $"m{index:D2}");

    private void WriteEntries(int count, long maxFileSizeBytes, int maxRetainedFiles)
    {
        using var sink = new FileLogSink(_temp.Combine("app.log"), Formatter, maxFileSizeBytes, maxRetainedFiles);
        for (var i = 0; i < count; i++)
        {
            sink.Write(Entry(i));
        }
    }

    private string[] Messages(string fileName) =>
        File.ReadAllLines(_temp.Combine(fileName)).Select(line => line[(line.IndexOf("] ", StringComparison.Ordinal) + 2)..]).ToArray();
}
