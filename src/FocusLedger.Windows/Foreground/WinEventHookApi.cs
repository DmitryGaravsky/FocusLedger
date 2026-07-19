using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Foreground;

sealed class WinEventHookApi : IWinEventHookApi {
    public readonly static WinEventHookApi Instance = new();
    WinEventHookApi() { }
    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
    public nint SetHook(uint eventMinimum, uint eventMaximum, WinEventHookCallback callback) {
        const uint OutOfContext = 0;
        const uint SkipOwnProcess = 2;
        return NativeMethods.SetWinEventHook(
            eventMinimum,
            eventMaximum,
            nint.Zero,
            callback,
            0,
            0,
            OutOfContext | SkipOwnProcess);
    }
    public bool Unhook(nint hookHandle) {
        return NativeMethods.UnhookWinEvent(hookHandle);
    }
    public nint GetForegroundWindow() {
        return NativeMethods.GetForegroundWindow();
    }
    public int GetLastError() {
        return Marshal.GetLastPInvokeError();
    }
}
