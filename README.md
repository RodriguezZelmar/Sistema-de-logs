# LoggingLibrary

A small, reusable logging library for C#/.NET.

> **Status: early development.** The solution structure is in place but the
> logging functionality is not implemented yet. This README only describes
> what actually exists; planned work is listed under [Roadmap](#roadmap).

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

## Requirements

- .NET SDK 10 (LTS). The exact SDK band is pinned in [`global.json`](global.json).

## Building

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

- [x] 1. Solution and project structure
- [ ] 2. `LogLevel`, `LogEntry` and core interfaces
- [ ] 3. File sink
- [ ] 4. JSON (NDJSON) formatting
- [ ] 5. Configuration (`LoggerOptions`)
- [ ] 6. Minimum-level filtering
- [ ] 7. Size-based file rotation
- [ ] 8. Thread safety
- [ ] 9. Complete test suite
- [ ] 10. Sample application
- [ ] 11. NuGet packaging
- [ ] 12. Final review of architecture, docs and code quality

## License

[MIT](LICENSE)
