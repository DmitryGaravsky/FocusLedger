namespace FocusLedger.App;

using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Tray;

static class Program {
    [STAThread]
    static void Main() {
        ApplicationConfiguration.Initialize();
        string storageRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusLedger");
        FocusLedgerRuntime runtime = new(storageRootPath, TimeProvider.System);
        try {
            runtime.InitializeAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            using(WindowsMessageLoopHost messageLoopHost = new()) {
                AppTrayCommandDispatcher dispatcher = new(runtime, messageLoopHost);
                using(TrayStatusIndicator trayStatusIndicator = new(dispatcher.Handle)) {
                    dispatcher.Attach(trayStatusIndicator);
                    messageLoopHost.Run(CancellationToken.None);
                }
            }
        }
        finally { runtime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
}
