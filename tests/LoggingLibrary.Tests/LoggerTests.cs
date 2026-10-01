using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests;

public class LoggerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 20, 30, 15, 432, TimeSpan.Zero);

    [Fact]
    public void Log_CreatesEntryWithTimestampLevelMessageAndException()
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink, LogLevel.Debug, new FixedTimeProvider(Now));
        var exception = new InvalidOperationException("boom");

        logger.Log(LogLevel.Error, "Something failed", exception);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(Now, entry.Timestamp);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Something failed", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void Log_WithSystemClock_UsesCurrentUtcTime()
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink);

        var before = DateTimeOffset.UtcNow;
        logger.Info("message");
        var after = DateTimeOffset.UtcNow;

        var entry = Assert.Single(sink.Entries);
        Assert.InRange(entry.Timestamp, before, after);
        Assert.Equal(TimeSpan.Zero, entry.Timestamp.Offset);
    }

    [Fact]
    public void ExtensionMethods_MapToTheirLevels()
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink);
        var exception = new Exception("x");

        logger.Debug("d");
        logger.Info("i");
        logger.Warning("w", exception);
        logger.Error("e", exception);
        logger.Critical("c", exception);

        Assert.Equal(
            [(LogLevel.Debug, "d", false), (LogLevel.Info, "i", false), (LogLevel.Warning, "w", true),
             (LogLevel.Error, "e", true), (LogLevel.Critical, "c", true)],
            sink.Entries.Select(e => (e.Level, e.Message, e.Exception is not null)));
    }

    [Fact]
    public void Log_NullMessage_Throws()
    {
        var logger = new Logger(new InMemorySink());

        Assert.Throws<ArgumentNullException>(() => logger.Log(LogLevel.Info, null!));
    }

    [Fact]
    public void Constructor_NullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Logger(null!));
    }
}
