namespace FocusLedger.Windows.Tray;

// Converts runtime state into the stable, privacy-safe text and icon shown by the Windows shell.
static class TrayPresentationResolver {
    public static TrayPresentation Resolve(TrayStatus status) {
        ArgumentNullException.ThrowIfNull(status);
        if(status.HasError)
            return new TrayPresentation(TrayIconKind.Error, "FocusLedger - Error");
        if(status.MeetingDetected)
            return new TrayPresentation(TrayIconKind.Meeting, "FocusLedger - Meeting detected");
        return status.Activity switch {
            TrayActivityState.Active => new TrayPresentation(TrayIconKind.Active, "FocusLedger - Collecting / Active"),
            TrayActivityState.Idle => new TrayPresentation(TrayIconKind.Idle, "FocusLedger - Collecting / Idle"),
            TrayActivityState.Paused => new TrayPresentation(TrayIconKind.Paused, "FocusLedger - Paused"),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.Activity, "Unknown tray activity state.")
        };
    }
}
