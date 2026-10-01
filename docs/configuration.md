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
| `MaxFileSizeBytes` | `long` | `10_000_000` (10 MB) | Size limit of the active file. A write that would exceed it rotates the file first. |
| `MaxRetainedFiles` | `int` | `5` | Rotated files kept besides the active one (`application.1.log` newest ... `application.N.log` oldest). The oldest is deleted when a new archive would exceed the count. `0` keeps no history. |

| `OnWriteError` | `Action<LogWriteException>?` | `null` (print to stderr) | Receives write failures (disk full, rotation blocked, logging after dispose) together with the unwritten entry. Logging calls do not throw because of them unless this handler throws. Code only; cannot come from a configuration file. |

### Write-error handler example

```csharp
var options = new LoggerOptions
{
    OnWriteError = error =>
        File.AppendAllText("fallback.log", $"{error.Message}: {error.Entry.Message}\n"),
};
```

The handler runs on the thread that was logging, possibly on several threads
at once, so it must be thread-safe. To make logging failures throw instead:
`OnWriteError = error => throw error`. See
[architecture.md](architecture.md#error-handling) for the full policy.

### Rotation example

```csharp
var options = new LoggerOptions
{
    FilePath = "logs/application.log",
    MaxFileSizeBytes = 1_000_000,
    MaxRetainedFiles = 3,
};
```

produces at most four files, together never much more than 4 MB:

```text
logs/application.log      active file
logs/application.1.log    most recent archive
logs/application.2.log
logs/application.3.log    oldest archive, deleted on the next rotation
```

A single entry larger than `MaxFileSizeBytes` is never split: it is written
whole into a new file. The full algorithm is in
[architecture.md](architecture.md#rotation-strategy).

### About relative paths

A relative `FilePath` is resolved against `Environment.CurrentDirectory` when
the logger is created. For console apps that is normally where the app was
started from; for services it may be a system folder. When in doubt, build an
absolute path, for example
`Path.Combine(AppContext.BaseDirectory, "logs", "application.log")`.

## Configuration files (`appsettings.json`)

Options can also be read from any `IConfiguration` source (JSON files,
environment variables, command line...) with `LoggerOptions.FromConfiguration`:

```json
{
  "LoggingLibrary": {
    "MinimumLevel": "Info",
    "FilePath": "logs/application.log",
    "Format": "Json",
    "MaxFileSizeBytes": 10000000,
    "MaxRetainedFiles": 5
  }
}
```

```csharp
using Microsoft.Extensions.Configuration;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")          // needs Microsoft.Extensions.Configuration.Json
    .Build();

var options = LoggerOptions.FromConfiguration(configuration.GetSection("LoggingLibrary"));
options.OnWriteError = ...;                   // code-only settings can still be added
using var logger = new Logger(options);
```

Rules:

- Pass the section that **directly contains** the keys. Any section name
  works; a dedicated name such as `LoggingLibrary` is recommended because
  ASP.NET Core already uses `Logging` for `Microsoft.Extensions.Logging`
  (`Logging:LogLevel:...`), and those keys would be rejected as unknown.
- Keys and enum values are case-insensitive (`"minimumLevel": "warning"` works).
- Absent keys keep their default values; an absent section gives all defaults.
- **Unknown keys are an error**, so a typo such as `MaxFileSize` is reported
  instead of being silently ignored.
- Unparsable values (`"MinimumLevel": "Verbose"`, `"MaxFileSizeBytes": "10MB"`)
  throw an `ArgumentException` whose message includes the configuration path
  (`LoggingLibrary:MaxFileSizeBytes`) and the expected format.
- The result is validated with `Validate()` before it is returned.
- `OnWriteError` is a delegate and cannot come from configuration.

The library depends only on `Microsoft.Extensions.Configuration.Abstractions`
(the `IConfiguration` interface). Which providers to use (JSON, environment
variables, ...) is up to the application.

## Validation

`Logger` calls `LoggerOptions.Validate()` in its constructor (it can also be
called directly). Invalid options throw an `ArgumentException` (or the derived
`ArgumentOutOfRangeException`) whose `ParamName` is the option's name:

| Option | Rule |
|---|---|
| `MinimumLevel` | Must be a defined `LogLevel` value. |
| `Format` | Must be a defined `LogFormat` value. |
| `FilePath` | Must not be null, empty or whitespace, and must be a syntactically valid path. |
| `MaxFileSizeBytes` | Must be greater than 0. |
| `MaxRetainedFiles` | Must be between 0 and 1000 (`FileLogSink.MaxRetainedFilesLimit`). |

Options are validated even when `Enabled` is `false`, so a configuration
mistake is not hidden until logging is turned on.

## Lifetime

The logger copies the options when it is created; changing the
`LoggerOptions` object afterwards has no effect. The file is opened in the
constructor and closed by `Logger.Dispose()`. Create one logger per log file
and share it (it is thread-safe).
