# LoggingLibrary

A small, reusable logging library for C#/.NET.

> **Status: early development.** The core API (levels, filtering, sink
> abstraction) and the JSON/text formatters exist, but there is no built-in
> sink (output destination) yet. This README only
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

## Usage (current API)

```csharp
using LoggingLibrary;
using LoggingLibrary.Sinks;

ILogSink sink = /* any ILogSink implementation */;
ILogger logger = new Logger(sink, minimumLevel: LogLevel.Info);

logger.Info("Application started");
logger.Warning("Low disk space");
logger.Error("Could not connect to database", exception);
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

## Requirements

- .NET SDK 10 (LTS). The exact SDK band is pinned in [`global.json`](global.json).

## Building and testing

```bash
dotnet build
```

```bash
dotnet test
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
- [ ] File sink
- [ ] Configuration (`LoggerOptions`)
- [ ] Size-based file rotation
- [ ] Thread safety verification
- [ ] Write-error handling policy
- [ ] External configuration (`IConfiguration`)
- [ ] Sample application
- [ ] NuGet packaging
- [ ] Final review of architecture, docs and code quality

See [docs/architecture.md](docs/architecture.md) for design decisions.

## License

[MIT](LICENSE)
