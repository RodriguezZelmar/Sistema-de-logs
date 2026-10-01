# Architecture

## Solution layout

| Project | Type | Purpose |
|---|---|---|
| `src/LoggingLibrary` | Class library | The reusable library and its public API. Its only package dependency is `Microsoft.Extensions.Configuration.Abstractions`. |
| `tests/LoggingLibrary.Tests` | xUnit test project | Behavioral tests against the library's public API. |
| `samples/LoggingLibrary.Sample` | Console app | Shows how an external project consumes the library. Only references the library, like any other consumer would. |

The library targets `net10.0`, the current LTS release of .NET.

## Dependency policy

The library relies on the .NET base class library (`System.Text.Json`,
`System.IO`, `System.Threading`). A package dependency is only added when it
removes real work for consumers.

| Package | Used by | Why |
|---|---|---|
| `Microsoft.Extensions.Configuration.Abstractions` | library | Accept an `IConfiguration` section in `LoggerOptions.FromConfiguration`, the standard way .NET apps pass settings. It only contains interfaces; the application chooses the providers (JSON, environment variables...). |
| `Microsoft.Extensions.Configuration`, `.Json` | tests, sample | Build real configurations from JSON in tests and in the sample. Not a library dependency. |
| `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | tests | Test framework, runner and coverage collection. |

`Microsoft.Extensions.Configuration.Binder` was not used: options are read
explicitly so that unknown keys and unparsable values produce errors that
name the configuration path, instead of being ignored.

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
| `LoggerOptions` | `LoggingLibrary` | Settings for a file logger, with defaults, `Validate()` and `FromConfiguration(IConfiguration)`. |
| `LogFormat` | `LoggingLibrary` | `Json` or `Text`; selects the formatter. |
| `LogWriteException` | `LoggingLibrary` | Reports an entry the sink could not write, with the cause. |
| `ILogSink` | `LoggingLibrary.Sinks` | A destination for entries. Must be thread-safe. |
| `ILogFormatter` | `LoggingLibrary.Formatting` | Turns a `LogEntry` into one line of text (no trailing line break). |
| `JsonLogFormatter` | `LoggingLibrary.Formatting` | Single-line JSON object per entry (JSON Lines / NDJSON). |
| `TextLogFormatter` | `LoggingLibrary.Formatting` | Human-readable `timestamp [Level] message` lines. |
| `FileLogSink` | `LoggingLibrary.Sinks` | Appends formatted entries to a file, one per line, with size-based rotation. |

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

### Creating a logger

`Logger` has two constructors:

- `Logger(LoggerOptions)` — the common case. Validates the options and, if
  logging is enabled, creates a `FileLogSink` with the formatter selected by
  `Format`. The logger **owns** that sink and closes it in `Dispose()`.
- `Logger(ILogSink, minimumLevel, timeProvider, onWriteError)` — for custom
  sinks and tests; all parameters but the sink are optional. The caller owns
  the sink; `Logger.Dispose()` does not dispose it.

`LoggerOptions` is a plain class with settable properties and defaults, so it
works with object initializers and can be bound from configuration. The
logger copies the values it needs, so mutating the options later has no
effect.

When `Enabled` is `false` no sink is created: `IsEnabled` returns `false`
for every level and calls cost one null check.

### Log flow

```text
logger.Error("Database connection failed", ex)
  └─ LoggerExtensions.Error  →  Logger.Log(Error, message, ex)
       ├─ IsEnabled(Error)?  no → return (no entry, no clock read, no I/O)
       ├─ new LogEntry(TimeProvider.GetUtcNow(), Error, message, ex)
       ├─ ILogSink.Write(entry)                       (FileLogSink)
       │    ├─ ILogFormatter.Format(entry) → string   (outside the lock)
       │    ├─ UTF-8 encode + "\n"
       │    └─ lock { rotate if needed; write bytes; flush }
       └─ on exception → OnWriteError(new LogWriteException(entry, cause))
