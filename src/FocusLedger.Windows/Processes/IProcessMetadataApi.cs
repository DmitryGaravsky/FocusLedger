using Microsoft.Win32.SafeHandles;

namespace FocusLedger.Windows.Processes;

interface IProcessMetadataApi {
    uint GetProcessId(nint windowHandle);
    SafeProcessHandle OpenProcess(uint processId);
    bool TryGetExecutablePath(SafeProcessHandle processHandle, out string? executablePath, out int errorCode);
    bool TryGetProcessName(uint processId, out string? processName);
    (string? ProductName, string? FileDescription) GetVersionMetadata(string executablePath);
    int GetLastError();
}
