namespace FocusLedger.App;

using FocusLedger.Core.State;
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
    bool stopping;
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
            _ = SetPausedAsync(true);
            return;
        }
        if(command == TrayCommand.ResumeTracking) {
            _ = SetPausedAsync(false);
            return;
        }
        if(command == TrayCommand.Exit && !stopping) {
            stopping = true;
            _ = StopAsync();
        }
    }
    async Task SetPausedAsync(bool paused) {
        try {
            TrackerLifecycleState state = await runtime.SetPausedAsync(paused, CancellationToken.None).ConfigureAwait(false);
            PostState(CreateMenuState(state, false));
        }
        catch {
            PostState(CreateMenuState(runtime.State, true));
        }
    }
    async Task StopAsync() {
        bool hasError = false;
        try {
            await runtime.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch { hasError = true; }
        TryPost(() => {
            if(hasError)
                trayStatusIndicator!.Update(CreateMenuState(runtime.State, true));
            messageLoopHost.RequestExit();
        });
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
}
