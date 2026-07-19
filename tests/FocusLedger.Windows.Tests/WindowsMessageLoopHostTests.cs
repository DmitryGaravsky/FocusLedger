using FocusLedger.Windows.Messaging;

namespace FocusLedger.Windows.Tests;

public sealed class WindowsMessageLoopHostTests {
    const int TestMessage = 0x8000 + 0x42;
    [Test]
    public async Task MessageOnlyWindowRoutesMessagesWithoutCreatingForms() {
        RecordingMessageHandler messageHandler = new();
        TaskCompletionSource<WindowsMessageLoopHost> hostReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> loopStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread messageLoopThread = CreateMessageLoopThread(messageHandler,
            hostReady, loopStopped, CancellationToken.None);
        messageLoopThread.Start();
        WindowsMessageLoopHost host = await hostReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(NativeMethods.PostMessage(host.MessageWindowHandle, TestMessage, nint.Zero, nint.Zero), Is.True);
        await messageHandler.MessageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        host.RequestExit();
        await loopStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Multiple(() => {
            Assert.That(messageHandler.OpenFormCount, Is.Zero);
            Assert.That(messageLoopThread.Join(TimeSpan.FromSeconds(5)), Is.True);
        });
    }
    [Test]
    public async Task CancellationRequestsCleanMessageLoopExit() {
        using CancellationTokenSource cancellationSource = new();
        TaskCompletionSource<WindowsMessageLoopHost> hostReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> loopStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread messageLoopThread = CreateMessageLoopThread(new RecordingMessageHandler(),
            hostReady, loopStopped, cancellationSource.Token);
        messageLoopThread.Start();
        await hostReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellationSource.CancelAsync();
        await loopStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(messageLoopThread.Join(TimeSpan.FromSeconds(5)), Is.True);
    }
    static Thread CreateMessageLoopThread(
        IWindowMessageHandler messageHandler,
        TaskCompletionSource<WindowsMessageLoopHost> hostReady,
        TaskCompletionSource<bool> loopStopped,
        CancellationToken cancellationToken) {
        Thread thread = new(() => {
            ApplicationConfiguration.Initialize();
            using WindowsMessageLoopHost host = new(messageHandler);
            hostReady.TrySetResult(host);
            host.Run(cancellationToken);
            loopStopped.TrySetResult(true);
        });
        thread.SetApartmentState(ApartmentState.STA);
        return thread;
    }
    sealed class RecordingMessageHandler : IWindowMessageHandler {
        internal readonly TaskCompletionSource<bool> MessageReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int OpenFormCount { get; set; } = -1;
        public bool TryHandle(ref Message message) {
            if(message.Msg != TestMessage)
                return false;
            OpenFormCount = Application.OpenForms.Count;
            MessageReceived.TrySetResult(true);
            return true;
        }
    }
}
