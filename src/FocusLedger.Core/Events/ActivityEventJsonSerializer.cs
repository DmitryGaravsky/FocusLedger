using System.Text.Json;

namespace FocusLedger.Core.Events;

// Provides the source-generated schema-1 serialization boundary used by persistence and readers.
public static class ActivityEventJsonSerializer {
    public static byte[] Serialize(ActivityEvent activityEvent) {
        ArgumentNullException.ThrowIfNull(activityEvent);
        return activityEvent switch {
            ForegroundActivityEvent foregroundActivityEvent => Serialize(foregroundActivityEvent),
            DayBoundaryActivityEvent dayBoundaryActivityEvent => JsonSerializer.SerializeToUtf8Bytes(
                dayBoundaryActivityEvent,
                ActivityEventJsonContext.Default.DayBoundaryActivityEvent),
            StateSnapshotActivityEvent stateSnapshotActivityEvent => JsonSerializer.SerializeToUtf8Bytes(
                stateSnapshotActivityEvent,
                ActivityEventJsonContext.Default.StateSnapshotActivityEvent),
            OperationalActivityEvent operationalActivityEvent => JsonSerializer.SerializeToUtf8Bytes(
                operationalActivityEvent,
                ActivityEventJsonContext.Default.OperationalActivityEvent),
            TrackingControlActivityEvent trackingControlActivityEvent => JsonSerializer.SerializeToUtf8Bytes(
                trackingControlActivityEvent,
                ActivityEventJsonContext.Default.TrackingControlActivityEvent),
            _ => throw new NotSupportedException("The activity event type does not have a registered schema serializer.")
        };
    }
    public static byte[] Serialize(ForegroundActivityEvent activityEvent) {
        ArgumentNullException.ThrowIfNull(activityEvent);
        return JsonSerializer.SerializeToUtf8Bytes(activityEvent, ActivityEventJsonContext.Default.ForegroundActivityEvent);
    }
    public static ForegroundActivityEvent? DeserializeForeground(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ActivityEventJsonContext.Default.ForegroundActivityEvent);
    }
    public static DayBoundaryActivityEvent? DeserializeDayBoundary(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ActivityEventJsonContext.Default.DayBoundaryActivityEvent);
    }
    public static StateSnapshotActivityEvent? DeserializeStateSnapshot(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ActivityEventJsonContext.Default.StateSnapshotActivityEvent);
    }
    public static TrackingControlActivityEvent? DeserializeTrackingControl(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ActivityEventJsonContext.Default.TrackingControlActivityEvent);
    }
}
