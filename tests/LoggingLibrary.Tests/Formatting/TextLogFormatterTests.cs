using LoggingLibrary.Formatting;

namespace LoggingLibrary.Tests.Formatting;

public class TextLogFormatterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 1, 20, 30, 15, 432, TimeSpan.Zero);
    private readonly TextLogFormatter _formatter = new();

    [Fact]
    public void Format_WritesTimestampLevelAndMessage()
    {
        var text = _formatter.Format(new LogEntry(Timestamp, LogLevel.Warning, "Low disk space"));

        Assert.Equal("2026-10-01T20:30:15.432Z [Warning] Low disk space", text);
    }

    [Fact]
    public void Format_WithException_AppendsExceptionOnNextLine()
    {
        var exception = new InvalidOperationException("boom");

        var text = _formatter.Format(new LogEntry(Timestamp, LogLevel.Error, "failed", exception));

        Assert.Equal($"2026-10-01T20:30:15.432Z [Error] failed\n{exception}", text);
    }
}
