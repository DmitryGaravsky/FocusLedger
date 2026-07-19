namespace FocusLedger.Windows.Tray;

// Applies runtime-state restrictions after the composition root declares which command handlers exist.
static class TrayMenuRules {
    public static string ResolveHeader(TrayStatus status) {
        ArgumentNullException.ThrowIfNull(status);
        if(status.HasError)
            return "FocusLedger - Error";
        if(status.MeetingDetected)
            return "FocusLedger - Meeting";
        return status.Activity switch {
            TrayActivityState.Active => "FocusLedger - Collecting",
            TrayActivityState.Idle => "FocusLedger - Idle",
            TrayActivityState.Paused => "FocusLedger - Paused",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.Activity, "Unknown tray activity state.")
        };
    }
    public static bool IsEnabled(TrayMenuState state, TrayCommand command) {
        ArgumentNullException.ThrowIfNull(state);
        TrayCommandCapabilities requiredCapability = ResolveCapability(command);
        if((state.Capabilities & requiredCapability) == 0)
            return false;
        return command switch {
            TrayCommand.PauseTracking => state.Status.Activity != TrayActivityState.Paused,
            TrayCommand.ResumeTracking => state.Status.Activity == TrayActivityState.Paused,
            TrayCommand.StartMeeting => !state.Status.MeetingDetected,
            TrayCommand.EndMeeting => state.Status.MeetingDetected,
            _ => true
        };
    }
    static TrayCommandCapabilities ResolveCapability(TrayCommand command) {
        return command switch {
            TrayCommand.PauseTracking => TrayCommandCapabilities.PauseTracking,
            TrayCommand.ResumeTracking => TrayCommandCapabilities.ResumeTracking,
            TrayCommand.ReportToday => TrayCommandCapabilities.ReportToday,
            TrayCommand.ReportYesterday => TrayCommandCapabilities.ReportYesterday,
            TrayCommand.ReportLastSevenDays => TrayCommandCapabilities.ReportLastSevenDays,
            TrayCommand.ReportCurrentMonth => TrayCommandCapabilities.ReportCurrentMonth,
            TrayCommand.OpenLatestReport => TrayCommandCapabilities.OpenLatestReport,
            TrayCommand.OpenReportsFolder => TrayCommandCapabilities.OpenReportsFolder,
            TrayCommand.OpenDataFolder => TrayCommandCapabilities.OpenDataFolder,
            TrayCommand.OpenConfiguration => TrayCommandCapabilities.OpenConfiguration,
            TrayCommand.ReloadConfiguration => TrayCommandCapabilities.ReloadConfiguration,
            TrayCommand.StartMeeting => TrayCommandCapabilities.StartMeeting,
            TrayCommand.EndMeeting => TrayCommandCapabilities.EndMeeting,
            TrayCommand.ToggleAutostart => TrayCommandCapabilities.ToggleAutostart,
            TrayCommand.Exit => TrayCommandCapabilities.Exit,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown tray command.")
        };
    }
}
