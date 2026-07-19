namespace FocusLedger.App;

using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Tray;

static class Program {
    [STAThread]
    static void Main() {
        ApplicationConfiguration.Initialize();
        using(WindowsMessageLoopHost messageLoopHost = new()) {
            using(TrayStatusIndicator trayStatusIndicator = new(command => HandleTrayCommand(command, messageLoopHost))) {
                messageLoopHost.Run(CancellationToken.None);
            }
        }
    }
    static void HandleTrayCommand(TrayCommand command, WindowsMessageLoopHost messageLoopHost) {
        if(command == TrayCommand.Exit)
            messageLoopHost.RequestExit();
    }
}
