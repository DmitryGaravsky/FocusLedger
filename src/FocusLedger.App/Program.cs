namespace FocusLedger.App;

using FocusLedger.Windows.Messaging;

static class Program {
    [STAThread]
    static void Main() {
        ApplicationConfiguration.Initialize();
        using(WindowsMessageLoopHost messageLoopHost = new()) {
            messageLoopHost.Run(CancellationToken.None);
        }
    }
}
