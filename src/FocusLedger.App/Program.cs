using FocusLedger.Windows.Autostart;
using FocusLedger.Windows.Commands;
using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Shell;
using FocusLedger.Windows.SingleInstance;
using FocusLedger.Windows.Tray;

namespace FocusLedger.App;

static class Program {
    static string StorageRootPath {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusLedger"); }
    }
    [STAThread]
    static async Task Main(string[] args) {
        if(!LocalCommandLine.TryParse(args, out LocalCommandKind? startupCommand))
            return;
        using(PerUserSingleInstance singleInstance = PerUserSingleInstance.Acquire()) {
            if(!singleInstance.IsPrimary) {
                await ForwardToPrimaryAsync(startupCommand ?? LocalCommandKind.Status)
                    .ConfigureAwait(false);
                return;
            }
            FocusLedgerRuntime runtime = new(StorageRootPath, TimeProvider.System);
            try {
                await runtime.InitializeAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                string executablePath = Environment.ProcessPath
                    ?? throw new InvalidOperationException("The current executable path is unavailable.");
                PerUserAutostart autostart = new(
                    executablePath,
                    runtime.Configuration.Startup.RegistryValueName,
                    runtime.Configuration.Startup.Arguments);
                AutostartState autostartState = autostart.GetState();
                KnownLocalPathLauncher pathLauncher = new(StorageRootPath);
                TaskCompletionSource<AppTrayCommandDispatcher> dispatcherReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Task applicationTask = RunStaApplicationAsync(runtime, autostart, pathLauncher, autostartState, dispatcherReady);
                AppTrayCommandDispatcher dispatcher = await dispatcherReady.Task
                    .ConfigureAwait(false);
                using(CancellationTokenSource commandServerCancellation = new()) {
                    LocalCommandServer commandServer = new(dispatcher.HandleLocalCommandAsync);
                    Task commandServerTask = commandServer.RunAsync(commandServerCancellation.Token);
                    Task configurationTask = runtime.RunConfigurationAsync(dispatcher.HandleConfigurationStateAsync, commandServerCancellation.Token);
                    try {
                        if(startupCommand is not null) {
                            LocalCommandResult result = await dispatcher.HandleLocalCommandAsync(startupCommand.Value, CancellationToken.None)
                                .ConfigureAwait(false);
                            if(result.AfterAcknowledgement is not null)
                                await result.AfterAcknowledgement()
                                    .ConfigureAwait(false);
                        }
                        await applicationTask
                            .ConfigureAwait(false);
                    }
                    finally {
                        await commandServerCancellation.CancelAsync()
                            .ConfigureAwait(false);
                        await commandServerTask
                            .ConfigureAwait(false);
                        try {
                            await configurationTask
                                .ConfigureAwait(false);
                        }
                        catch(OperationCanceledException)
                            when(commandServerCancellation.IsCancellationRequested) {
                        }
                    }
                }
            }
            finally {
                await runtime.DisposeAsync()
                    .ConfigureAwait(false);
            }
        }
    }
    // Runs all Windows Forms resources on one dedicated STA thread and exposes only its completion task.
    static Task RunStaApplicationAsync(
        FocusLedgerRuntime runtime,
        PerUserAutostart autostart,
        KnownLocalPathLauncher pathLauncher,
        AutostartState autostartState,
        TaskCompletionSource<AppTrayCommandDispatcher> dispatcherReady) {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread applicationThread = new(() => RunStaApplication(runtime, autostart, pathLauncher, autostartState, dispatcherReady, completion)) {
            IsBackground = false,
            Name = "FocusLedger.Application"
        };
        applicationThread.SetApartmentState(ApartmentState.STA);
        applicationThread.Start();
        return completion.Task;
    }
    static void RunStaApplication(
        FocusLedgerRuntime runtime,
        PerUserAutostart autostart,
        KnownLocalPathLauncher pathLauncher,
        AutostartState autostartState,
        TaskCompletionSource<AppTrayCommandDispatcher> dispatcherReady,
        TaskCompletionSource completion) {
        try {
            ApplicationConfiguration.Initialize();
            using(WindowsMessageLoopHost messageLoopHost = new()) {
                AppTrayCommandDispatcher dispatcher = new(runtime, messageLoopHost, autostart, pathLauncher, autostartState);
                using(TrayStatusIndicator trayStatusIndicator = new(dispatcher.Handle)) {
                    dispatcher.Attach(trayStatusIndicator);
                    dispatcherReady.TrySetResult(dispatcher);
                    messageLoopHost.Run(CancellationToken.None);
                }
            }
        }
        catch(Exception exception) {
            dispatcherReady.TrySetException(exception);
            completion.TrySetException(exception);
        }
        finally { completion.TrySetResult(); }
    }
    static async ValueTask ForwardToPrimaryAsync(LocalCommandKind command) {
        try {
            LocalCommandClient client = new();
            await client.SendAsync(command, TimeSpan.FromSeconds(5), CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch(Exception exception)
            when(exception is IOException or TimeoutException or OperationCanceledException) {
        }
    }
}
