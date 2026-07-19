using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FocusLedger.Windows.Processes;

sealed class ProcessMetadataApi : IProcessMetadataApi {
    const uint QueryLimitedInformation = 0x1000;
    const int InitialPathCapacity = 1024;
    const int MaximumPathCapacity = 32768;
    const int InsufficientBufferError = 122;
    internal static ProcessMetadataApi Instance { get; } = new();
    public uint GetProcessId(nint windowHandle) {
        return NativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId) == 0
            ? 0
            : processId;
    }
    public SafeProcessHandle OpenProcess(uint processId) {
        return NativeMethods.OpenProcess(QueryLimitedInformation, false, processId);
    }
    public bool TryGetExecutablePath(SafeProcessHandle processHandle, out string? executablePath, out int errorCode) {
        if(TryQueryExecutablePath(processHandle, InitialPathCapacity, out executablePath, out errorCode))
            return true;
        if(errorCode != InsufficientBufferError)
            return false;
        return TryQueryExecutablePath(processHandle, MaximumPathCapacity, out executablePath, out errorCode);
    }
    public bool TryGetProcessName(uint processId, out string? processName) {
        try {
            using(Process process = Process.GetProcessById(checked((int)processId))) {
                processName = NormalizeProcessName(process.ProcessName);
                return processName is not null;
            }
        }
        catch(ArgumentException) {
            processName = null;
            return false;
        }
        catch(InvalidOperationException) {
            processName = null;
            return false;
        }
        catch(SystemException) {
            processName = null;
            return false;
        }
    }
    public (string? ProductName, string? FileDescription) GetVersionMetadata(string executablePath) {
        try {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            return (versionInfo.ProductName, versionInfo.FileDescription);
        }
        catch(ArgumentException) {
            return (null, null);
        }
        catch(SystemException) {
            return (null, null);
        }
    }
    public int GetLastError() {
        return Marshal.GetLastPInvokeError();
    }
    static bool TryQueryExecutablePath(
        SafeProcessHandle processHandle,
        int capacity,
        out string? executablePath,
        out int errorCode) {
        char[] pathBuffer = new char[capacity];
        uint size = checked((uint)pathBuffer.Length);
        if(NativeMethods.QueryFullProcessImageName(processHandle, 0, pathBuffer, ref size)) {
            executablePath = new string(pathBuffer, 0, checked((int)size));
            errorCode = 0;
            return true;
        }
        executablePath = null;
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
    static string? NormalizeProcessName(string? processName) {
        if(string.IsNullOrWhiteSpace(processName))
            return null;
        string fileName = Path.GetFileName(processName);
        if(!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            fileName += ".exe";
        return fileName.ToLowerInvariant();
    }
}
