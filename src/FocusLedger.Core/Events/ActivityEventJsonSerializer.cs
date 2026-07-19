using System.Text.Json;

namespace FocusLedger.Core.Events;

// Provides the source-generated schema-1 serialization boundary used by persistence and readers.
public static class ActivityEventJsonSerializer {
    public static byte[] Serialize(ForegroundActivityEvent activityEvent) {
        ArgumentNullException.ThrowIfNull(activityEvent);
        return JsonSerializer.SerializeToUtf8Bytes(activityEvent, ActivityEventJsonContext.Default.ForegroundActivityEvent);
    }
    public static ForegroundActivityEvent? DeserializeForeground(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ActivityEventJsonContext.Default.ForegroundActivityEvent);
    }
}
