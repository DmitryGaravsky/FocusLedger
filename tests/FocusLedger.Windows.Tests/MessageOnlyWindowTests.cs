using FocusLedger.Windows.Messaging;

namespace FocusLedger.Windows.Tests;

public sealed class MessageOnlyWindowTests {
    [Test]
    public void TryPostRejectsWorkOnceQueueIsSaturatedOrDisposed() {
        using ManualResetEventSlim windowReady = new(false);
        using ManualResetEventSlim disposeRequested = new(false);
        using ManualResetEventSlim disposeCompleted = new(false);
        MessageOnlyWindow? window = null;
        Thread thread = new(() => {
            window = new MessageOnlyWindow([], () => { });
            windowReady.Set();
            disposeRequested.Wait();
            window.Dispose();
            disposeCompleted.Set();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.That(windowReady.Wait(TimeSpan.FromSeconds(5)), Is.True);
        bool[] postResults = new bool[65];
        for(int index = 0; index < postResults.Length; index++)
            postResults[index] = window!.TryPost(() => { });
        disposeRequested.Set();
        Assert.That(disposeCompleted.Wait(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
        bool postAfterDispose = window!.TryPost(() => { });
        Assert.Multiple(() => {
            Assert.That(postResults.Take(64), Has.All.True);
            Assert.That(postResults[64], Is.False);
            Assert.That(postAfterDispose, Is.False);
        });
    }
}
