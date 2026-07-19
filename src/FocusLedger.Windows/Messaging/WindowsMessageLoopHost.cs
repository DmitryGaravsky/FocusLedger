namespace FocusLedger.Windows.Messaging;

// Runs the process message loop without creating a console, form, taskbar button, or visible window.
public sealed class WindowsMessageLoopHost : IDisposable {
    readonly MessageLoopApplicationContext applicationContext;
    readonly int ownerThreadId;
    bool disposed;
    bool runStarted;
    public WindowsMessageLoopHost(params IReadOnlyList<IWindowMessageHandler> messageHandlers) {
        ArgumentNullException.ThrowIfNull(messageHandlers);
        if(messageHandlers.Any(messageHandler => messageHandler is null))
            throw new ArgumentException("Message handlers cannot contain null entries.", nameof(messageHandlers));
        ownerThreadId = Environment.CurrentManagedThreadId;
        applicationContext = new MessageLoopApplicationContext(messageHandlers.ToArray());
    }
    public void Dispose() {
        if(disposed)
            return;
        ThrowIfWrongThread();
        applicationContext.Dispose();
        disposed = true;
    }
    public nint MessageWindowHandle {
        get { return applicationContext.MessageWindowHandle; }
    }
    // Blocks the owning STA thread while Windows dispatches messages and cancellation requests clean exit.
    public void Run(CancellationToken cancellationToken) {
        ThrowIfDisposed();
        ThrowIfWrongThread();
        if(runStarted)
            throw new InvalidOperationException("The Windows message loop can run only once.");
        runStarted = true;
        using(CancellationTokenRegistration registration = cancellationToken.Register(RequestExit)) {
            Application.Run(applicationContext);
        }
    }
    // Enqueues shutdown without blocking the caller or performing work on the requesting thread.
    public void RequestExit() {
        ThrowIfDisposed();
        applicationContext.RequestExit();
    }
    void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
    void ThrowIfWrongThread() {
        if(Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("The Windows message-loop host must be run and disposed on its creating thread.");
    }
}
