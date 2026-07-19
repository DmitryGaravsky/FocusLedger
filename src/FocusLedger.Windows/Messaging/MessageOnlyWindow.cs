namespace FocusLedger.Windows.Messaging;

// Provides the non-visible HWND used by future WinEvent, WTS, power, and command integrations.
sealed class MessageOnlyWindow : NativeWindow, IDisposable {
    internal const int ExitMessage = 0x8000 + 0x41;
    internal const int DispatchMessage = 0x8000 + 0x43;
    const int MaximumPendingActions = 64;
    static readonly nint MessageOnlyWindowParent = new(-3);
    readonly IReadOnlyList<IWindowMessageHandler> messageHandlers;
    readonly Action exitThread;
    readonly Queue<Action> pendingActions = new();
    bool disposed;
    internal MessageOnlyWindow(IReadOnlyList<IWindowMessageHandler> messageHandlers, Action exitThread) {
        this.messageHandlers = messageHandlers;
        this.exitThread = exitThread;
        CreateHandle(new CreateParams {
            Caption = "FocusLedger.MessageWindow",
            Parent = MessageOnlyWindowParent
        });
    }
    public void Dispose() {
        if(disposed)
            return;
        DestroyHandle();
        disposed = true;
    }
    internal bool TryPost(Action action) {
        ArgumentNullException.ThrowIfNull(action);
        lock(pendingActions) {
            if(disposed || pendingActions.Count >= MaximumPendingActions)
                return false;
            pendingActions.Enqueue(action);
        }
        if(NativeMethods.PostMessage(Handle, DispatchMessage, nint.Zero, nint.Zero))
            return true;
        lock(pendingActions) {
            pendingActions.Clear();
        }
        return false;
    }
    protected override void WndProc(ref Message message) {
        if(message.Msg == ExitMessage) {
            exitThread();
            return;
        }
        if(message.Msg == DispatchMessage) {
            DispatchPendingActions();
            return;
        }
        foreach(IWindowMessageHandler messageHandler in messageHandlers) {
            if(messageHandler.TryHandle(ref message))
                return;
        }
        base.WndProc(ref message);
    }
    void DispatchPendingActions() {
        while(true) {
            Action? action;
            lock(pendingActions) {
                if(!pendingActions.TryDequeue(out action))
                    return;
            }
            action();
        }
    }
}
