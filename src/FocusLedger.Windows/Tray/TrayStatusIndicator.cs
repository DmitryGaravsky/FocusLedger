namespace FocusLedger.Windows.Tray;

// Owns the shell notification icon and keeps all Windows Forms access on its creating thread.
public sealed class TrayStatusIndicator : IDisposable {
    readonly NotifyIcon notifyIcon;
    readonly TrayCommandMenu commandMenu;
    readonly int ownerThreadId;
    bool disposed;
    public TrayStatusIndicator(Action<TrayCommand> commandRequested) {
        ArgumentNullException.ThrowIfNull(commandRequested);
        ownerThreadId = Environment.CurrentManagedThreadId;
        commandMenu = new TrayCommandMenu(commandRequested);
        notifyIcon = new NotifyIcon();
        notifyIcon.ContextMenuStrip = commandMenu.ContextMenu;
        Update(TrayMenuState.Initial);
        notifyIcon.Visible = true;
    }
    public void Dispose() {
        if(disposed)
            return;
        ThrowIfWrongThread();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        commandMenu.Dispose();
        disposed = true;
    }
    // Updates icon, tooltip, command availability, and checked state from one immutable snapshot.
    public void Update(TrayMenuState state) {
        ObjectDisposedException.ThrowIf(disposed, this);
        ThrowIfWrongThread();
        ArgumentNullException.ThrowIfNull(state);
        TrayPresentation presentation = TrayPresentationResolver.Resolve(state.Status);
        notifyIcon.Icon = ResolveIcon(presentation.Icon);
        notifyIcon.Text = presentation.Tooltip;
        commandMenu.Update(state);
    }
    static Icon ResolveIcon(TrayIconKind icon) {
        return icon switch {
            TrayIconKind.Active => SystemIcons.Application,
            TrayIconKind.Idle => SystemIcons.Information,
            TrayIconKind.Paused => SystemIcons.Warning,
            TrayIconKind.Meeting => SystemIcons.Question,
            TrayIconKind.Error => SystemIcons.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown tray icon kind.")
        };
    }
    void ThrowIfWrongThread() {
        if(Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("The tray status indicator must be updated and disposed on its creating thread.");
    }
}
