namespace FocusLedger.App;

using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Tray;

static class Program {
    [STAThread]
    static void Main() {
        ApplicationConfiguration.Initialize();
        using(TrayStatusIndicator trayStatusIndicator = new()) {
            using(WindowsMessageLoopHost messageLoopHost = new()) {
                messageLoopHost.Run(CancellationToken.None);
            }
        }
    }
}
