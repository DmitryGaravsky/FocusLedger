namespace FocusLedger.Windows.Messaging;

// Provides the non-visible HWND used by future WinEvent, WTS, power, and command integrations.
sealed class MessageOnlyWindow : NativeWindow, IDisposable {
    internal const int ExitMessage = 0x8000 + 0x41;
    static readonly nint MessageOnlyWindowParent = new(-3);
    readonly IReadOnlyList<IWindowMessageHandler> messageHandlers;
    readonly Action exitThread;
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
    protected override void WndProc(ref Message message) {
        if(message.Msg == ExitMessage) {
            exitThread();
            return;
        }
        foreach(IWindowMessageHandler messageHandler in messageHandlers) {
            if(messageHandler.TryHandle(ref message))
                return;
        }
        base.WndProc(ref message);
    }
}
