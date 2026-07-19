namespace FocusLedger.Windows.Idle;

interface IIdleInputApi {
    bool TryGetLastInputTime(out uint lastInputTime, out int errorCode);
    ulong GetUptimeMilliseconds();
}
