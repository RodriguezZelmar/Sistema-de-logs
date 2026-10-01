# LoggingLibrary

A small, reusable logging library for C#/.NET 10.

It is a library, not an application: other .NET projects reference it (as a
project or as a NuGet package) and get a logger with a small public API,
structured output and predictable behavior under failures and concurrency.

## Features

- **Log levels** `Debug`, `Info`, `Warning`, `Error`, `Critical`, with
  minimum-level filtering and `IsEnabled` to skip expensive messages.
- **JSON Lines output** (one JSON object per line) via `System.Text.Json`,
  including exception details; optional plain-text format.
- **UTC timestamps** in ISO 8601 with millisecond precision.
- **File logging** with automatic directory creation.
- **Size-based rotation** with a configurable number of retained files.
- **Configuration** from code (`LoggerOptions`) or from `appsettings.json` /
  any `IConfiguration`, with validation.
- **Thread-safe**: one logger can be shared by every thread.
- **Explicit error policy**: startup problems throw; write failures while
  running never crash the caller and never disappear silently.
- **Extensible**: `Logger` writes through `ILogSink`, so new destinations
  don't require changing the logger.
- **Tested** (xUnit) and **packable** as a NuGet package.

## Usage

```csharp
using LoggingLibrary;

using var logger = new Logger(new LoggerOptions
{
    MinimumLevel = LogLevel.Info,
    FilePath = "logs/application.log",
});

logger.Info("Application started");
logger.Warning("Low disk space");
logger.Error("Could not connect to database", exception);
```

The log directory is created if it does not exist. Each entry is appended
and flushed immediately. Application code should depend on `ILogger`, which
`Logger` implements.

## Configuration

| Option | Default | Description |
|---|---|---|
| `Enabled` | `true` | Turns logging off entirely (no file is created). |
| `MinimumLevel` | `Info` | Entries below this level are discarded. |
| `FilePath` | `logs/application.log` | Log file path; directories are created. |
| `Format` | `Json` | `Json` (JSON Lines) or `Text`. |
| `MaxFileSizeBytes` | `10_000_000` | Size at which the file is rotated. |
| `MaxRetainedFiles` | `5` | Rotated files kept besides the active one. |
| `OnWriteError` | `null` (stderr) | Receives entries that could not be written. |

Invalid options throw `ArgumentException` when the logger is created.

Options can also come from `appsettings.json` (or any `IConfiguration`):

```json
{
  "LoggingLibrary": {
    "MinimumLevel": "Info",
    "FilePath": "logs/application.log",
    "MaxFileSizeBytes": 10000000,
    "MaxRetainedFiles": 5
  }
}
```

```csharp
var options = LoggerOptions.FromConfiguration(configuration.GetSection("LoggingLibrary"));
using var logger = new Logger(options);
```

Unknown keys and invalid values are reported with their configuration path.
Details in [docs/configuration.md](docs/configuration.md).

### Custom sinks

`Logger` writes through the `ILogSink` interface. To send entries somewhere
else, implement `ILogSink` and pass it in:

```csharp
ILogger logger = new Logger(mySink, minimumLevel: LogLevel.Info);
```

## Log levels

From least to most severe:

| Level | Use for |
|---|---|
| `Debug` | Diagnostic detail while developing or troubleshooting |
| `Info` | Normal events (startup, completed operations) |
| `Warning` | Unexpected situations the application recovers from |
| `Error` | A failed operation |
| `Critical` | A failure that stops the application from working |

Entries below the configured minimum level are discarded. With a minimum of
`Warning`, `Debug` and `Info` are ignored and `Warning`, `Error` and `Critical`
are written. To avoid building expensive messages that would be discarded:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.Debug(BuildExpensiveDiagnostics());
}
```

## Output formats

`JsonLogFormatter` produces one JSON object per line (JSON Lines / NDJSON):

```json
{"timestamp":"2026-10-01T20:30:15.432Z","level":"Info","message":"Application started"}
{"timestamp":"2026-10-01T20:30:16.101Z","level":"Error","message":"Database connection failed","exception":{"type":"System.InvalidOperationException","message":"Connection refused","stackTrace":"   at ..."}}
```

`exception` is present only when an exception was logged; it also contains
`innerException` when there is one. `TextLogFormatter` produces
human-readable lines: `2026-10-01T20:30:15.432Z [Info] Application started`.

Timestamps are always UTC, ISO 8601, millisecond precision.

## Rotation

When writing an entry would make the active file larger than
`MaxFileSizeBytes`, the file is rotated first:

```text
application.log    → application.1.log   (newest archive)
application.1.log  → application.2.log
...
application.N.log  → deleted             (N = MaxRetainedFiles)
```

Entries are never split between files. See
[docs/architecture.md](docs/architecture.md#rotation-strategy) for the exact
rules.

## Error handling

- **Logging exceptions:** `logger.Error("message", exception)` records the
  exception's type, message, stack trace and inner exceptions.
- **Startup problems** (invalid options, directory cannot be created, file
  locked or not writable) throw from `new Logger(options)`, so they are
  found immediately.
- **Problems while running** (disk full, rotation blocked) never make a
  `Log` call throw. They are passed, together with the entry that was not
  written, to `LoggerOptions.OnWriteError`; by default both are printed to
  standard error so the information is not silently lost. The next write
  retries the file.

Full table of cases in
[docs/architecture.md](docs/architecture.md#error-handling).

## Thread safety

A single `Logger` instance can be shared by all threads. `FileLogSink`
serializes each write (including rotation) with a lock, so entries are never
interleaved or lost; when a `Log` call returns, the entry is in the file (or,
if writing failed, it was handed to the write-error handler).
Details in [docs/architecture.md](docs/architecture.md#concurrency).

## Requirements

- .NET SDK 10 (LTS). The exact SDK band is pinned in [`global.json`](global.json).

## Building and testing

```bash
dotnet build
```

```bash
dotnet test
```

## Sample application

[`samples/LoggingLibrary.Sample`](samples/LoggingLibrary.Sample/Program.cs) is
a separate console app that consumes the library the way any other project
would. It reads the options from its `appsettings.json`, writes entries of
several levels (one filtered out), a warning and an error with a nested
exception, then prints where the log file is and its last lines.

```bash
dotnet run --project samples/LoggingLibrary.Sample
```

```text
Logs written to: .../samples/LoggingLibrary.Sample/bin/Debug/net10.0/logs/application.log

