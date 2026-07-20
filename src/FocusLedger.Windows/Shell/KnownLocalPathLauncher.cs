namespace FocusLedger.Windows.Shell;

using System.Diagnostics;

// Opens only application-owned paths derived from one trusted storage root after an explicit user command.
public sealed class KnownLocalPathLauncher {
    readonly ILocalShell shell;
    readonly ILocalDirectoryStorage directoryStorage;
    readonly string dataPath;
    readonly string reportsPath;
    readonly string configurationPath;
    public KnownLocalPathLauncher(string storageRootPath)
        : this(new WindowsLocalShell(), new LocalDirectoryStorage(), storageRootPath) {
    }
    internal KnownLocalPathLauncher(
        ILocalShell shell,
        ILocalDirectoryStorage directoryStorage,
        string storageRootPath) {
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.directoryStorage = directoryStorage ?? throw new ArgumentNullException(nameof(directoryStorage));
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRootPath);
        string rootPath = Path.GetFullPath(storageRootPath);
        dataPath = Path.Combine(rootPath, "data");
        reportsPath = Path.Combine(rootPath, "reports");
        configurationPath = Path.Combine(rootPath, "config.json");
    }
    public void Open(KnownLocalPath target) {
        string path = target switch {
            KnownLocalPath.DataFolder => EnsureDirectory(dataPath),
            KnownLocalPath.ReportsFolder => EnsureDirectory(reportsPath),
            KnownLocalPath.Configuration => configurationPath,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown local path target.")
        };
        shell.Open(path);
    }
    string EnsureDirectory(string path) {
        directoryStorage.Create(path);
        return path;
    }
}

public enum KnownLocalPath {
    DataFolder,
    ReportsFolder,
    Configuration
}

interface ILocalShell {
    void Open(string path);
}

interface ILocalDirectoryStorage {
    void Create(string path);
}

sealed class WindowsLocalShell : ILocalShell {
    public void Open(string path) {
        ProcessStartInfo startInfo = new() {
            FileName = path,
            UseShellExecute = true
        };
        using(Process? process = Process.Start(startInfo)) {
            // Shell activation may succeed without returning a process object; dispose one only when supplied.
        }
    }
}

sealed class LocalDirectoryStorage : ILocalDirectoryStorage {
    public void Create(string path) {
        Directory.CreateDirectory(path);
    }
}
