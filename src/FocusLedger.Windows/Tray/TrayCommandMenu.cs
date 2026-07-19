namespace FocusLedger.Windows.Tray;

// Owns the fixed menu layout and forwards only enabled, direct user commands to the composition root.
sealed class TrayCommandMenu : IDisposable {
    readonly Action<TrayCommand> commandRequested;
    readonly Dictionary<TrayCommand, ToolStripMenuItem> commandItems = [];
    readonly ToolStripMenuItem headerItem;
    readonly ToolStripMenuItem reportItem;
    readonly ContextMenuStrip contextMenu;
    bool disposed;
    public TrayCommandMenu(Action<TrayCommand> commandRequested) {
        this.commandRequested = commandRequested ?? throw new ArgumentNullException(nameof(commandRequested));
        contextMenu = new ContextMenuStrip();
        headerItem = new ToolStripMenuItem { Enabled = false };
        reportItem = new ToolStripMenuItem("Generate report");
        reportItem.DropDownItems.Add(CreateCommandItem("Today", TrayCommand.ReportToday));
        reportItem.DropDownItems.Add(CreateCommandItem("Yesterday", TrayCommand.ReportYesterday));
        reportItem.DropDownItems.Add(CreateCommandItem("Last 7 days", TrayCommand.ReportLastSevenDays));
        reportItem.DropDownItems.Add(CreateCommandItem("Current month", TrayCommand.ReportCurrentMonth));
        contextMenu.Items.Add(headerItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateCommandItem("Pause tracking", TrayCommand.PauseTracking));
        contextMenu.Items.Add(CreateCommandItem("Resume tracking", TrayCommand.ResumeTracking));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(reportItem);
        contextMenu.Items.Add(CreateCommandItem("Open latest report", TrayCommand.OpenLatestReport));
        contextMenu.Items.Add(CreateCommandItem("Open reports folder", TrayCommand.OpenReportsFolder));
        contextMenu.Items.Add(CreateCommandItem("Open data folder", TrayCommand.OpenDataFolder));
        contextMenu.Items.Add(CreateCommandItem("Open configuration", TrayCommand.OpenConfiguration));
        contextMenu.Items.Add(CreateCommandItem("Reload configuration", TrayCommand.ReloadConfiguration));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateCommandItem("Start meeting manually", TrayCommand.StartMeeting));
        contextMenu.Items.Add(CreateCommandItem("End meeting manually", TrayCommand.EndMeeting));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateCommandItem("Start with Windows", TrayCommand.ToggleAutostart));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateCommandItem("Exit", TrayCommand.Exit));
    }
    public void Dispose() {
        if(disposed)
            return;
        contextMenu.Dispose();
        disposed = true;
    }
    public ContextMenuStrip ContextMenu {
        get { return contextMenu; }
    }
    public void Update(TrayMenuState state) {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(state);
        headerItem.Text = TrayMenuRules.ResolveHeader(state.Status);
        foreach(KeyValuePair<TrayCommand, ToolStripMenuItem> entry in commandItems)
            entry.Value.Enabled = TrayMenuRules.IsEnabled(state, entry.Key);
        reportItem.Enabled = reportItem.DropDownItems.Cast<ToolStripItem>().Any(item => item.Enabled);
        commandItems[TrayCommand.ToggleAutostart].Checked = state.AutostartEnabled;
    }
    ToolStripMenuItem CreateCommandItem(string text, TrayCommand command) {
        ToolStripMenuItem item = new(text);
        item.Click += (_, _) => commandRequested(command);
        commandItems.Add(command, item);
        return item;
    }
}