{"timestamp":"...","level":"Info","message":"Sample application started"}
{"timestamp":"...","level":"Warning","message":"Disk space is below 10%"}
{"timestamp":"...","level":"Error","message":"Could not connect to the database","exception":{"type":"System.InvalidOperationException","message":"Database unavailable","stackTrace":"...","innerException":{"type":"System.TimeoutException","message":"The server did not respond within 5 seconds."}}}
{"timestamp":"...","level":"Info","message":"Sample application finished"}
```

## Testing

The xUnit suite in `tests/LoggingLibrary.Tests` tests behavior through the
public API, writing real files into a unique temporary directory per test.

| Area | Test class | What it verifies |
|---|---|---|
| Levels | `LogLevelFilteringTests` | Every minimum-level / level combination, `IsEnabled` |
| Logger | `LoggerTests`, `LogEntryTests` | Timestamps (UTC, injected clock), level shortcuts, argument checks |
| JSON / text | `JsonLogFormatterTests`, `TextLogFormatterTests` | Exact output, valid JSON, exceptions (type, message, stack trace, inner), escaping |
| File | `FileLogSinkTests`, `FileLoggerTests` | File and directory creation, appending, encoding, flushing, locked/invalid paths |
| Configuration | `LoggerOptionsTests`, `LoggerOptionsConfigurationTests` | Defaults, validation of every option, `appsettings.json` parsing and errors |
| Rotation | `FileLogSinkRotationTests` | Exact file names and contents after rotation, size limit, retention, oversized entries |
| Concurrency | `ConcurrencyTests` | Thousands of concurrent writes, with rotation: no exceptions, valid JSON, no lost or duplicated entries |
| Errors | `ErrorHandlingTests` | Write failures are reported with the entry, never thrown; recovery after a blocked rotation |

Rotation tests use a fixed clock so every line has a known size, which makes
the rotation points exact. Concurrency tests wait for threads to finish
(`Parallel.For`, `Thread.Join`) rather than sleeping.

To collect code coverage:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Packaging

The library is configured as a NuGet package (it is not published to
nuget.org). Only `src/LoggingLibrary` is packable; the tests and the sample
set `IsPackable=false`.

```bash
dotnet pack -c Release -o ./artifacts
```

This produces `LoggingLibrary.0.1.0.nupkg`, containing the assembly, its XML
documentation (IntelliSense) and this README, plus a `.snupkg` symbols
package for debugging. Metadata (`PackageId`, `Version`, `Authors`,
`Description`, `PackageTags`, MIT license) is in
[`LoggingLibrary.csproj`](src/LoggingLibrary/LoggingLibrary.csproj).
`RepositoryUrl` is left commented out until the repository is published.

To use the package from another project without publishing it, add the
output folder as a package source:

```bash
dotnet add package LoggingLibrary --version 0.1.0 --source ./artifacts
```

NuGet caches packages by version, so when repacking changes, increase
`Version` (or clear the `logginglibrary` folder in the global packages cache).
Before publishing to nuget.org the `PackageId` would need a unique prefix,
since `LoggingLibrary` is a generic name.

## Project structure

```text
.
├── src/LoggingLibrary/            The library (class library, NuGet package)
│   ├── Formatting/                ILogFormatter, JSON and text formatters
│   └── Sinks/                     ILogSink, FileLogSink
├── tests/LoggingLibrary.Tests/    xUnit tests
├── samples/LoggingLibrary.Sample/ Console app that consumes the library
├── docs/
│   ├── architecture.md            Components, design decisions
│   └── configuration.md           Configuration options
├── Directory.Build.props          Build settings shared by all projects
├── global.json                    SDK version pinning
└── LoggingLibrary.slnx            Solution file
```

Shared build settings (`Directory.Build.props`): target `net10.0`, nullable
reference types enabled, and warnings treated as errors. The library also
generates an XML documentation file, so every public member must be documented.

## Limitations and possible improvements

Deliberately out of scope for this version:

- **One process per log file.** A second writer (another process or another
  logger instance on the same path) fails to open the file.
- **Synchronous writes.** Each call writes and flushes before returning. A
  buffered/background sink would be faster at high volume, at the cost of
  losing queued entries on a crash; it could be added as another `ILogSink`.
- **Only size-based rotation**, no time-based rotation.
- **No structured properties** (`{"userId": 42}`) beyond message and
  exception. `LogEntry` was designed so they can be added without breaking
  the API.
- **No integration with `Microsoft.Extensions.Logging`** (`ILoggerProvider`).
  It could be added as a separate adapter package.
- **One sink per logger.** A composite sink would allow several destinations.

## Documentation

- [docs/architecture.md](docs/architecture.md): components, log flow, and
  the design decisions behind concurrency, rotation and error handling.
- [docs/configuration.md](docs/configuration.md): every option, validation
  rules and configuration files.

## License

[MIT](LICENSE)
