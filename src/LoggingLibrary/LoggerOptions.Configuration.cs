using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace LoggingLibrary;

public sealed partial class LoggerOptions
{
    private static readonly Dictionary<string, Action<LoggerOptions, IConfigurationSection>> Setters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Enabled)] = (o, s) => o.Enabled = ParseBool(s),
            [nameof(MinimumLevel)] = (o, s) => o.MinimumLevel = ParseEnum<LogLevel>(s),
            [nameof(FilePath)] = (o, s) => o.FilePath = RequireValue(s),
            [nameof(Format)] = (o, s) => o.Format = ParseEnum<LogFormat>(s),
            [nameof(MaxFileSizeBytes)] = (o, s) => o.MaxFileSizeBytes = ParseInteger(s, long.Parse),
            [nameof(MaxRetainedFiles)] = (o, s) => o.MaxRetainedFiles = ParseInteger(s, int.Parse),
        };

    /// <summary>
    /// Creates options from a configuration section, such as a section of
    /// <c>appsettings.json</c>. Keys that are absent keep their default value.
    /// </summary>
    /// <param name="section">
    /// The section that directly contains the option keys, e.g.
    /// <c>configuration.GetSection("LoggingLibrary")</c>. Keys are case-insensitive.
    /// </param>
    /// <returns>Validated options. <see cref="OnWriteError"/> can still be set in code.</returns>
    /// <exception cref="ArgumentException">
    /// A key is unknown, a value cannot be parsed, or the resulting options are invalid.
    /// The message includes the configuration path of the offending key.
    /// </exception>
    /// <example>
    /// <code>
    /// // appsettings.json: { "LoggingLibrary": { "MinimumLevel": "Warning", "FilePath": "logs/app.log" } }
    /// var options = LoggerOptions.FromConfiguration(configuration.GetSection("LoggingLibrary"));
    /// </code>
    /// </example>
    public static LoggerOptions FromConfiguration(IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var options = new LoggerOptions();
        foreach (var child in section.GetChildren())
        {
            if (!Setters.TryGetValue(child.Key, out var setter))
            {
                // Rejecting unknown keys turns a typo such as "MaxFileSize"
                // into an error instead of a silently ignored setting.
                throw new ArgumentException(
                    $"Unknown logging option '{child.Path}'. Valid options: {string.Join(", ", Setters.Keys)}.",
                    nameof(section));
            }
            setter(options, child);
        }

        options.Validate();
        return options;
    }

    private static string RequireValue(IConfigurationSection setting) =>
        setting.Value ?? throw InvalidValue(setting, "a value");

    private static bool ParseBool(IConfigurationSection setting) =>
        bool.TryParse(RequireValue(setting), out var result) ? result : throw InvalidValue(setting, "true or false");

    private static T ParseEnum<T>(IConfigurationSection setting)
        where T : struct, Enum
    {
        var value = RequireValue(setting);
        // Enum.TryParse also accepts numbers; IsDefined rejects ones without a name.
        return Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : throw InvalidValue(setting, $"one of {string.Join(", ", Enum.GetNames<T>())}");
    }

    private static T ParseInteger<T>(IConfigurationSection setting, Func<string, NumberStyles, IFormatProvider, T> parse)
    {
        try
        {
            return parse(RequireValue(setting), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw InvalidValue(setting, "a whole number", ex);
        }
    }

    private static ArgumentException InvalidValue(IConfigurationSection setting, string expected, Exception? inner = null) =>
        new($"Invalid value '{setting.Value}' for logging option '{setting.Path}': expected {expected}.", "section", inner);
}
