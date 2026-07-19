using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Session;

sealed class SessionNotificationApi : ISessionNotificationApi {
    const uint NotifyForThisSession = 0;
    internal static SessionNotificationApi Instance { get; } = new();
    public bool TryGetCurrentSessionId(out uint sessionId, out int errorCode) {
        if(NativeMethods.ProcessIdToSessionId(NativeMethods.GetCurrentProcessId(), out sessionId)) {
            errorCode = 0;
            return true;
        }
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
    public bool TryRegister(nint windowHandle, out int errorCode) {
        if(NativeMethods.WTSRegisterSessionNotification(windowHandle, NotifyForThisSession)) {
            errorCode = 0;
            return true;
        }
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
    public bool TryUnregister(nint windowHandle, out int errorCode) {
        if(NativeMethods.WTSUnRegisterSessionNotification(windowHandle)) {
            errorCode = 0;
            return true;
        }
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
}
