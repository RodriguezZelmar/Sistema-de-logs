# LoggingLibrary

A small, reusable logging library for C#/.NET.

> **Status: early development.** The core API (levels, filtering, sink
> abstraction), JSON/text formatting, file output and code-based configuration
> and size-based rotation exist. This README only
> describes what actually exists; planned work is listed under
> [Roadmap](#roadmap).

## Goal

Provide a library (not an application) with a clear public API that other
.NET projects can reference to write structured logs:

- log levels with minimum-level filtering
- one JSON object per line (JSON Lines / NDJSON)
- file output with size-based rotation
- configuration from code (and possibly from `appsettings.json`)
- safe usage from multiple threads
- an extensible sink abstraction, so the core logger does not depend on the
  file implementation

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
interleaved or lost; when a `Log` call returns, the entry is in the file.
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

## Project structure

```text
.
├── src/LoggingLibrary/            The library (class library, future NuGet package)
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

## Roadmap

Each stage is built, tested, documented and committed separately.

- [x] Solution and project structure
- [x] `LogLevel`, `LogEntry`, `ILogger`, `ILogSink` and minimum-level filtering
- [x] JSON (NDJSON) and plain-text formatting
- [x] File sink
- [x] Configuration (`LoggerOptions`)
- [x] Size-based file rotation
- [x] Thread safety verification
- [x] Write-error handling policy
- [x] External configuration (`IConfiguration`)
- [x] Complete test suite
- [ ] Sample application
- [ ] NuGet packaging
- [ ] Final review of architecture, docs and code quality

See [docs/architecture.md](docs/architecture.md) for design decisions.

## License

[MIT](LICENSE)
