using System.Collections.Concurrent;
using LoggingLibrary.Sinks;

namespace LoggingLibrary.Tests.TestDoubles;

/// <summary>Sink that keeps entries in memory so tests can inspect them.</summary>
internal sealed class InMemorySink : ILogSink
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public bool IsDisposed { get; private set; }

    public void Write(LogEntry entry) => _entries.Enqueue(entry);

    public void Dispose() => IsDisposed = true;
}
