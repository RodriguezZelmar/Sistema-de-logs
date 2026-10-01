namespace LoggingLibrary.Tests;

public class LoggerOptionsTests
{
    [Fact]
    public void Defaults_AreValidAndDocumented()
    {
        var options = new LoggerOptions();

        options.Validate();
        Assert.True(options.Enabled);
        Assert.Equal(LogLevel.Info, options.MinimumLevel);
        Assert.Equal("logs/application.log", options.FilePath);
        Assert.Equal(LogFormat.Json, options.Format);
        Assert.Equal(10_000_000, options.MaxFileSizeBytes);
        Assert.Equal(5, options.MaxRetainedFiles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveMaxFileSize_Throws(long size)
    {
        var options = new LoggerOptions { MaxFileSizeBytes = size };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.MaxFileSizeBytes), ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Validate_MaxRetainedFilesOutOfRange_Throws(int count)
    {
        var options = new LoggerOptions { MaxRetainedFiles = count };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.MaxRetainedFiles), ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Validate_MaxRetainedFilesBoundaries_AreValid(int count)
    {
        new LoggerOptions { MaxRetainedFiles = count }.Validate();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Validate_EmptyFilePath_Throws(string? path)
    {
        var options = new LoggerOptions { FilePath = path! };

        var ex = Assert.Throws<ArgumentException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.FilePath), ex.ParamName);
    }

    [Fact]
    public void Validate_InvalidPathCharacters_Throws()
    {
        var options = new LoggerOptions { FilePath = "logs/\0app.log" };

        var ex = Assert.Throws<ArgumentException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.FilePath), ex.ParamName);
    }

    [Fact]
    public void Validate_UndefinedMinimumLevel_Throws()
    {
        var options = new LoggerOptions { MinimumLevel = (LogLevel)42 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.MinimumLevel), ex.ParamName);
    }

    [Fact]
    public void Validate_UndefinedFormat_Throws()
    {
        var options = new LoggerOptions { Format = (LogFormat)42 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
        Assert.Equal(nameof(LoggerOptions.Format), ex.ParamName);
    }
}
