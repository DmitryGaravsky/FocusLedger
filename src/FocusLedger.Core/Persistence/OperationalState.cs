using System.Text.Json.Serialization;

namespace FocusLedger.Core.Persistence;

// Contains only minimal operational recovery data and no activity context or personal identifiers.
public sealed record OperationalState(
    int SchemaVersion,
    long NextSequence,
    bool ManualPause,
    bool CleanShutdown) {
    public static OperationalState Initial { get; } = new(1, 1, false, true);
}

public enum OperationalStateLoadStatus {
    Missing,
    Loaded,
    Invalid
}

// Reports safe recovery facts without retaining malformed state content or a storage path.
public sealed record OperationalStateLoadResult(OperationalState State, OperationalStateLoadStatus Status);

// Reports the previous shutdown boundary and whether invalid state required a safe reset.
public sealed record OperationalStateInitialization(
    OperationalState State,
    bool WasPreviousShutdownClean,
    bool RecoveredFromInvalidState);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(OperationalState))]
sealed partial class OperationalStateJsonContext : JsonSerializerContext {
}
