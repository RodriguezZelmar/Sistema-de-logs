using System.Text.Json;
using LoggingLibrary.Formatting;

namespace LoggingLibrary.Tests.Formatting;

public class JsonLogFormatterTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 1, 20, 30, 15, 432, TimeSpan.Zero);
    private readonly JsonLogFormatter _formatter = new();

    [Fact]
    public void Format_WritesTimestampLevelAndMessage()
    {
        var json = _formatter.Format(new LogEntry(Timestamp, LogLevel.Info, "Application started"));

        Assert.Equal(
            """{"timestamp":"2026-10-01T20:30:15.432Z","level":"Info","message":"Application started"}""",
            json);
    }

    [Fact]
    public void Format_WithoutException_OmitsExceptionProperty()
    {
        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(Timestamp, LogLevel.Error, "failed")));

        Assert.False(doc.RootElement.TryGetProperty("exception", out _));
    }

    [Fact]
    public void Format_TimestampIsUtcWithMillisecondPrecision()
    {
        var withTicks = new DateTimeOffset(2026, 10, 1, 17, 30, 15, TimeSpan.FromHours(-3)).AddTicks(4_329_999);

        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(withTicks, LogLevel.Info, "m")));

        Assert.Equal("2026-10-01T20:30:15.432Z", doc.RootElement.GetProperty("timestamp").GetString());
    }

    [Fact]
    public void Format_WithThrownException_IncludesTypeMessageAndStackTrace()
    {
        var exception = Throw(() => throw new InvalidOperationException("Connection refused"));

        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(Timestamp, LogLevel.Error, "Database connection failed", exception)));

        var ex = doc.RootElement.GetProperty("exception");
        Assert.Equal("System.InvalidOperationException", ex.GetProperty("type").GetString());
        Assert.Equal("Connection refused", ex.GetProperty("message").GetString());
        Assert.Contains(nameof(Format_WithThrownException_IncludesTypeMessageAndStackTrace), ex.GetProperty("stackTrace").GetString());
        Assert.False(ex.TryGetProperty("innerException", out _));
    }

    [Fact]
    public void Format_WithInnerException_NestsIt()
    {
        var exception = new InvalidOperationException("outer", new TimeoutException("inner"));

        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(Timestamp, LogLevel.Error, "m", exception)));

        var inner = doc.RootElement.GetProperty("exception").GetProperty("innerException");
        Assert.Equal("System.TimeoutException", inner.GetProperty("type").GetString());
        Assert.Equal("inner", inner.GetProperty("message").GetString());
    }

    [Fact]
    public void Format_ExceptionNeverThrown_OmitsStackTrace()
    {
        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(Timestamp, LogLevel.Error, "m", new Exception("x"))));

        Assert.False(doc.RootElement.GetProperty("exception").TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public void Format_MessageWithLineBreaksAndQuotes_StaysOnOneLineAndRoundTrips()
    {
        const string message = "line 1\nline 2\r\n\"quoted\" \\ tab\t";

        var json = _formatter.Format(new LogEntry(Timestamp, LogLevel.Info, message));

        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain('\r', json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(message, doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void Format_NonAsciiText_IsNotEscaped()
    {
        var json = _formatter.Format(new LogEntry(Timestamp, LogLevel.Info, "Conexión rechazada"));

        Assert.Contains("Conexión rechazada", json);
    }

    [Theory]
    [InlineData(LogLevel.Debug, "Debug")]
    [InlineData(LogLevel.Info, "Info")]
    [InlineData(LogLevel.Warning, "Warning")]
    [InlineData(LogLevel.Error, "Error")]
    [InlineData(LogLevel.Critical, "Critical")]
    public void Format_WritesLevelName(LogLevel level, string expected)
    {
        using var doc = JsonDocument.Parse(_formatter.Format(new LogEntry(Timestamp, level, "m")));

        Assert.Equal(expected, doc.RootElement.GetProperty("level").GetString());
    }

    private static Exception Throw(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }
        throw new InvalidOperationException("The action did not throw.");
    }
}
