using FocusLedger.Windows.Shell;

namespace FocusLedger.Windows.Tests;

public sealed class KnownLocalPathLauncherTests {
    const string StorageRoot = @"C:\Users\Example\AppData\Local\FocusLedger";
    [TestCase(KnownLocalPath.DataFolder, "data", true)]
    [TestCase(KnownLocalPath.ReportsFolder, "reports", true)]
    [TestCase(KnownLocalPath.Configuration, "config.json", false)]
    public void OpenUsesOnlyPathDerivedFromStorageRoot(KnownLocalPath target, string childName, bool createsDirectory) {
        RecordingShell shell = new();
        RecordingDirectoryStorage storage = new();
        KnownLocalPathLauncher launcher = new(shell, storage, StorageRoot);
        launcher.Open(target);
        string expectedPath = Path.Combine(StorageRoot, childName);
        Assert.Multiple(() => {
            Assert.That(shell.OpenedPath, Is.EqualTo(expectedPath));
            Assert.That(storage.CreatedPath, Is.EqualTo(createsDirectory ? expectedPath : null));
        });
    }
    [Test]
    public void UnknownTargetIsRejectedWithoutOpeningShell() {
        RecordingShell shell = new();
        RecordingDirectoryStorage storage = new();
        KnownLocalPathLauncher launcher = new(shell, storage, StorageRoot);
        Assert.That(
            () => launcher.Open((KnownLocalPath)999),
            Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(shell.OpenedPath, Is.Null);
    }
    sealed class RecordingShell : ILocalShell {
        public string? OpenedPath { get; set; }
        public void Open(string path) {
            OpenedPath = path;
        }
    }
    sealed class RecordingDirectoryStorage : ILocalDirectoryStorage {
        public string? CreatedPath { get; set; }
        public void Create(string path) {
            CreatedPath = path;
        }
    }
}
