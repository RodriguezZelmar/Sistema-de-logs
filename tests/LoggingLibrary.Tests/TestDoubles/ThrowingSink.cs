using LoggingLibrary.Sinks;

namespace LoggingLibrary.Tests.TestDoubles;

/// <summary>Sink whose writes always fail with the given exception.</summary>
internal sealed class ThrowingSink(Exception exception) : ILogSink
{
    public void Write(LogEntry entry) => throw exception;

    public void Dispose()
    {
    }
}
