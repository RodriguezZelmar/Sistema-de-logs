using System.Collections.Concurrent;
using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests;

public sealed class ErrorHandlingTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ConcurrentQueue<LogWriteException> _errors = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void SinkFailure_DoesNotThrow_AndReportsErrorWithEntry()
    {
        var cause = new IOException("Disk full");
        var logger = new Logger(new ThrowingSink(cause), onWriteError: _errors.Enqueue);
        var original = new InvalidOperationException("original");

        logger.Error("Payment failed", original);

        var error = Assert.Single(_errors);
        Assert.Same(cause, error.InnerException);
        Assert.Equal(LogLevel.Error, error.Entry.Level);
        Assert.Equal("Payment failed", error.Entry.Message);
        Assert.Same(original, error.Entry.Exception);
        Assert.Contains("Disk full", error.Message);
    }

    [Fact]
    public void HandlerThatThrows_PropagatesToCaller()
    {
        var logger = new Logger(new ThrowingSink(new IOException("Disk full")), onWriteError: e => throw e);

        var thrown = Assert.Throws<LogWriteException>(() => logger.Info("message"));
        Assert.IsType<IOException>(thrown.InnerException);
    }

    [Fact]
    public void FilteredEntries_NeverReachTheFailingSink()
    {
        var logger = new Logger(new ThrowingSink(new IOException()), LogLevel.Error, onWriteError: _errors.Enqueue);

        logger.Info("filtered");

        Assert.Empty(_errors);
    }

    [Fact]
    public void LoggingAfterDispose_IsReportedNotThrown()
    {
        var logger = new Logger(new LoggerOptions { FilePath = _temp.Combine("app.log"), OnWriteError = _errors.Enqueue });
        logger.Dispose();

        logger.Info("too late");

        var error = Assert.Single(_errors);
        Assert.IsType<ObjectDisposedException>(error.InnerException);
        Assert.Equal("too late", error.Entry.Message);
    }

    [Fact]
    public void RotationBlocked_ReportsErrors_AndRecoversWhenUnblocked()
    {
        var path = _temp.Combine("app.log");
        var archive = _temp.Combine("app.1.log");
        var options = new LoggerOptions
        {
            FilePath = path,
            MaxFileSizeBytes = 150,
            MaxRetainedFiles = 1,
            OnWriteError = _errors.Enqueue,
        };

        using var logger = new Logger(options);
        while (!File.Exists(archive))
        {
            logger.Info("filling the first file");
        }

        // Hold the archive open without sharing: on Windows the next rotation
        // cannot delete it. (Unix file locks are advisory and do not block
        // deletion, so there the rotation simply succeeds.)
        using (new FileStream(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            for (var i = 0; i < 10; i++)
            {
                logger.Info($"while blocked {i}");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            Assert.NotEmpty(_errors);
        }
        Assert.All(_errors, e => Assert.IsAssignableFrom<IOException>(e.InnerException));

        logger.Info("after unblocking");
        Assert.EndsWith("""
            "message":"after unblocking"}
            """, ReadLastLine(path));
    }

    [Fact]
    public void NullMessage_StillThrows_BecauseItIsAProgrammingError()
    {
        var logger = new Logger(new ThrowingSink(new IOException()), onWriteError: _errors.Enqueue);

        Assert.Throws<ArgumentNullException>(() => logger.Info(null!));
        Assert.Empty(_errors);
    }

    private static string ReadLastLine(string path)
    {
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        return reader.ReadToEnd().TrimEnd('\n').Split('\n')[^1];
    }
}

/// <summary>Tests that replace <see cref="Console.Error"/>, which is global, so they must not run in parallel.</summary>
[Collection(nameof(StandardErrorCollection))]
public class DefaultErrorHandlerTests
{
    [Fact]
    public void DefaultHandler_WritesErrorAndLostEntryToStandardError()
    {
        var original = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            var logger = new Logger(new ThrowingSink(new IOException("Disk full")));

            logger.Critical("Shutting down", new InvalidOperationException("Out of credits"));
        }
        finally
        {
            Console.SetError(original);
        }

        var output = captured.ToString();
        Assert.Contains("[LoggingLibrary] Failed to write a log entry (System.IO.IOException: Disk full)", output);
        Assert.Contains("[Critical] Shutting down", output);
        Assert.Contains("Out of credits", output);
    }
}

[CollectionDefinition(nameof(StandardErrorCollection), DisableParallelization = true)]
public class StandardErrorCollection;
