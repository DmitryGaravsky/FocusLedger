namespace FocusLedger.Windows.Power;

// Isolates suspend/resume notification registration for deterministic platform-failure tests.
interface IPowerNotificationApi {
    bool TryRegister(nint windowHandle, out nint registrationHandle, out int errorCode);
    bool TryUnregister(nint registrationHandle, out int errorCode);
}
