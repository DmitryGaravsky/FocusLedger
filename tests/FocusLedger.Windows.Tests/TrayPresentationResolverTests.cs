namespace FocusLedger.Windows.Tests;

using FocusLedger.Windows.Tray;

public sealed class TrayPresentationResolverTests {
    static readonly object[] BaseStateCases = [
        new object[] { TrayActivityState.Active, (int)TrayIconKind.Active, "FocusLedger - Collecting / Active" },
        new object[] { TrayActivityState.Idle, (int)TrayIconKind.Idle, "FocusLedger - Collecting / Idle" },
        new object[] { TrayActivityState.Paused, (int)TrayIconKind.Paused, "FocusLedger - Paused" }
    ];
    [TestCaseSource(nameof(BaseStateCases))]
    public void ResolveMapsBaseActivityState(TrayActivityState activity, int expectedIcon, string expectedTooltip) {
        TrayPresentation presentation = TrayPresentationResolver.Resolve(new TrayStatus(activity, false, false));
        Assert.That(presentation, Is.EqualTo(new TrayPresentation((TrayIconKind)expectedIcon, expectedTooltip)));
    }
    [Test]
    public void ResolveMeetingTakesPrecedenceOverActivityState() {
        TrayPresentation presentation = TrayPresentationResolver.Resolve(new TrayStatus(TrayActivityState.Paused, true, false));
        Assert.That(presentation, Is.EqualTo(new TrayPresentation(TrayIconKind.Meeting, "FocusLedger - Meeting detected")));
    }
    [Test]
    public void ResolveErrorTakesPrecedenceOverMeeting() {
        TrayPresentation presentation = TrayPresentationResolver.Resolve(new TrayStatus(TrayActivityState.Active, true, true));
        Assert.That(presentation, Is.EqualTo(new TrayPresentation(TrayIconKind.Error, "FocusLedger - Error")));
    }
    [Test]
    public void ResolveRejectsUnknownActivityState() {
        TrayStatus status = new((TrayActivityState)int.MaxValue, false, false);
        Assert.That(() => TrayPresentationResolver.Resolve(status), Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
