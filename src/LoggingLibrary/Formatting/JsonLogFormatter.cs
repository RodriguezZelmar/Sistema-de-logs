using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LoggingLibrary.Formatting;

/// <summary>
/// Formats each entry as a single-line JSON object, suitable for JSON Lines
/// (NDJSON) files:
/// <code>{"timestamp":"2026-10-01T20:30:15.432Z","level":"Info","message":"Application started"}</code>
/// </summary>
/// <remarks>
/// The <c>exception</c> property is only written when the entry has an
/// exception, and contains <c>type</c>, <c>message</c>, <c>stackTrace</c>
/// (when available) and a nested <c>innerException</c> (when present).
/// Line breaks inside strings are escaped, so one entry is always one line.
/// </remarks>
public sealed class JsonLogFormatter : ILogFormatter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // Keeps non-ASCII text (accents, other alphabets) readable instead of
        // \uXXXX escapes. The relaxed encoder is only unsafe when the output is
        // embedded in HTML; control characters are still escaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <inheritdoc />
    public string Format(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", LogFormatting.FormatTimestamp(entry.Timestamp));
            writer.WriteString("level", LogFormatting.LevelName(entry.Level));
            writer.WriteString("message", entry.Message);
            if (entry.Exception is not null)
            {
                writer.WritePropertyName("exception");
                WriteException(writer, entry.Exception);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteException(Utf8JsonWriter writer, Exception exception)
    {
        writer.WriteStartObject();
        writer.WriteString("type", exception.GetType().FullName);
        writer.WriteString("message", exception.Message);
        if (exception.StackTrace is not null)
        {
            writer.WriteString("stackTrace", exception.StackTrace);
        }
        if (exception.InnerException is not null)
        {
            writer.WritePropertyName("innerException");
            WriteException(writer, exception.InnerException);
        }
        writer.WriteEndObject();
    }
}
