namespace FocusLedger.Windows.Foreground;

delegate void WinEventHookCallback(
    nint hookHandle,
    uint eventType,
    nint windowHandle,
    int objectId,
    int childId,
    uint eventThreadId,
    uint eventTimeMilliseconds);

// Isolates foreground Win32 hook registration so failure and callback behavior can be tested safely.
interface IWinEventHookApi {
    nint SetHook(uint eventMinimum, uint eventMaximum, WinEventHookCallback callback);
    bool Unhook(nint hookHandle);
    nint GetForegroundWindow();
    int GetLastError();
}
