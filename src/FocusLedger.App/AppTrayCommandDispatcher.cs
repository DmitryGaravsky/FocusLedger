namespace FocusLedger.App;

using FocusLedger.Core.State;
using FocusLedger.Windows.Autostart;
using FocusLedger.Windows.Commands;
using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Shell;
using FocusLedger.Windows.Tray;

// Keeps asynchronous command execution off the Windows message-loop thread and marshals presentation updates back.
sealed class AppTrayCommandDispatcher {
    const TrayCommandCapabilities Capabilities = TrayCommandCapabilities.PauseTracking
        | TrayCommandCapabilities.ResumeTracking
        | TrayCommandCapabilities.OpenReportsFolder
        | TrayCommandCapabilities.OpenDataFolder
        | TrayCommandCapabilities.OpenConfiguration
        | TrayCommandCapabilities.ToggleAutostart
        | TrayCommandCapabilities.Exit;
    readonly FocusLedgerRuntime runtime;
    readonly WindowsMessageLoopHost messageLoopHost;
    readonly PerUserAutostart autostart;
    readonly KnownLocalPathLauncher pathLauncher;
    TrayStatusIndicator? trayStatusIndicator;
    AutostartState autostartState;
    bool hasConfigurationError;
    int stopping;
    public AppTrayCommandDispatcher(
        FocusLedgerRuntime runtime,
        WindowsMessageLoopHost messageLoopHost,
        PerUserAutostart autostart,
        KnownLocalPathLauncher pathLauncher,
        AutostartState autostartState) {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.messageLoopHost = messageLoopHost ?? throw new ArgumentNullException(nameof(messageLoopHost));
        this.autostart = autostart ?? throw new ArgumentNullException(nameof(autostart));
        this.pathLauncher = pathLauncher ?? throw new ArgumentNullException(nameof(pathLauncher));
        this.autostartState = autostartState;
        hasConfigurationError = runtime.HasConfigurationError;
    }
    public void Attach(TrayStatusIndicator indicator) {
        ArgumentNullException.ThrowIfNull(indicator);
        if(trayStatusIndicator is not null)
            throw new InvalidOperationException("The tray status indicator is already attached.");
        trayStatusIndicator = indicator;
        indicator.Update(CreateMenuState(runtime.State));
    }
    public void Handle(TrayCommand command) {
        if(command == TrayCommand.PauseTracking) {
            _ = HandleTrayCommandAsync(LocalCommandKind.Pause);
            return;
        }
        if(command == TrayCommand.ResumeTracking) {
            _ = HandleTrayCommandAsync(LocalCommandKind.Resume);
            return;
        }
        if(command == TrayCommand.ToggleAutostart) {
            LocalCommandKind localCommand = autostartState == AutostartState.Enabled
                ? LocalCommandKind.DisableStartup
                : LocalCommandKind.EnableStartup;
            _ = HandleTrayCommandAsync(localCommand);
            return;
        }
        if(command == TrayCommand.OpenReportsFolder) {
            _ = HandleTrayPathCommandAsync(KnownLocalPath.ReportsFolder);
            return;
        }
        if(command == TrayCommand.OpenDataFolder) {
            _ = HandleTrayCommandAsync(LocalCommandKind.OpenDataFolder);
            return;
        }
        if(command == TrayCommand.OpenConfiguration) {
            _ = HandleTrayCommandAsync(LocalCommandKind.OpenConfiguration);
            return;
        }
        if(command == TrayCommand.Exit)
            _ = HandleTrayCommandAsync(LocalCommandKind.Quit);
    }
    public async ValueTask<LocalCommandResult> HandleLocalCommandAsync(
        LocalCommandKind command,
        CancellationToken cancellationToken) {
        if(command == LocalCommandKind.Status)
            return new LocalCommandResult(true, GetStatusName(runtime.State));
        if(command == LocalCommandKind.Quit)
            return await StopAsync(cancellationToken)
                .ConfigureAwait(false);
        if(command is LocalCommandKind.EnableStartup or LocalCommandKind.DisableStartup)
            return await SetAutostartAsync(command, cancellationToken)
                .ConfigureAwait(false);
        if(command is LocalCommandKind.OpenConfiguration or LocalCommandKind.OpenDataFolder) {
            KnownLocalPath target = command == LocalCommandKind.OpenConfiguration
                ? KnownLocalPath.Configuration
                : KnownLocalPath.DataFolder;
            return await OpenPathAsync(target, cancellationToken)
                .ConfigureAwait(false);
        }
        bool paused = command == LocalCommandKind.Pause;
        try {
            TrackerLifecycleState state = await runtime.SetPausedAsync(paused, cancellationToken)
                .ConfigureAwait(false);
            PostState(CreateMenuState(state));
            return new LocalCommandResult(true, GetStatusName(state));
        }
        catch {
            PostState(CreateMenuState(runtime.State, true));
            return new LocalCommandResult(false, "error");
        }
    }
    public ValueTask HandleConfigurationStateAsync(bool hasError, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        hasConfigurationError = hasError;
        PostState(CreateMenuState(runtime.State));
        return ValueTask.CompletedTask;
    }
    async ValueTask<LocalCommandResult> SetAutostartAsync(LocalCommandKind command, CancellationToken cancellationToken) {
        try {
            autostartState = await Task.Run(
                () => command == LocalCommandKind.EnableStartup ? autostart.Enable() : autostart.Disable(),
                cancellationToken)
                .ConfigureAwait(false);
            PostState(CreateMenuState(runtime.State));
            return new LocalCommandResult(true, autostartState == AutostartState.Enabled ? "startup-enabled" : "startup-disabled");
        }
        catch(Exception exception)
            when(exception is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
            PostState(CreateMenuState(runtime.State, true));
            return new LocalCommandResult(false, "error");
        }
    }
    async ValueTask<LocalCommandResult> OpenPathAsync(KnownLocalPath target, CancellationToken cancellationToken) {
        try {
            await Task.Run(() => pathLauncher.Open(target), cancellationToken)
                .ConfigureAwait(false);
            return new LocalCommandResult(true, "opened");
        }
        catch(Exception exception)
            when(exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) {
            PostState(CreateMenuState(runtime.State, true));
            return new LocalCommandResult(false, "error");
        }
    }
    async ValueTask<LocalCommandResult> StopAsync(CancellationToken cancellationToken) {
        if(Interlocked.Exchange(ref stopping, 1) != 0)
            return new LocalCommandResult(true, "stopping");
        bool hasError = false;
        try {
            await runtime.StopAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch { hasError = true; }
        Func<ValueTask> afterAcknowledgement = () => {
            TryPost(() => {
                if(hasError)
                    trayStatusIndicator!.Update(CreateMenuState(runtime.State, true));
                messageLoopHost.RequestExit();
            });
            return ValueTask.CompletedTask;
        };
        return new LocalCommandResult(!hasError, hasError ? "error" : "stopping", afterAcknowledgement);
    }
    async Task HandleTrayCommandAsync(LocalCommandKind command) {
        LocalCommandResult result = await HandleLocalCommandAsync(command, CancellationToken.None)
            .ConfigureAwait(false);
        if(result.AfterAcknowledgement is not null)
            await result.AfterAcknowledgement()
                .ConfigureAwait(false);
    }
    async Task HandleTrayPathCommandAsync(KnownLocalPath target) {
        await OpenPathAsync(target, CancellationToken.None)
            .ConfigureAwait(false);
    }
    void PostState(TrayMenuState state) {
        TryPost(() => trayStatusIndicator!.Update(state));
    }
    bool TryPost(Action action) {
        try { return messageLoopHost.TryPost(action); }
        catch(ObjectDisposedException) { return false; }
    }
    TrayMenuState CreateMenuState(TrackerLifecycleState state, bool operationError = false) {
        TrayActivityState activity = state == TrackerLifecycleState.Paused ? TrayActivityState.Paused : TrayActivityState.Active;
        bool hasError = operationError || hasConfigurationError || autostartState == AutostartState.InvalidPath;
        return new TrayMenuState(new TrayStatus(activity, false, hasError), Capabilities, autostartState == AutostartState.Enabled);
    }
    static string GetStatusName(TrackerLifecycleState state) {
        return state switch {
            TrackerLifecycleState.Running => "running",
            TrackerLifecycleState.Paused => "paused",
            TrackerLifecycleState.Stopping => "stopping",
            TrackerLifecycleState.Stopped => "stopped",
            TrackerLifecycleState.Faulted => "error",
            _ => "starting"
        };
    }
}
