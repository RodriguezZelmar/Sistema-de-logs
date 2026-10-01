using System.Text;
using Microsoft.Extensions.Configuration;

namespace LoggingLibrary.Tests;

public class LoggerOptionsConfigurationTests
{
    [Fact]
    public void FromConfiguration_ReadsAllOptionsFromJson()
    {
        const string json = """
            {
              "LoggingLibrary": {
                "Enabled": false,
                "MinimumLevel": "Warning",
                "FilePath": "custom/app.log",
                "Format": "Text",
                "MaxFileSizeBytes": 2048,
                "MaxRetainedFiles": 7
              }
            }
            """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        var options = LoggerOptions.FromConfiguration(configuration.GetSection("LoggingLibrary"));

        Assert.False(options.Enabled);
        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Equal("custom/app.log", options.FilePath);
        Assert.Equal(LogFormat.Text, options.Format);
        Assert.Equal(2048, options.MaxFileSizeBytes);
        Assert.Equal(7, options.MaxRetainedFiles);
    }

    [Fact]
    public void FromConfiguration_MissingKeys_KeepDefaults()
    {
        var options = LoggerOptions.FromConfiguration(Section(("MinimumLevel", "Error")));

        var defaults = new LoggerOptions();
        Assert.Equal(LogLevel.Error, options.MinimumLevel);
        Assert.Equal(defaults.FilePath, options.FilePath);
        Assert.Equal(defaults.MaxFileSizeBytes, options.MaxFileSizeBytes);
        Assert.Equal(defaults.MaxRetainedFiles, options.MaxRetainedFiles);
        Assert.Equal(defaults.Format, options.Format);
        Assert.True(options.Enabled);
    }

    [Fact]
    public void FromConfiguration_EmptySection_GivesDefaults()
    {
        var options = LoggerOptions.FromConfiguration(new ConfigurationBuilder().Build().GetSection("Missing"));

        Assert.Equal(new LoggerOptions().MinimumLevel, options.MinimumLevel);
    }

    [Fact]
    public void FromConfiguration_KeysAndEnumValuesAreCaseInsensitive()
    {
        var options = LoggerOptions.FromConfiguration(Section(("minimumlevel", "debug"), ("FORMAT", "text")));

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
        Assert.Equal(LogFormat.Text, options.Format);
    }

    [Theory]
    [InlineData("MinimumLevel", "Verbose")]
    [InlineData("MinimumLevel", "42")]
    [InlineData("Format", "Xml")]
    [InlineData("Enabled", "yes")]
    [InlineData("MaxFileSizeBytes", "10MB")]
    [InlineData("MaxFileSizeBytes", "99999999999999999999")]
    [InlineData("MaxRetainedFiles", "1.5")]
    public void FromConfiguration_UnparsableValue_ThrowsWithPath(string key, string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => LoggerOptions.FromConfiguration(Section((key, value))));

        Assert.Contains($"LoggingLibrary:{key}", ex.Message);
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void FromConfiguration_UnknownKey_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => LoggerOptions.FromConfiguration(Section(("MaxFileSize", "100"))));

        Assert.Contains("LoggingLibrary:MaxFileSize", ex.Message);
    }

    [Fact]
    public void FromConfiguration_OutOfRangeValue_FailsValidation()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoggerOptions.FromConfiguration(Section(("MaxRetainedFiles", "-1"))));

        Assert.Equal(nameof(LoggerOptions.MaxRetainedFiles), ex.ParamName);
    }

    private static IConfigurationSection Section(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>($"LoggingLibrary:{s.Key}", s.Value)))
            .Build()
            .GetSection("LoggingLibrary");
}
