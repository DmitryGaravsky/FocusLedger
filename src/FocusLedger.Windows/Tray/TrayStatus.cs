namespace FocusLedger.Windows.Tray;

// Describes the user-visible collection state without exposing collector implementation details.
public sealed record TrayStatus(TrayActivityState Activity, bool MeetingDetected, bool HasError) {
    public static TrayStatus Active { get; } = new(TrayActivityState.Active, false, false);
}

public enum TrayActivityState {
    Active,
    Idle,
    Paused
}

enum TrayIconKind {
    Active,
    Idle,
    Paused,
    Meeting,
    Error
}

sealed record TrayPresentation(TrayIconKind Icon, string Tooltip);
