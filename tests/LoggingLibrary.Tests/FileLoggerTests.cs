using System.Text.Json;
using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests;

/// <summary>End-to-end tests of a <see cref="Logger"/> built from <see cref="LoggerOptions"/>.</summary>
public sealed class FileLoggerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Logger_WritesJsonLinesToConfiguredPath()
    {
        var path = _temp.Combine("logs", "application.log");

        using (var logger = new Logger(new LoggerOptions { FilePath = path, MinimumLevel = LogLevel.Debug }))
        {
            logger.Debug("Debugging");
            logger.Info("Application started");
            logger.Error("Database connection failed", new InvalidOperationException("Connection refused"));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(3, lines.Length);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out var timestamp));
            Assert.Equal(TimeSpan.Zero, timestamp.Offset);
            Assert.False(string.IsNullOrEmpty(root.GetProperty("level").GetString()));
            Assert.False(string.IsNullOrEmpty(root.GetProperty("message").GetString()));
        }

        using var error = JsonDocument.Parse(lines[2]);
        Assert.Equal("Error", error.RootElement.GetProperty("level").GetString());
        Assert.Equal("Connection refused", error.RootElement.GetProperty("exception").GetProperty("message").GetString());
    }

    [Fact]
    public void Logger_AppliesConfiguredMinimumLevel()
    {
        var path = _temp.Combine("app.log");

        using (var logger = new Logger(new LoggerOptions { FilePath = path, MinimumLevel = LogLevel.Warning }))
        {
            logger.Debug("ignored");
            logger.Info("ignored");
            logger.Warning("written");
            logger.Error("written");
        }

        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Fact]
    public void Logger_TextFormat_WritesTextLines()
    {
        var path = _temp.Combine("app.log");

        using (var logger = new Logger(new LoggerOptions { FilePath = path, Format = LogFormat.Text }))
        {
            logger.Info("Application started");
        }

        Assert.EndsWith(" [Info] Application started", Assert.Single(File.ReadAllLines(path)));
    }

    [Fact]
    public void Logger_Disabled_DoesNotCreateFileAndIsNotEnabled()
    {
        var path = _temp.Combine("logs", "app.log");

        using var logger = new Logger(new LoggerOptions { FilePath = path, Enabled = false });
        logger.Critical("discarded");

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(_temp.Combine("logs")));
    }

    [Fact]
    public void Logger_InvalidOptions_ThrowsBeforeTouchingTheFileSystem()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Logger(new LoggerOptions { FilePath = _temp.Combine("app.log"), MinimumLevel = (LogLevel)42 }));

        Assert.Empty(_temp.FileNames());
    }

    [Fact]
    public void Dispose_ReleasesTheFile()
    {
        var path = _temp.Combine("app.log");
        var logger = new Logger(new LoggerOptions { FilePath = path });
        logger.Info("message");

        logger.Dispose();

        // Exclusive access only succeeds if the logger closed its handle.
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void SecondLoggerOnSameFile_FailsToOpenInsteadOfCorruptingIt()
    {
        var options = new LoggerOptions { FilePath = _temp.Combine("app.log") };
        using var first = new Logger(options);

        Assert.Throws<IOException>(() => new Logger(options));
    }

    [Fact]
    public void Dispose_DoesNotDisposeCallerOwnedSink()
    {
        var sink = new InMemorySink();

        new Logger(sink).Dispose();

        Assert.False(sink.IsDisposed);
    }
}
