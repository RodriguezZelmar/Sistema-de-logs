# Configuration

A file logger is configured with a `LoggerOptions` instance:

```csharp
using LoggingLibrary;

using var logger = new Logger(new LoggerOptions
{
    MinimumLevel = LogLevel.Info,
    FilePath = "logs/application.log",
    Format = LogFormat.Json,
});
```

Every option has a default, so `new Logger(new LoggerOptions())` is a valid,
working configuration.

## Options

| Option | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | When `false`, every entry is discarded, `IsEnabled` returns `false` for all levels, and the log file is not created or opened. |
| `MinimumLevel` | `LogLevel` | `Info` | Entries below this level are discarded. Order: `Debug < Info < Warning < Error < Critical`. |
| `FilePath` | `string` | `logs/application.log` | Path of the log file. Relative paths are resolved against the process's current directory. Missing directories are created. |
| `Format` | `LogFormat` | `Json` | `Json`: one JSON object per line (JSON Lines). `Text`: `timestamp [Level] message` lines. |

### About relative paths

A relative `FilePath` is resolved against `Environment.CurrentDirectory` when
the logger is created. For console apps that is normally where the app was
started from; for services it may be a system folder. When in doubt, build an
absolute path, for example
`Path.Combine(AppContext.BaseDirectory, "logs", "application.log")`.

## Validation

`Logger` calls `LoggerOptions.Validate()` in its constructor (it can also be
called directly). Invalid options throw an `ArgumentException` (or the derived
`ArgumentOutOfRangeException`) whose `ParamName` is the option's name:

| Option | Rule |
|---|---|
| `MinimumLevel` | Must be a defined `LogLevel` value. |
| `Format` | Must be a defined `LogFormat` value. |
| `FilePath` | Must not be null, empty or whitespace, and must be a syntactically valid path. |

Options are validated even when `Enabled` is `false`, so a configuration
mistake is not hidden until logging is turned on.

## Lifetime

The logger copies the options when it is created; changing the
`LoggerOptions` object afterwards has no effect. The file is opened in the
constructor and closed by `Logger.Dispose()`. Create one logger per log file
and share it (it is thread-safe).
