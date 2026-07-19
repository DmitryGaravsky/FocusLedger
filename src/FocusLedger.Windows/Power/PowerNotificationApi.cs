using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Power;

sealed class PowerNotificationApi : IPowerNotificationApi {
    const uint DeviceNotifyWindowHandle = 0;
    internal static PowerNotificationApi Instance { get; } = new();
    public bool TryRegister(nint windowHandle, out nint registrationHandle, out int errorCode) {
        registrationHandle = NativeMethods.RegisterSuspendResumeNotification(windowHandle, DeviceNotifyWindowHandle);
        if(registrationHandle != nint.Zero) {
            errorCode = 0;
            return true;
        }
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
    public bool TryUnregister(nint registrationHandle, out int errorCode) {
        if(NativeMethods.UnregisterSuspendResumeNotification(registrationHandle)) {
            errorCode = 0;
            return true;
        }
        errorCode = Marshal.GetLastPInvokeError();
        return false;
    }
}
