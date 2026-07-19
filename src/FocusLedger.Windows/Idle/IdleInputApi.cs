using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Idle;

sealed class IdleInputApi : IIdleInputApi {
    internal static IdleInputApi Instance { get; } = new();
    public bool TryGetLastInputTime(out uint lastInputTime, out int errorCode) {
        NativeMethods.LastInputInfo lastInputInfo = new() {
            Size = checked((uint)Marshal.SizeOf<NativeMethods.LastInputInfo>())
        };
        if(NativeMethods.GetLastInputInfo(ref lastInputInfo)) {
            lastInputTime = lastInputInfo.Time;
            errorCode = 0;
            return true;
        }
        lastInputTime = 0;
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
    public ulong GetUptimeMilliseconds() {
        return NativeMethods.GetTickCount64();
    }
}
