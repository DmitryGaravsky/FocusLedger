namespace FocusLedger.App;

using FocusLedger.Core.State;
using FocusLedger.Windows.Commands;
using FocusLedger.Windows.Messaging;
using FocusLedger.Windows.Tray;

// Keeps asynchronous command execution off the Windows message-loop thread and marshals presentation updates back.
sealed class AppTrayCommandDispatcher {
    const TrayCommandCapabilities Capabilities = TrayCommandCapabilities.PauseTracking
        | TrayCommandCapabilities.ResumeTracking
        | TrayCommandCapabilities.Exit;
    readonly FocusLedgerRuntime runtime;
    readonly WindowsMessageLoopHost messageLoopHost;
    TrayStatusIndicator? trayStatusIndicator;
    int stopping;
    public AppTrayCommandDispatcher(FocusLedgerRuntime runtime, WindowsMessageLoopHost messageLoopHost) {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.messageLoopHost = messageLoopHost ?? throw new ArgumentNullException(nameof(messageLoopHost));
    }
    public void Attach(TrayStatusIndicator indicator) {
        ArgumentNullException.ThrowIfNull(indicator);
        if(trayStatusIndicator is not null)
            throw new InvalidOperationException("The tray status indicator is already attached.");
        trayStatusIndicator = indicator;
        indicator.Update(CreateMenuState(runtime.State, false));
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
        bool paused = command == LocalCommandKind.Pause;
        try {
            TrackerLifecycleState state = await runtime.SetPausedAsync(paused, cancellationToken)
                .ConfigureAwait(false);
            PostState(CreateMenuState(state, false));
            return new LocalCommandResult(true, GetStatusName(state));
        }
        catch {
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
    void PostState(TrayMenuState state) {
        TryPost(() => trayStatusIndicator!.Update(state));
    }
    bool TryPost(Action action) {
        try { return messageLoopHost.TryPost(action); }
        catch(ObjectDisposedException) { return false; }
    }
    static TrayMenuState CreateMenuState(TrackerLifecycleState state, bool hasError) {
        TrayActivityState activity = state == TrackerLifecycleState.Paused ? TrayActivityState.Paused : TrayActivityState.Active;
        return new TrayMenuState(new TrayStatus(activity, false, hasError), Capabilities, false);
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