```

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
  If closing fails too, the original exception is the one reported.
- **Truncated last line.** If a previous process died in the middle of an
  entry, the file does not end with `\n`. When the file is opened, a line
  break is added first, so only the cut-off line is invalid and the new
  entries start on lines of their own.

### Rotation strategy

Rotation is size-based, which is deterministic and easy to test; time-based
rotation was left out because nothing in the current scope needs it.

**When.** Before writing an entry, if the active file is not empty and the
entry would make it larger than `MaxFileSizeBytes`, the file is rotated
first. Consequences:

- A file never exceeds the limit, except when a *single* entry is larger than
  the limit: it is then written whole into a fresh file. Entries are never
  split or dropped because of size.
- A file that is already over the limit when the logger starts (for example
  after the limit was lowered) is rotated on the first write.

**How.** With `application.log` and `MaxRetainedFiles = 3`:

```text
1. delete  application.3.log                   (oldest; ignored if missing)
2. rename  application.2.log → application.3.log
3. rename  application.1.log → application.2.log
4. rename  application.log   → application.1.log
5. create  application.log                     (new, empty)
```

So `application.1.log` is always the most recent archive and higher numbers
are older. The index goes before the extension (`app` without extension
becomes `app.1`). Renaming is a metadata operation: rotating never reads or
copies file contents, whatever the file size.

**Retention.** `MaxRetainedFiles` counts archives *besides* the active file,
so at most `MaxRetainedFiles + 1` files exist. With `0`, rotation deletes the
full file and starts over. The value is capped at 1000 because every rotation
walks the archive indices.

**Leftovers.** Only indices `1..MaxRetainedFiles` are managed. If the limit is
lowered between runs, archives with higher indices from earlier runs are left
untouched (the library does not delete files it is not currently managing).

**Failure mid-rotation.** Each step tolerates missing files, so a rotation
interrupted half-way (for example, an archive locked by another program) is
completed by the next attempt. Until then the exception is propagated to the
caller of `Write`, and the next write reopens the active file.

The size of the active file is tracked in memory (initialized from the file
length when it is opened), so the check costs no I/O.

### Concurrency

One logger is meant to be shared by the whole application, so it must handle
calls from many threads at once without corrupting the file, interleaving
lines, throwing because of contention, or losing entries.

**Where the shared state is.** `Logger` and the formatters have no mutable
state, so they are safe without synchronization. All shared mutable state
(the open `FileStream`, the tracked file size, the rotation) lives in
`FileLogSink`, and the `ILogSink` contract requires sinks to be thread-safe.

**Strategy: one lock in `FileLogSink.Write`** (`System.Threading.Lock`).
Inside it: check disposal, rotate if needed, write the bytes, flush. Because
"check size → rotate → write" is one critical section, two threads can never
both decide to rotate, or write into a file that is being renamed. Formatting
and UTF-8 encoding happen *before* taking the lock, since they only touch the
entry, which keeps the critical section to the I/O itself.

**Alternatives considered.**

| Option | Why not (for now) |
|---|---|
| Background writer (`Channel<T>` / `ConcurrentQueue<T>` + one consumer thread) | Callers would not wait for I/O, but entries still in the queue are lost if the process crashes, `Dispose` must drain the queue, and errors happen on another thread, away from the caller. More code and new failure modes for a gain that only matters at high volume. |
| Opening the file per write with OS-level locking | Much slower and still needs in-process coordination for rotation. |
| Lock-free tricks | No benefit: the expensive part is the I/O, which must be serialized anyway. |

The lock keeps the behavior simple to reason about: when `Log` returns, the
entry is in the file (or was handed to the write-error handler). A
buffered/asynchronous sink could be added later as
another `ILogSink` without changing `Logger`.

**Verification.** `ConcurrencyTests` writes thousands of entries from many
threads at once (`Parallel.For`, and threads released together by a
`Barrier`, with rotation happening during the run) and checks that no call
threw, every line parses as JSON, no file exceeds the size limit, and every
message appears exactly once. Removing the lock makes both tests fail. The
tests wait for the threads to finish (`Join`) instead of using sleeps.

**Limits.** The lock coordinates threads in one process. Several processes
writing the same file are not supported: the file is opened with
`FileShare.Read`, so a second writer fails to open it instead of corrupting it.

### Naming

`ILogger` and `LogLevel` share names with types in
`Microsoft.Extensions.Logging`. They live in the `LoggingLibrary` namespace, so
there is no conflict unless a file imports both namespaces; in that case use an
alias, e.g. `using LogLevel = LoggingLibrary.LogLevel;`.

## Error handling

A logging library has two conflicting goals: logging must not take the
application down, and failures must not silently lose information. The
policy depends on *when* the error happens.

| Situation | When | Behavior |
|---|---|---|
| Invalid option (empty path, negative size, undefined level...) | `new Logger(options)` | Throws `ArgumentException` naming the option. |
| Invalid path syntax | `new Logger(options)` | Throws `ArgumentException`. |
| Directory cannot be created | `new Logger(options)` | Throws `IOException` / `UnauthorizedAccessException`. |
| File cannot be opened (locked, no permission, path is a directory) | `new Logger(options)` | Throws `IOException` / `UnauthorizedAccessException`. |
| Write fails (disk full, I/O error) | `Log(...)` | Reported to the write-error handler; `Log` does not throw. |
| Rotation fails (archive locked, file deleted...) | `Log(...)` | Same: reported, the next write retries. |
| Logging after `Dispose()` | `Log(...)` | Reported (`ObjectDisposedException` as cause). |
| `null` message | `Log(...)` | Throws `ArgumentNullException` (programming error). |

**At startup: fail fast.** Configuration and file-system problems found
while creating the logger are thrown to the caller. They are usually
deployment mistakes, and the application can decide whether to run without
logs. Because the file is opened in the constructor, these problems surface
here and not later.

**While running: never throw, never be silent.** `Logger.Log` catches
exceptions from the sink and passes a `LogWriteException` to the write-error
handler. The exception carries the cause (`InnerException`) and the entry
that was not written (`Entry`), so nothing is lost without a trace.

- The default handler prints the cause and the full entry (as a text line,
  including its exception) to **standard error**. Console apps, containers
  and service managers usually collect stderr, so the entry still ends up
  somewhere visible.
- `LoggerOptions.OnWriteError` replaces the default, e.g. to count failures,
  write to a fallback location, or alert. A handler that throws makes `Log`
  throw, which gives a strict "fail on log errors" mode:
  `OnWriteError = error => throw error`.
- If standard error itself fails, the default handler gives up silently;
  there is no channel left.

**Recovery.** After a failed write `FileLogSink` closes its stream; the next
write reopens the file (and retries a pending rotation), so a transient
problem does not disable logging for the rest of the process. The failed
entry itself is not retried: it was handed to the handler.

**Known trade-off.** If rotation fails, entries are reported rather than
appended past the size limit. Writing them anyway would keep them in the file
but break the size guarantee and make the failure invisible, so the explicit
report was preferred.

Programming errors (`null` message or sink) always throw, as in any .NET API.
