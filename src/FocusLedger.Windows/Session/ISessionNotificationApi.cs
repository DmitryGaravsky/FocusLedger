namespace FocusLedger.Windows.Session;

// Isolates WTS registration and current-session resolution for deterministic failure testing.
interface ISessionNotificationApi {
    bool TryGetCurrentSessionId(out uint sessionId, out int errorCode);
    bool TryRegister(nint windowHandle, out int errorCode);
    bool TryUnregister(nint windowHandle, out int errorCode);
}
