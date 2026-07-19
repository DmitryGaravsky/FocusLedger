using Microsoft.Win32.SafeHandles;

namespace FocusLedger.Windows.Processes;

// Maps expected Windows process races and access restrictions to privacy-safe structured outcomes.
public sealed class WindowsProcessMetadataInspector : IProcessMetadataInspector {
    const int AccessDeniedError = 5;
    const int InvalidHandleError = 6;
    const int InvalidParameterError = 87;
    const int NotFoundError = 1168;
    readonly IProcessMetadataApi processApi;
    public WindowsProcessMetadataInspector()
        : this(ProcessMetadataApi.Instance) {
    }
    internal WindowsProcessMetadataInspector(IProcessMetadataApi processApi) {
        ArgumentNullException.ThrowIfNull(processApi);
        this.processApi = processApi;
    }
    public Task<ProcessInspectionResult> InspectAsync(long windowHandle, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, 0);
        return Task.Run(() => Inspect(new nint(windowHandle)), cancellationToken);
    }
    internal ProcessInspectionResult Inspect(nint windowHandle) {
        uint processId = processApi.GetProcessId(windowHandle);
        if(processId == 0)
            return new ProcessInspectionResult(ProcessInspectionStatus.WindowUnavailable, processApi.GetLastError(), null);
        SafeProcessHandle processHandle = processApi.OpenProcess(processId);
        using(processHandle) {
            if(processHandle.IsInvalid)
                return CreateFailure(processId, processApi.GetLastError());
            if(!processApi.TryGetExecutablePath(processHandle, out string? executablePath, out int errorCode))
                return CreateFailure(processId, errorCode);
            string? processName = NormalizeExecutableName(executablePath);
            (string? productName, string? fileDescription) = processApi.GetVersionMetadata(executablePath!);
            ProcessMetadata metadata = new(processId, processName, executablePath, productName, fileDescription);
            return new ProcessInspectionResult(ProcessInspectionStatus.Success, null, metadata);
        }
    }
    ProcessInspectionResult CreateFailure(uint processId, int errorCode) {
        processApi.TryGetProcessName(processId, out string? processName);
        ProcessMetadata metadata = new(processId, processName, null, null, null);
        ProcessInspectionStatus status = errorCode switch {
            AccessDeniedError => ProcessInspectionStatus.AccessDenied,
            InvalidHandleError or InvalidParameterError or NotFoundError => ProcessInspectionStatus.ProcessExited,
            _ when processName is not null => ProcessInspectionStatus.Limited,
            _ => ProcessInspectionStatus.PlatformError
        };
        return new ProcessInspectionResult(status, errorCode, metadata);
    }
    static string? NormalizeExecutableName(string? executablePath) {
        if(string.IsNullOrWhiteSpace(executablePath))
            return null;
        return Path.GetFileName(executablePath).ToLowerInvariant();
    }
}
