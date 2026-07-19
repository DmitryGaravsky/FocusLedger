namespace FocusLedger.Windows.Tests;

using FocusLedger.Windows.Tray;

public sealed class TrayMenuRulesTests {
    const TrayCommandCapabilities ActivityCapabilities = TrayCommandCapabilities.PauseTracking | TrayCommandCapabilities.ResumeTracking;
    const TrayCommandCapabilities MeetingCapabilities = TrayCommandCapabilities.StartMeeting | TrayCommandCapabilities.EndMeeting;
    [TestCase(TrayActivityState.Active, true, false)]
    [TestCase(TrayActivityState.Idle, true, false)]
    [TestCase(TrayActivityState.Paused, false, true)]
    public void ActivityCommandsFollowPauseState(TrayActivityState activity, bool pauseEnabled, bool resumeEnabled) {
        TrayMenuState state = new(new TrayStatus(activity, false, false), ActivityCapabilities, false);
        Assert.That(TrayMenuRules.IsEnabled(state, TrayCommand.PauseTracking), Is.EqualTo(pauseEnabled));
        Assert.That(TrayMenuRules.IsEnabled(state, TrayCommand.ResumeTracking), Is.EqualTo(resumeEnabled));
    }
    [TestCase(false, true, false)]
    [TestCase(true, false, true)]
    public void MeetingCommandsFollowMeetingState(bool meetingDetected, bool startEnabled, bool endEnabled) {
        TrayMenuState state = new(new TrayStatus(TrayActivityState.Active, meetingDetected, false), MeetingCapabilities, false);
        Assert.That(TrayMenuRules.IsEnabled(state, TrayCommand.StartMeeting), Is.EqualTo(startEnabled));
        Assert.That(TrayMenuRules.IsEnabled(state, TrayCommand.EndMeeting), Is.EqualTo(endEnabled));
    }
    [Test]
    public void CapabilityKeepsUnavailablePlaceholderDisabled() {
        Assert.That(TrayMenuRules.IsEnabled(TrayMenuState.Initial, TrayCommand.ReportToday), Is.False);
        Assert.That(TrayMenuRules.IsEnabled(TrayMenuState.Initial, TrayCommand.Exit), Is.True);
    }
    [TestCase(TrayActivityState.Active, false, false, "FocusLedger - Collecting")]
    [TestCase(TrayActivityState.Idle, false, false, "FocusLedger - Idle")]
    [TestCase(TrayActivityState.Paused, false, false, "FocusLedger - Paused")]
    [TestCase(TrayActivityState.Active, true, false, "FocusLedger - Meeting")]
    [TestCase(TrayActivityState.Active, true, true, "FocusLedger - Error")]
    public void HeaderUsesStatePrecedence(TrayActivityState activity, bool meetingDetected, bool hasError, string expected) {
        Assert.That(TrayMenuRules.ResolveHeader(new TrayStatus(activity, meetingDetected, hasError)), Is.EqualTo(expected));
    }
}
