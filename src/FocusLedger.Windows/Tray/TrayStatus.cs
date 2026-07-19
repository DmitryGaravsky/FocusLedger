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

// Carries the complete state needed to enable commands without giving the tray access to application services.
public sealed record TrayMenuState(TrayStatus Status, TrayCommandCapabilities Capabilities, bool AutostartEnabled) {
    public static TrayMenuState Initial { get; } = new(TrayStatus.Active, TrayCommandCapabilities.Exit, false);
}

public enum TrayCommand {
    PauseTracking,
    ResumeTracking,
    ReportToday,
    ReportYesterday,
    ReportLastSevenDays,
    ReportCurrentMonth,
    OpenLatestReport,
    OpenReportsFolder,
    OpenDataFolder,
    OpenConfiguration,
    ReloadConfiguration,
    StartMeeting,
    EndMeeting,
    ToggleAutostart,
    Exit
}

[Flags]
public enum TrayCommandCapabilities {
    None = 0,
    PauseTracking = 1 << 0,
    ResumeTracking = 1 << 1,
    ReportToday = 1 << 2,
    ReportYesterday = 1 << 3,
    ReportLastSevenDays = 1 << 4,
    ReportCurrentMonth = 1 << 5,
    OpenLatestReport = 1 << 6,
    OpenReportsFolder = 1 << 7,
    OpenDataFolder = 1 << 8,
    OpenConfiguration = 1 << 9,
    ReloadConfiguration = 1 << 10,
    StartMeeting = 1 << 11,
    EndMeeting = 1 << 12,
    ToggleAutostart = 1 << 13,
    Exit = 1 << 14
}

enum TrayIconKind {
    Active,
    Idle,
    Paused,
    Meeting,
    Error
}

sealed record TrayPresentation(TrayIconKind Icon, string Tooltip);
