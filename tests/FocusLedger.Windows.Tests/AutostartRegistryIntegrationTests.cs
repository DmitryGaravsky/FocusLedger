using FocusLedger.Windows.Autostart;
using Microsoft.Win32;

namespace FocusLedger.Windows.Tests;

public sealed class AutostartRegistryIntegrationTests {
    const string ExecutablePath = @"C:\Tools\FocusLedger\FocusLedger.exe";
    [Test]
    public void IsolatedCurrentUserKeySupportsEnableStalePathDetectionAndDisable() {
        string testRootPath = $@"Software\FocusLedger.Tests.{Guid.NewGuid():N}";
        string testKeyPath = $@"{testRootPath}\Autostart";
        const string ValueName = "FocusLedger";
        try {
            CurrentUserAutostartRegistry registry = new();
            PerUserAutostart autostart = new(registry, ExecutablePath, ValueName, "--autostart", testKeyPath);
            Assert.That(autostart.GetState(), Is.EqualTo(AutostartState.Disabled));
            Assert.That(autostart.Enable(), Is.EqualTo(AutostartState.Enabled));
            using(RegistryKey? key = Registry.CurrentUser.OpenSubKey(testKeyPath, true)) {
                Assert.That(key, Is.Not.Null);
                Assert.That(key!.GetValue(ValueName), Is.EqualTo("\"C:\\Tools\\FocusLedger\\FocusLedger.exe\" --autostart"));
                key.SetValue(ValueName, "\"C:\\Moved\\FocusLedger.exe\" --autostart", RegistryValueKind.String);
            }
            Assert.That(autostart.GetState(), Is.EqualTo(AutostartState.InvalidPath));
            Assert.That(autostart.Disable(), Is.EqualTo(AutostartState.Disabled));
            using(RegistryKey? key = Registry.CurrentUser.OpenSubKey(testKeyPath, false)) {
                Assert.That(key?.GetValue(ValueName), Is.Null);
            }
        }
        finally {
            Assert.That(testRootPath, Does.StartWith(@"Software\FocusLedger.Tests."));
            Registry.CurrentUser.DeleteSubKeyTree(testRootPath, false);
        }
    }
}
