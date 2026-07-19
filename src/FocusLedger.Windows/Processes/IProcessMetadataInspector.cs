using System.Text.Json.Serialization;

namespace FocusLedger.Windows.Processes;

public enum ProcessInspectionStatus {
    Success,
    Limited,
    WindowUnavailable,
    ProcessExited,
    AccessDenied,
    PlatformError
}

// Contains external process values only for transient identity resolution in memory.
public sealed class ProcessMetadata {
    public ProcessMetadata(
        uint processId,
        string? processName,
        string? executablePath,
        string? productName,
        string? fileDescription) {
        ProcessId = processId;
        ProcessName = processName;
        ExecutablePath = executablePath;
        ProductName = productName;
        FileDescription = fileDescription;
    }
    [JsonIgnore]
    public uint ProcessId { get; }
    [JsonIgnore]
    public string? ProcessName { get; }
    [JsonIgnore]
    public string? ExecutablePath { get; }
    [JsonIgnore]
    public string? ProductName { get; }
    [JsonIgnore]
    public string? FileDescription { get; }
    public override string ToString() {
        return "ProcessMetadata { transient values redacted }";
    }
}

public sealed record ProcessInspectionResult(
    ProcessInspectionStatus Status,
    int? PlatformErrorCode,
    ProcessMetadata? Metadata) {
    public override string ToString() {
        return $"ProcessInspectionResult {{ Status = {Status}, PlatformErrorCode = {PlatformErrorCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, transient values redacted }}";
    }
}

// Reads the minimum process identity metadata required by later application classification.
public interface IProcessMetadataInspector {
    // Performs potentially blocking cross-process inspection away from the caller thread.
    Task<ProcessInspectionResult> InspectAsync(long windowHandle, CancellationToken cancellationToken);
}
