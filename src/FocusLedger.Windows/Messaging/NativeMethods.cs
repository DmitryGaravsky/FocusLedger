using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Messaging;

static class NativeMethods {
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(
        nint windowHandle,
        uint message,
        nint wordParameter,
        nint longParameter
    );
}
