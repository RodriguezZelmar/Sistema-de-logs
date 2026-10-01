using System.Collections.Concurrent;
using System.Text.Json;
using LoggingLibrary.Tests.TestDoubles;

namespace LoggingLibrary.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ParallelFor_AllEntriesWrittenAsValidJsonLines()
    {
        const int count = 2000;
        var path = _temp.Combine("app.log");

        using (var logger = new Logger(new LoggerOptions { FilePath = path }))
        {
            Parallel.For(0, count, i => logger.Info($"Message {i}"));
        }

        AssertExactlyOnce(ReadAllMessages(), count);
    }

    [Fact]
    public void ThreadsStartingTogether_WithRotation_NoEntryLostOrCorrupted()
    {
        const int threadCount = 8;
        const int perThread = 250;
        var options = new LoggerOptions
        {
            FilePath = _temp.Combine("app.log"),
            MaxFileSizeBytes = 4_000,
            // Large enough that rotation never deletes anything, so every
            // entry must still be on disk at the end.
            MaxRetainedFiles = 1000,
        };

        // An unhandled exception on a raw thread would crash the test host,
        // so each thread records its exceptions for a normal assertion.
        var errors = new ConcurrentQueue<Exception>();
        using (var logger = new Logger(options))
        {
            // The barrier releases all threads at once to maximize contention.
            using var barrier = new Barrier(threadCount);
            var threads = Enumerable.Range(0, threadCount).Select(t => new Thread(() =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < perThread; i++)
                {
                    try
                    {
                        logger.Warning($"Message {(t * perThread) + i}");
                    }
                    catch (Exception ex)
                    {
                        errors.Enqueue(ex);
                    }
                }
            })).ToList();

            threads.ForEach(thread => thread.Start());
            threads.ForEach(thread => thread.Join());
        }

        Assert.Empty(errors);
        Assert.True(_temp.FileNames().Length > 1, "The test should have caused rotation.");
        Assert.All(Directory.GetFiles(_temp.Path), file => Assert.InRange(new FileInfo(file).Length, 1, options.MaxFileSizeBytes));
        AssertExactlyOnce(ReadAllMessages(), threadCount * perThread);
    }

    /// <summary>Parses every line of every file; fails on any malformed line.</summary>
    private List<string> ReadAllMessages()
    {
        var messages = new List<string>();
        foreach (var file in Directory.GetFiles(_temp.Path))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                using var doc = JsonDocument.Parse(line);
                messages.Add(doc.RootElement.GetProperty("message").GetString()!);
            }
        }
        return messages;
    }

    private static void AssertExactlyOnce(List<string> messages, int count)
    {
        var expected = Enumerable.Range(0, count).Select(i => $"Message {i}").Order(StringComparer.Ordinal);
        Assert.Equal(expected, messages.Order(StringComparer.Ordinal));
    }
}
