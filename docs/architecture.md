# Architecture

> This document is updated as each stage is implemented. Sections marked
> **Planned** describe intended design, not existing code.

## Solution layout

| Project | Type | Purpose |
|---|---|---|
| `src/LoggingLibrary` | Class library | The reusable library and its public API. Has no external package dependencies. |
| `tests/LoggingLibrary.Tests` | xUnit test project | Behavioral tests against the library's public API. |
| `samples/LoggingLibrary.Sample` | Console app | Shows how an external project consumes the library. Only references the library, like any other consumer would. |

The library targets `net10.0`, the current LTS release of .NET.

## Dependency policy

The library should rely on the .NET base class library (`System.Text.Json`,
`System.IO`, `System.Threading`). A package dependency is only added when it
removes real work for consumers — for example
`Microsoft.Extensions.Configuration` if binding from `appsettings.json` is
implemented. Test-only packages (xUnit) live in the test project and never
reach library consumers.

## Planned component design

**Planned.** The core logger will not depend on a concrete output:

```text
Logger  ──►  ILogSink  ──►  FileLogSink
(filtering,   (abstraction)   (formatting to file,
 LogEntry                      rotation)
 creation)
```

Adding a new destination (console, HTTP, ...) should mean adding a new
`ILogSink` implementation without modifying `Logger`. Only the file sink is
in scope for the first version.

The remaining sections (log flow, concurrency strategy, rotation strategy,
error handling) will be written together with their implementation.
