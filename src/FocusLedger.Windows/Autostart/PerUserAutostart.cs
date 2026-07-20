namespace FocusLedger.Windows.Autostart;

using Microsoft.Win32;

// Manages the current user's opt-in startup command and reports stale portable executable paths.
public sealed class PerUserAutostart {
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    readonly IAutostartRegistry registry;
    readonly string valueName;
    readonly string expectedCommand;
    public PerUserAutostart(string executablePath, string valueName = "FocusLedger", string arguments = "--autostart")
        : this(new CurrentUserAutostartRegistry(), executablePath, valueName, arguments) {
    }
    internal PerUserAutostart(
        IAutostartRegistry registry,
        string executablePath,
        string valueName,
        string arguments) {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);
        if(!Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("The executable path must be fully qualified.", nameof(executablePath));
        if(executablePath.Contains('"', StringComparison.Ordinal))
            throw new ArgumentException("The executable path contains an unsupported character.", nameof(executablePath));
        this.valueName = valueName;
        expectedCommand = $"\"{executablePath}\" {arguments}";
    }
    public AutostartState GetState() {
        string? configuredCommand = registry.Read(RunKeyPath, valueName);
        if(configuredCommand is null)
            return AutostartState.Disabled;
        return string.Equals(configuredCommand, expectedCommand, StringComparison.OrdinalIgnoreCase)
            ? AutostartState.Enabled
            : AutostartState.InvalidPath;
    }
    public AutostartState Enable() {
        registry.Write(RunKeyPath, valueName, expectedCommand);
        return AutostartState.Enabled;
    }
    public AutostartState Disable() {
        registry.Delete(RunKeyPath, valueName);
        return AutostartState.Disabled;
    }
}

public enum AutostartState {
    Disabled,
    Enabled,
    InvalidPath
}

interface IAutostartRegistry {
    string? Read(string keyPath, string valueName);
    void Write(string keyPath, string valueName, string value);
    void Delete(string keyPath, string valueName);
}

sealed class CurrentUserAutostartRegistry : IAutostartRegistry {
    public string? Read(string keyPath, string valueName) {
        using(RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, false)) {
            return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
    }
    public void Write(string keyPath, string valueName, string value) {
        using(RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath, true)) {
            key.SetValue(valueName, value, RegistryValueKind.String);
        }
    }
    public void Delete(string keyPath, string valueName) {
        using(RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyPath, true)) {
            key?.DeleteValue(valueName, false);
        }
    }
}
