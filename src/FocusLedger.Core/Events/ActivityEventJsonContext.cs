using System.Text.Json.Serialization;

namespace FocusLedger.Core.Events;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ForegroundActivityEvent))]
[JsonSerializable(typeof(DayBoundaryActivityEvent))]
[JsonSerializable(typeof(StateSnapshotActivityEvent))]
[JsonSerializable(typeof(OperationalActivityEvent))]
public sealed partial class ActivityEventJsonContext : JsonSerializerContext {
}
