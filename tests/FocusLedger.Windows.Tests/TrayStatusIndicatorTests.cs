using FocusLedger.Windows.Tray;

namespace FocusLedger.Windows.Tests;

public sealed class TrayStatusIndicatorTests {
    [Test]
    public void ConstructorRejectsNullCommandCallback() {
        Assert.That(() => new TrayStatusIndicator(null!), Throws.TypeOf<ArgumentNullException>());
    }
    [Test]
    public void UpdateAndDisposeThrowWhenCalledFromAnotherThread() {
        RunOnStaThread(owningAction => {
            using TrayStatusIndicator indicator = new(_ => { });
            owningAction(indicator);
        }, indicator => {
            Assert.Multiple(() => {
                Assert.That(() => indicator.Update(TrayMenuState.Initial), Throws.TypeOf<InvalidOperationException>());
                Assert.That(() => indicator.Dispose(), Throws.TypeOf<InvalidOperationException>());
            });
        });
    }
    [Test]
    public void UpdateThrowsAfterDisposeOnOwningThread() {
        RunOnStaThread(() => {
            TrayStatusIndicator indicator = new(_ => { });
            indicator.Dispose();
            indicator.Dispose();
            Assert.That(() => indicator.Update(TrayMenuState.Initial), Throws.TypeOf<ObjectDisposedException>());
        });
    }
    [Test]
    public void UpdateRejectsNullMenuState() {
        RunOnStaThread(() => {
            using TrayStatusIndicator indicator = new(_ => { });
            Assert.That(() => indicator.Update((TrayMenuState)null!), Throws.TypeOf<ArgumentNullException>());
        });
    }
    [Test]
    public void UpdateAcceptsEveryDocumentedStatusCombination() {
        RunOnStaThread(() => {
            using TrayStatusIndicator indicator = new(_ => { });
            Assert.Multiple(() => {
                Assert.That(() => indicator.Update(ToMenuState(new TrayStatus(TrayActivityState.Active, false, false))), Throws.Nothing);
                Assert.That(() => indicator.Update(ToMenuState(new TrayStatus(TrayActivityState.Idle, false, false))), Throws.Nothing);
                Assert.That(() => indicator.Update(ToMenuState(new TrayStatus(TrayActivityState.Paused, false, false))), Throws.Nothing);
                Assert.That(() => indicator.Update(ToMenuState(new TrayStatus(TrayActivityState.Active, true, false))), Throws.Nothing);
                Assert.That(() => indicator.Update(ToMenuState(new TrayStatus(TrayActivityState.Active, false, true))), Throws.Nothing);
            });
        });
    }
    static TrayMenuState ToMenuState(TrayStatus status) {
        return new TrayMenuState(status, TrayCommandCapabilities.Exit, false);
    }
    static void RunOnStaThread(Action action) {
        Exception? failure = null;
        Thread thread = new(() => {
            try { action(); }
            catch(Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
        if(failure is not null)
            throw failure;
    }
    static void RunOnStaThread(Action<Action<TrayStatusIndicator>> owningThreadBody, Action<TrayStatusIndicator> foreignThreadAssertions) {
        TaskCompletionSource<TrayStatusIndicator> indicatorReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim assertionsCompleted = new(false);
        Exception? failure = null;
        Thread owningThread = new(() => {
            try {
                owningThreadBody(indicator => {
                    indicatorReady.TrySetResult(indicator);
                    assertionsCompleted.Wait();
                });
            }
            catch(Exception exception) { failure = exception; }
        });
        owningThread.SetApartmentState(ApartmentState.STA);
        owningThread.Start();
        TrayStatusIndicator indicator = indicatorReady.Task.Wait(TimeSpan.FromSeconds(5))
            ? indicatorReady.Task.Result
            : throw new TimeoutException("The tray status indicator was not created in time.");
        try {
            foreignThreadAssertions(indicator);
        }
        finally {
            assertionsCompleted.Set();
            Assert.That(owningThread.Join(TimeSpan.FromSeconds(5)), Is.True);
        }
        if(failure is not null)
            throw failure;
    }
}
