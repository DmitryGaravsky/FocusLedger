namespace FocusLedger.Windows.Tray;

// Owns the shell notification icon and keeps all Windows Forms access on its creating thread.
public sealed class TrayStatusIndicator : IDisposable {
    readonly NotifyIcon notifyIcon;
    readonly int ownerThreadId;
    bool disposed;
    public TrayStatusIndicator() {
        ownerThreadId = Environment.CurrentManagedThreadId;
        notifyIcon = new NotifyIcon();
        Update(TrayStatus.Active);
        notifyIcon.Visible = true;
    }
    public void Dispose() {
        if(disposed)
            return;
        ThrowIfWrongThread();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        disposed = true;
    }
    // Replaces the shell presentation atomically from one privacy-safe status snapshot.
    public void Update(TrayStatus status) {
        ObjectDisposedException.ThrowIf(disposed, this);
        ThrowIfWrongThread();
        TrayPresentation presentation = TrayPresentationResolver.Resolve(status);
        notifyIcon.Icon = ResolveIcon(presentation.Icon);
        notifyIcon.Text = presentation.Tooltip;
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
