using System.Text.Json;
using LoggingLibrary.Formatting;
using LoggingLibrary.Sinks;
using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests.Sinks;

public sealed class FileLogSinkTests : IDisposable
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 1, 20, 30, 15, 432, TimeSpan.Zero);
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Constructor_CreatesMissingDirectoriesAndFile()
    {
        var path = _temp.Combine("nested", "logs", "application.log");

        using var sink = new FileLogSink(path, new JsonLogFormatter());

        Assert.True(File.Exists(path));
        Assert.Equal(path, sink.FilePath);
    }

    [Fact]
    public void Write_AppendsOneJsonLinePerEntry()
    {
        var path = _temp.Combine("application.log");

        using (var sink = new FileLogSink(path, new JsonLogFormatter()))
        {
            sink.Write(new LogEntry(Timestamp, LogLevel.Info, "first"));
            sink.Write(new LogEntry(Timestamp, LogLevel.Warning, "second"));
            sink.Write(new LogEntry(Timestamp, LogLevel.Error, "third"));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(["first", "second", "third"], lines.Select(MessageOf));
    }

    [Fact]
    public void Write_UsesUtf8WithoutBomAndLineFeedEndings()
    {
        var path = _temp.Combine("application.log");

        using (var sink = new FileLogSink(path, new TextLogFormatter()))
        {
            sink.Write(new LogEntry(Timestamp, LogLevel.Info, "Conexión"));
        }

        var bytes = File.ReadAllBytes(path);
        var expected = "2026-10-01T20:30:15.432Z [Info] Conexión\n"u8.ToArray();
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Write_ToExistingFile_KeepsPreviousContent()
    {
        var path = _temp.Combine("application.log");
        File.WriteAllText(path, "previous line\n");

        using (var sink = new FileLogSink(path, new TextLogFormatter()))
        {
            sink.Write(new LogEntry(Timestamp, LogLevel.Info, "new line"));
        }

        Assert.Equal(["previous line", "2026-10-01T20:30:15.432Z [Info] new line"], File.ReadAllLines(path));
    }

    [Fact]
    public void Write_AfterTruncatedLastLine_StartsOnANewLine()
    {
        // Simulates a previous run that crashed in the middle of writing an entry.
        var path = _temp.Combine("application.log");
        File.WriteAllText(path, "{\"timestamp\":\"2026-10-01T20:30:15.432Z\",\"lev");

        using (var sink = new FileLogSink(path, new JsonLogFormatter()))
        {
            sink.Write(new LogEntry(Timestamp, LogLevel.Info, "after crash"));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Equal("after crash", MessageOf(lines[1]));
    }

    [Fact]
    public void Write_IsFlushedImmediately()
    {
        var path = _temp.Combine("application.log");
        using var sink = new FileLogSink(path, new TextLogFormatter());

        sink.Write(new LogEntry(Timestamp, LogLevel.Info, "flushed"));

        // Read while the sink still holds the file open.
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.Contains("flushed", reader.ReadToEnd());
    }

    [Fact]
    public void Constructor_RelativePath_IsResolvedToFullPath()
    {
        using var sink = new FileLogSink(Path.GetRelativePath(Environment.CurrentDirectory, _temp.Combine("app.log")), new TextLogFormatter());

        Assert.Equal(_temp.Combine("app.log"), sink.FilePath);
    }

    [Fact]
    public void Write_AfterDispose_Throws()
    {
        var sink = new FileLogSink(_temp.Combine("app.log"), new TextLogFormatter());
        sink.Dispose();
        sink.Dispose(); // Disposing twice is allowed.

        Assert.Throws<ObjectDisposedException>(() => sink.Write(new LogEntry(Timestamp, LogLevel.Info, "m")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyPath_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => new FileLogSink(path, new TextLogFormatter()));
    }

    [Fact]
    public void Constructor_FileLockedByAnotherHandle_ThrowsIOException()
    {
        var path = _temp.Combine("locked.log");
        using var locked = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<IOException>(() => new FileLogSink(path, new TextLogFormatter()));
    }

    [Fact]
    public void Constructor_DirectoryCannotBeCreated_ThrowsIOException()
    {
        // A file with the same name as the directory we need blocks its creation.
        var blocker = _temp.Combine("blocker");
        File.WriteAllText(blocker, "");

        Assert.Throws<IOException>(() => new FileLogSink(Path.Combine(blocker, "app.log"), new TextLogFormatter()));
    }

    [Fact]
    public void Constructor_PathIsADirectory_Throws()
    {
        var directory = _temp.Combine("a-directory");
        Directory.CreateDirectory(directory);

        var exception = Record.Exception(() => new FileLogSink(directory, new TextLogFormatter()));

        Assert.True(exception is IOException or UnauthorizedAccessException, $"Unexpected exception: {exception}");
    }

    private static string? MessageOf(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        return doc.RootElement.GetProperty("message").GetString();
    }
}
