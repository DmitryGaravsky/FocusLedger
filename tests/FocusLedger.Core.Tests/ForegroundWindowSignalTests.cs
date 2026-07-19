using FocusLedger.Core.Signals;

namespace FocusLedger.Core.Tests;

public sealed class ForegroundWindowSignalTests {
    [Test]
    public void TitleCandidatesForSameWindowCoalesceAcrossObservationTimes() {
        DateTimeOffset firstTime = new(2026, 7, 19, 10, 30, 0, TimeSpan.Zero);
        ForegroundWindowSignal first = new(100, ForegroundObservationKind.TitleChangedCandidate, firstTime, 10, SignalDelivery.Coalescible);
        ForegroundWindowSignal later = first with { ObservedAt = firstTime.AddSeconds(1), MonotonicTimestamp = 20 };
        Assert.That(first.CanCoalesceWith(later), Is.True);
    }
    [Test]
    public void ForegroundSwitchesNeverCoalesce() {
        DateTimeOffset observationTime = new(2026, 7, 19, 10, 30, 0, TimeSpan.Zero);
        ForegroundWindowSignal signal = new(100, ForegroundObservationKind.WindowChanged, observationTime, 10, SignalDelivery.NonDroppable);
        Assert.That(signal.CanCoalesceWith(signal), Is.False);
    }
}
