using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FocusLedger.Windows.Messaging;

// Owns the hidden window and exits the Windows Forms thread only through its message queue.
sealed class MessageLoopApplicationContext : ApplicationContext {
    readonly MessageOnlyWindow messageWindow;
    internal MessageLoopApplicationContext(IReadOnlyList<IWindowMessageHandler> messageHandlers) {
        messageWindow = new MessageOnlyWindow(messageHandlers, ExitThread);
    }
    protected override void Dispose(bool disposing) {
        if(disposing)
            messageWindow.Dispose();
        base.Dispose(disposing);
    }
    internal nint MessageWindowHandle {
        get { return messageWindow.Handle; }
    }
    internal void RequestExit() {
        if(messageWindow.Handle == nint.Zero)
            return;
        if(!NativeMethods.PostMessage(messageWindow.Handle, MessageOnlyWindow.ExitMessage, nint.Zero, nint.Zero))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
}
