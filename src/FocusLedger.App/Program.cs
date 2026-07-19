using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.SingleInstance;
using FocusLedger.Windows.Tray;

namespace FocusLedger.App;

static class Program {
    static string StorageRootPath {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusLedger"); }
    }
    [STAThread]
    static async Task Main() {
        using(PerUserSingleInstance singleInstance = PerUserSingleInstance.Acquire()) {
            if(!singleInstance.IsPrimary)
                return;
            FocusLedgerRuntime runtime = new(StorageRootPath, TimeProvider.System);
            try {
                await runtime.InitializeAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                await RunStaApplicationAsync(runtime)
                    .ConfigureAwait(false);
            }
            finally {
                await runtime.DisposeAsync()
                    .ConfigureAwait(false);
            }
        }
    }
    // Runs all Windows Forms resources on one dedicated STA thread and exposes only its completion task.
    static Task RunStaApplicationAsync(FocusLedgerRuntime runtime) {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread applicationThread = new(() => RunStaApplication(runtime, completion)) {
            IsBackground = false,
            Name = "FocusLedger.Application"
        };
        applicationThread.SetApartmentState(ApartmentState.STA);
        applicationThread.Start();
        return completion.Task;
    }
    static void RunStaApplication(FocusLedgerRuntime runtime, TaskCompletionSource completion) {
        try {
            ApplicationConfiguration.Initialize();
            using(WindowsMessageLoopHost messageLoopHost = new()) {
                AppTrayCommandDispatcher dispatcher = new(runtime, messageLoopHost);
                using(TrayStatusIndicator trayStatusIndicator = new(dispatcher.Handle)) {
                    dispatcher.Attach(trayStatusIndicator);
                    messageLoopHost.Run(CancellationToken.None);
                }
            }
        }
        catch(Exception exception) {
            completion.TrySetException(exception);
        }
        finally {
            completion.TrySetResult();
        }
    }
}
