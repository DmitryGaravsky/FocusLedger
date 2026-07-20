using FocusLedger.Windows.Autostart;

namespace FocusLedger.Windows.Tests;

public sealed class PerUserAutostartTests {
    const string ExecutablePath = @"C:\Tools\FocusLedger\FocusLedger.exe";
    const string ExpectedCommand = "\"C:\\Tools\\FocusLedger\\FocusLedger.exe\" --autostart";
    [Test]
    public void MissingEntryIsDisabled() {
        MemoryAutostartRegistry registry = new();
        PerUserAutostart autostart = Create(registry);
        Assert.That(autostart.GetState(), Is.EqualTo(AutostartState.Disabled));
    }
    [Test]
    public void EnableWritesQuotedCommandForCurrentExecutable() {
        MemoryAutostartRegistry registry = new();
        PerUserAutostart autostart = Create(registry);
        AutostartState state = autostart.Enable();
        Assert.Multiple(() => {
            Assert.That(state, Is.EqualTo(AutostartState.Enabled));
            Assert.That(registry.Value, Is.EqualTo(ExpectedCommand));
            Assert.That(autostart.GetState(), Is.EqualTo(AutostartState.Enabled));
        });
    }
    [Test]
    public void DifferentConfiguredCommandIsReportedAsInvalidPath() {
        MemoryAutostartRegistry registry = new() { Value = "\"C:\\Old\\FocusLedger.exe\" --autostart" };
        PerUserAutostart autostart = Create(registry);
        Assert.That(autostart.GetState(), Is.EqualTo(AutostartState.InvalidPath));
    }
    [Test]
    public void ExplicitEnableRepairsInvalidPath() {
        MemoryAutostartRegistry registry = new() { Value = "\"C:\\Old\\FocusLedger.exe\" --autostart" };
        PerUserAutostart autostart = Create(registry);
        autostart.Enable();
        Assert.That(registry.Value, Is.EqualTo(ExpectedCommand));
    }
    [Test]
    public void DisableRemovesExistingEntry() {
        MemoryAutostartRegistry registry = new() { Value = ExpectedCommand };
        PerUserAutostart autostart = Create(registry);
        AutostartState state = autostart.Disable();
        Assert.Multiple(() => {
            Assert.That(state, Is.EqualTo(AutostartState.Disabled));
            Assert.That(registry.Value, Is.Null);
        });
    }
    static PerUserAutostart Create(MemoryAutostartRegistry registry) {
        return new PerUserAutostart(registry, ExecutablePath, "FocusLedger", "--autostart");
    }
    sealed class MemoryAutostartRegistry : IAutostartRegistry {
        public string? Value { get; set; }
        public string? Read(string keyPath, string valueName) {
            return Value;
        }
        public void Write(string keyPath, string valueName, string value) {
            Value = value;
        }
        public void Delete(string keyPath, string valueName) {
            Value = null;
        }
    }
}
