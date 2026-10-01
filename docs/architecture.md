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

## Components

```text
caller ──► ILogger ──► Logger ──► ILogSink ──► (concrete sink)
           (+ LoggerExtensions:    filters by level,
            Info, Error, ...)      timestamps, builds LogEntry
```

| Type | Namespace | Responsibility |
|---|---|---|
| `LogLevel` | `LoggingLibrary` | Ordered severity: `Debug < Info < Warning < Error < Critical`. |
| `LogEntry` | `LoggingLibrary` | Immutable event: UTC timestamp, level, message, optional exception. |
| `ILogger` | `LoggingLibrary` | What application code depends on: `IsEnabled(level)` and `Log(level, message, exception)`. |
| `LoggerExtensions` | `LoggingLibrary` | `Debug`, `Info`, `Warning`, `Error`, `Critical` shortcuts over `ILogger.Log`. |
| `Logger` | `LoggingLibrary` | Default `ILogger`: filters by minimum level, timestamps and forwards to one `ILogSink`. |
| `ILogSink` | `LoggingLibrary.Sinks` | A destination for entries. Must be thread-safe. |
| `ILogFormatter` | `LoggingLibrary.Formatting` | Turns a `LogEntry` into one line of text (no trailing line break). |
| `JsonLogFormatter` | `LoggingLibrary.Formatting` | Single-line JSON object per entry (JSON Lines / NDJSON). |
| `TextLogFormatter` | `LoggingLibrary.Formatting` | Human-readable `timestamp [Level] message` lines. |
| `FileLogSink` | `LoggingLibrary.Sinks` | Appends formatted entries to a file, one per line. |

### Why these abstractions

- **`ILogger`** lets application code be tested with a fake logger and keeps
  it independent from how logs are written. It has only two members; the
  level shortcuts are extension methods, so a custom implementation (or a test
  fake) does not have to repeat them.
- **`ILogSink`** decouples *what to log* (filtering, timestamps — `Logger`)
  from *where it goes*. A new destination (console, HTTP...) is a new
  `ILogSink`; `Logger` does not change. Only the sinks that are actually used
  are implemented.
- **`ILogFormatter`** separates *how an entry looks* from *where it is
  written*. There are two real implementations (JSON and text), selected by
  configuration, and a text-based sink only needs the interface.
- **`LogEntry`** is a sealed class with read-only properties instead of a
  positional record, so fields (for example structured properties) can be
  added later without breaking callers.

### Log levels

`Debug`, `Info`, `Warning` and `Error` are the core levels. `Critical` was
added because "this operation failed" (`Error`) and "the application cannot
continue" (`Critical`) usually trigger different reactions (retry vs. alert
someone), and a log reader benefits from being able to search for the second
one specifically. `Trace` was left out: with a single destination and no
per-component configuration, a level below `Debug` would add API surface
without a clear use.

Filtering is a single comparison (`level >= minimumLevel`), which works because
the enum values are ordered by severity.

### Avoiding unnecessary work

`Logger.Log` checks the level before creating the `LogEntry` or reading the
clock, so a disabled call costs one comparison. When *building the message* is
the expensive part, callers can guard it with `IsEnabled`:

```csharp
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.Debug(BuildExpensiveDiagnostics());
}
```

A lambda-based overload (`logger.Debug(() => ...)`) was considered and not
added: `IsEnabled` covers the same need without a delegate allocation per call
and without doubling the number of methods.

### Timestamps

Timestamps are `DateTimeOffset` values taken from `TimeProvider.GetUtcNow()`
and stored in UTC (`LogEntry` converts any offset to UTC). UTC avoids
ambiguity when servers run in different time zones and around daylight-saving
changes. `TimeProvider` is the standard .NET clock abstraction; injecting it
lets tests use a fixed time.

Formatters write the timestamp as ISO 8601 UTC with millisecond precision and
a `Z` suffix (`2026-10-01T20:30:15.432Z`), using the invariant culture. The
string always has the same length and sorts chronologically as text.

### JSON format

`JsonLogFormatter` writes each entry as one JSON object on one line (JSON
Lines / NDJSON), never a single array for the whole file. This lets a file be
appended to without rewriting it, read line by line without loading it
whole, and still be parsed if the last line was cut off by a crash.

- Written with `Utf8JsonWriter` (System.Text.Json) instead of serializing a
  DTO, to control the property order, omit absent fields and format the
  timestamp exactly.
- Properties: `timestamp`, `level`, `message`, and `exception` only when
  there is one. The exception object has `type` (full type name), `message`,
  `stackTrace` (omitted when the exception was never thrown) and
  `innerException` (same shape, nested). `AggregateException` contents beyond
  its first inner exception are not expanded.
- Line breaks and quotes inside strings are escaped, so a multi-line message
  cannot break the one-entry-per-line rule. Non-ASCII characters are kept
  as-is (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`) for readability; that
  encoder is only "unsafe" when the output is embedded in HTML.

The text format puts the exception (`Exception.ToString()`) on the following
lines, so it is meant for people, not for parsing.

### File sink

`FileLogSink` turns each entry into a line with its `ILogFormatter` and
appends it to the file.

- **Opened once, kept open.** The file is opened in the constructor
  (`FileMode.Append`) and reused for every write, instead of opening and
  closing it per entry. Opening eagerly also means an invalid or inaccessible
  path fails at startup, not at the first log call.
- **Directories are created** (`Directory.CreateDirectory`) if they do not
  exist yet.
- **Flushed on every write** to the operating system. Entries survive a
  process that ends without disposing the logger. (This is not an `fsync`:
  a power loss can still lose the last entries.)
- **Encoding:** UTF-8 without BOM, `\n` line endings on every OS, so files
  are identical across platforms and valid JSON Lines.
- **Sharing:** the file is opened with `FileShare.Read`: other processes can
  read (tail) it, but a second writer cannot open it. One sink per file.
- **After a failed write** the stream is closed and the next write reopens
  the file, so a transient error does not leave the sink permanently broken.

### Thread safety

`Logger` has no mutable state, so concurrent calls are safe as long as the
sink is thread-safe, which the `ILogSink` contract requires. The built-in
formatters are stateless. `FileLogSink` serializes writes with a lock; see
[Concurrency](#concurrency) once that stage is documented.

### Naming

`ILogger` and `LogLevel` share names with types in
`Microsoft.Extensions.Logging`. They live in the `LoggingLibrary` namespace, so
there is no conflict unless a file imports both namespaces; in that case use an
alias, e.g. `using LogLevel = LoggingLibrary.LogLevel;`.

## Planned

**Planned.** Size-based rotation for the file sink,
configuration through `LoggerOptions`, and an explicit policy for write
errors. Their sections will be written together with their implementation.
