using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FocusLedger.Core.Events;

namespace FocusLedger.Reporting.Reading;

sealed record LineReadResult(string? Line, bool InvalidEncoding);

// Streams one activity file while isolating damaged lines and never returning their raw content.
public static class CrashTolerantJsonlReader {
    const int MaximumLineCharacters = 1024 * 1024;
    static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static async IAsyncEnumerable<JsonlReadItem> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string safeFileName = GetSafeFileName(filePath);
        FileStream stream;
        try {
            stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch {
            throw new IOException("The activity file could not be opened for reading.");
        }
        using(stream) {
            using(StreamReader reader = new(stream, StrictUtf8, false, 4096, false)) {
                int lineNumber = 0;
                LineReadResult current = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
                if(current.InvalidEncoding) {
                    yield return new JsonlIssueReadItem(safeFileName, 1, JsonlDataQualityIssueKind.MalformedEvent);
                    yield break;
                }
                while(current.Line is not null) {
                    lineNumber++;
                    LineReadResult next = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
                    if(next.InvalidEncoding) {
                        yield return ParseLine(current.Line, safeFileName, lineNumber, false);
                        yield return new JsonlIssueReadItem(safeFileName, lineNumber + 1, JsonlDataQualityIssueKind.MalformedEvent);
                        yield break;
                    }
                    bool isFinalLine = next.Line is null;
                    yield return ParseLine(current.Line, safeFileName, lineNumber, isFinalLine);
                    current = next;
                }
            }
        }
    }
    static JsonlReadItem ParseLine(string line, string safeFileName, int lineNumber, bool isFinalLine) {
        if(line.Length > MaximumLineCharacters)
            return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.LineTooLong);
        byte[] utf8Json = StrictUtf8.GetBytes(line);
        try {
            using JsonDocument document = JsonDocument.Parse(utf8Json);
            JsonElement root = document.RootElement;
            if(root.ValueKind != JsonValueKind.Object)
                return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.InvalidEnvelope);
            if(!TryReadSchemaVersion(root, out int schemaVersion))
                return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.InvalidEnvelope);
            if(schemaVersion != 1)
                return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.UnsupportedSchema);
            if(!TryReadEnvelope(root, schemaVersion, out EventEnvelope? envelope))
                return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.InvalidEnvelope);
            ActivityEvent? activityEvent = TryReadKnownEvent(envelope!, utf8Json, out bool invalidPayload);
            if(invalidPayload)
                return new JsonlIssueReadItem(safeFileName, lineNumber, JsonlDataQualityIssueKind.InvalidPayload);
            return new JsonlEventReadItem(safeFileName, lineNumber, envelope!, activityEvent);
        }
        catch(JsonException) {
            JsonlDataQualityIssueKind kind = isFinalLine
                ? JsonlDataQualityIssueKind.IncompleteTrailingLine
                : JsonlDataQualityIssueKind.MalformedEvent;
            return new JsonlIssueReadItem(safeFileName, lineNumber, kind);
        }
    }
    static bool TryReadSchemaVersion(JsonElement root, out int schemaVersion) {
        schemaVersion = 0;
        return root.TryGetProperty("schemaVersion", out JsonElement property)
            && property.TryGetInt32(out schemaVersion);
    }
    static bool TryReadEnvelope(JsonElement root, int schemaVersion, out EventEnvelope? envelope) {
        envelope = null;
        if(!root.TryGetProperty("sequence", out JsonElement sequenceProperty)
            || !sequenceProperty.TryGetInt64(out long sequence)
            || sequence < 1)
            return false;
        if(!root.TryGetProperty("eventId", out JsonElement eventIdProperty)
            || !eventIdProperty.TryGetGuid(out Guid eventId)
            || eventId == Guid.Empty)
            return false;
        if(!root.TryGetProperty("timestampUtc", out JsonElement timestampProperty)
            || !timestampProperty.TryGetDateTimeOffset(out DateTimeOffset timestampUtc)
            || timestampUtc.Offset != TimeSpan.Zero)
            return false;
        if(!root.TryGetProperty("utcOffsetMinutes", out JsonElement offsetProperty)
            || !offsetProperty.TryGetInt32(out int utcOffsetMinutes)
            || utcOffsetMinutes is < -840 or > 840)
            return false;
        if(!root.TryGetProperty("type", out JsonElement typeProperty)
            || typeProperty.ValueKind != JsonValueKind.String)
            return false;
        string? type = typeProperty.GetString();
        if(string.IsNullOrWhiteSpace(type) || type.Length > 128)
            return false;
        if(!TryReadOptionalString(root, "source", out string? source)
            || !TryReadOptionalGuid(root, "correlationId", out Guid? correlationId))
            return false;
        envelope = new EventEnvelope(schemaVersion, sequence, eventId, timestampUtc, utcOffsetMinutes, type, source, correlationId);
        return true;
    }
    static ActivityEvent? TryReadKnownEvent(EventEnvelope envelope, byte[] utf8Json, out bool invalidPayload) {
        invalidPayload = false;
        try {
            return envelope.Type switch {
                "foreground.changed" or "foreground.context_changed" => ActivityEventJsonSerializer.DeserializeForeground(utf8Json),
                "day.started" or "day.ended" => ActivityEventJsonSerializer.DeserializeDayBoundary(utf8Json),
                "state.snapshot" => ActivityEventJsonSerializer.DeserializeStateSnapshot(utf8Json),
                "tracking.paused" or "tracking.resumed" => ActivityEventJsonSerializer.DeserializeTrackingControl(utf8Json),
                _ => null
            };
        }
        catch(JsonException) {
            invalidPayload = true;
            return null;
        }
        catch(ArgumentException) {
            invalidPayload = true;
            return null;
        }
    }
    static async ValueTask<LineReadResult> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken) {
        try {
            return new LineReadResult(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false), false);
        }
        catch(DecoderFallbackException) {
            return new LineReadResult(null, true);
        }
    }
    static bool TryReadOptionalString(JsonElement root, string propertyName, out string? value) {
        value = null;
        if(!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            return true;
        if(property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return value is not null && value.Length <= 128;
    }
    static bool TryReadOptionalGuid(JsonElement root, string propertyName, out Guid? value) {
        value = null;
        if(!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            return true;
        if(!property.TryGetGuid(out Guid parsedValue) || parsedValue == Guid.Empty)
            return false;
        value = parsedValue;
        return true;
    }
    static string GetSafeFileName(string filePath) {
        string fileName = Path.GetFileName(filePath);
        if(fileName.Length != 25
            || !fileName.StartsWith("activity-", StringComparison.Ordinal)
            || !fileName.EndsWith(".jsonl", StringComparison.Ordinal)
            || fileName[13] != '-'
            || fileName[16] != '-')
            return "activity-file";
        for(int index = 9; index < 19; index++) {
            if(index is 13 or 16)
                continue;
            if(!char.IsAsciiDigit(fileName[index]))
                return "activity-file";
        }
        return fileName;
    }
}
