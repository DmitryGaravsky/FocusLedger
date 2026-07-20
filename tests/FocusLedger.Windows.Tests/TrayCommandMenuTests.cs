using FocusLedger.Windows.Tray;

namespace FocusLedger.Windows.Tests;

public sealed class TrayCommandMenuTests {
    [Test]
    public void UnavailableCommandsAreDisabledRatherThanRemoved() {
        RunOnStaThread(() => {
            using TrayCommandMenu menu = new(_ => { });
            int initialItemCount = menu.ContextMenu.Items.Count;
            menu.Update(TrayMenuState.Initial);
            Assert.Multiple(() => {
                Assert.That(menu.ContextMenu.Items.Count, Is.EqualTo(initialItemCount));
                Assert.That(FindItem(menu, TrayCommand.PauseTracking).Enabled, Is.False);
                Assert.That(FindItem(menu, TrayCommand.Exit).Enabled, Is.True);
            });
        });
    }
    [Test]
    public void PauseAndResumeStayMutuallyExclusive() {
        RunOnStaThread(() => {
            using TrayCommandMenu menu = new(_ => { });
            TrayCommandCapabilities capabilities = TrayCommandCapabilities.PauseTracking | TrayCommandCapabilities.ResumeTracking;
            menu.Update(new TrayMenuState(TrayStatus.Active, capabilities, false));
            Assert.Multiple(() => {
                Assert.That(FindItem(menu, TrayCommand.PauseTracking).Enabled, Is.True);
                Assert.That(FindItem(menu, TrayCommand.ResumeTracking).Enabled, Is.False);
            });
            menu.Update(new TrayMenuState(new TrayStatus(TrayActivityState.Paused, false, false), capabilities, false));
            Assert.Multiple(() => {
                Assert.That(FindItem(menu, TrayCommand.PauseTracking).Enabled, Is.False);
                Assert.That(FindItem(menu, TrayCommand.ResumeTracking).Enabled, Is.True);
            });
        });
    }
    [Test]
    public void AutostartCheckmarkReflectsPersistedState() {
        RunOnStaThread(() => {
            using TrayCommandMenu menu = new(_ => { });
            menu.Update(new TrayMenuState(TrayStatus.Active, TrayCommandCapabilities.ToggleAutostart, true));
            Assert.That(FindItem(menu, TrayCommand.ToggleAutostart).Checked, Is.True);
            menu.Update(new TrayMenuState(TrayStatus.Active, TrayCommandCapabilities.ToggleAutostart, false));
            Assert.That(FindItem(menu, TrayCommand.ToggleAutostart).Checked, Is.False);
        });
    }
    [Test]
    public void ClickingAnItemForwardsOnlyItsOwnCommand() {
        RunOnStaThread(() => {
            List<TrayCommand> requestedCommands = [];
            using TrayCommandMenu menu = new(requestedCommands.Add);
            menu.Update(new TrayMenuState(TrayStatus.Active, TrayCommandCapabilities.Exit, false));
            FindItem(menu, TrayCommand.Exit).PerformClick();
            Assert.That(requestedCommands, Is.EqualTo(new[] { TrayCommand.Exit }));
        });
    }
    [Test]
    public void UpdateThrowsAfterDispose() {
        RunOnStaThread(() => {
            TrayCommandMenu menu = new(_ => { });
            menu.Dispose();
            Assert.That(() => menu.Update(TrayMenuState.Initial), Throws.TypeOf<ObjectDisposedException>());
        });
    }
    static ToolStripMenuItem FindItem(TrayCommandMenu menu, TrayCommand command) {
        string text = command switch {
            TrayCommand.PauseTracking => "Pause tracking",
            TrayCommand.ResumeTracking => "Resume tracking",
            TrayCommand.ToggleAutostart => "Start with Windows",
            TrayCommand.Exit => "Exit",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unmapped test command text.")
        };
        return menu.ContextMenu.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().Single(item => item.Text == text);
    }
    static void RunOnStaThread(Action action) {
        Exception? failure = null;
        Thread thread = new(() => {
            try { action(); }
            catch(Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.That(thread.Join(TimeSpan.FromSeconds(5)), Is.True);
        if(failure is not null)
            throw failure;
    }
}
