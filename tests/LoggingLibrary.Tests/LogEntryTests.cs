namespace LoggingLibrary.Tests;

public class LogEntryTests
{
    [Fact]
    public void Constructor_ConvertsTimestampToUtc()
    {
        var local = new DateTimeOffset(2026, 10, 1, 17, 30, 0, TimeSpan.FromHours(-3));

        var entry = new LogEntry(local, LogLevel.Info, "message");

        Assert.Equal(TimeSpan.Zero, entry.Timestamp.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 20, 30, 0, TimeSpan.Zero), entry.Timestamp);
    }

    [Fact]
    public void Constructor_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LogEntry(DateTimeOffset.UtcNow, LogLevel.Info, null!));
    }
}
