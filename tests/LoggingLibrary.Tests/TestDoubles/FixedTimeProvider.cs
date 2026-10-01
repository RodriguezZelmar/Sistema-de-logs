namespace LoggingLibrary.Tests.TestDoubles;

/// <summary>Clock that always returns the same instant, for deterministic timestamps.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
