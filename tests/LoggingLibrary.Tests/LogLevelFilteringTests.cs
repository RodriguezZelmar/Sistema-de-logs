using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests;

public class LogLevelFilteringTests
{
    public static TheoryData<LogLevel, LogLevel> AllLevelPairs()
    {
        var data = new TheoryData<LogLevel, LogLevel>();
        foreach (var minimum in Enum.GetValues<LogLevel>())
        {
            foreach (var level in Enum.GetValues<LogLevel>())
            {
                data.Add(minimum, level);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllLevelPairs))]
    public void Log_WritesOnlyLevelsAtOrAboveMinimum(LogLevel minimum, LogLevel level)
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink, minimum);

        logger.Log(level, "message");

        var expectedCount = level >= minimum ? 1 : 0;
        Assert.Equal(expectedCount, sink.Entries.Count);
    }

    [Theory]
    [MemberData(nameof(AllLevelPairs))]
    public void IsEnabled_MatchesMinimumLevel(LogLevel minimum, LogLevel level)
    {
        var logger = new Logger(new InMemorySink(), minimum);

        Assert.Equal(level >= minimum, logger.IsEnabled(level));
    }

    [Fact]
    public void WarningMinimum_IgnoresDebugAndInfo_WritesWarningAndAbove()
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink, LogLevel.Warning);

        logger.Debug("debug");
        logger.Info("info");
        logger.Warning("warning");
        logger.Error("error");
        logger.Critical("critical");

        Assert.Equal(
            [LogLevel.Warning, LogLevel.Error, LogLevel.Critical],
            sink.Entries.Select(e => e.Level));
    }

    [Fact]
    public void DefaultMinimum_WritesDebug()
    {
        var sink = new InMemorySink();
        var logger = new Logger(sink);

        logger.Debug("debug");

        Assert.Single(sink.Entries);
    }

    [Fact]
    public void Error_IsWrittenWithEveryMinimumUpToError()
    {
        foreach (var minimum in new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error })
        {
            var sink = new InMemorySink();
            new Logger(sink, minimum).Error("error");
            Assert.Single(sink.Entries);
        }
    }
}
