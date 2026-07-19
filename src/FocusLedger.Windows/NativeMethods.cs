using System.Runtime.InteropServices;

namespace FocusLedger.Windows;

static class NativeMethods {
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(
        nint windowHandle,
        uint message,
        nint wordParameter,
        nint longParameter
    );
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint moduleHandle,
        Foreground.WinEventHookCallback callback,
        uint processId,
        uint threadId,
        uint flags
    );
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hookHandle);
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();
}
