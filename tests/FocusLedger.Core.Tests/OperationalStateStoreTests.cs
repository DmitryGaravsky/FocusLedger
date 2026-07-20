using System.Text.Json;
using FocusLedger.Core.Persistence;

namespace FocusLedger.Core.Tests;

public sealed class OperationalStateStoreTests {
    static readonly string[] ExpectedPropertyNames = ["schemaVersion", "nextSequence", "manualPause", "cleanShutdown"];
    [Test]
    public async Task FirstRunCreatesDirtyInitialStateWithoutReportingRecovery() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalStateStore store = CreateStore(rootPath);
            OperationalStateInitialization initialization = await store.BeginRunAsync(CancellationToken.None);
            OperationalStateLoadResult persisted = await store.LoadAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.WasPreviousShutdownClean, Is.True);
                Assert.That(initialization.RecoveredFromInvalidState, Is.False);
                Assert.That(initialization.State.NextSequence, Is.EqualTo(1));
                Assert.That(persisted.State.CleanShutdown, Is.False);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task ProgressAndCleanShutdownPreserveSequenceAndManualPause() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalStateStore store = CreateStore(rootPath);
            await store.BeginRunAsync(CancellationToken.None);
            await store.SaveProgressAsync(42, true, CancellationToken.None);
            OperationalStateLoadResult progress = await store.LoadAsync(CancellationToken.None);
            await store.MarkCleanShutdownAsync(43, true, CancellationToken.None);
            OperationalStateLoadResult stopped = await store.LoadAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(progress.State, Is.EqualTo(new OperationalState(1, 42, true, false)));
                Assert.That(stopped.State, Is.EqualTo(new OperationalState(1, 43, true, true)));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task DirtyPreviousRunIsReportedWithoutChangingRecoveryValues() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalStateStore firstStore = CreateStore(rootPath);
            await firstStore.BeginRunAsync(CancellationToken.None);
            await firstStore.SaveProgressAsync(77, true, CancellationToken.None);
            await firstStore.DisposeAsync();
            await using OperationalStateStore recoveredStore = CreateStore(rootPath);
            OperationalStateInitialization initialization = await recoveredStore.BeginRunAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.WasPreviousShutdownClean, Is.False);
                Assert.That(initialization.RecoveredFromInvalidState, Is.False);
                Assert.That(initialization.State.NextSequence, Is.EqualTo(77));
                Assert.That(initialization.State.ManualPause, Is.True);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task MalformedStateResetsSafelyAndReportsRecovery() {
        string rootPath = CreateRootPath();
        try {
            string statePath = GetStatePath(rootPath);
            await File.WriteAllTextAsync(statePath, "{not-valid-json");
            await using OperationalStateStore store = CreateStore(rootPath);
            OperationalStateInitialization initialization = await store.BeginRunAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(initialization.WasPreviousShutdownClean, Is.False);
                Assert.That(initialization.RecoveredFromInvalidState, Is.True);
                Assert.That(initialization.State, Is.EqualTo(OperationalState.Initial with { CleanShutdown = false }));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task ReaderIgnoresUnknownAdditiveProperties() {
        string rootPath = CreateRootPath();
        try {
            string json = """
                {"schemaVersion":1,"nextSequence":9,"manualPause":false,"cleanShutdown":true,"futureProperty":"ignored"}
                """;
            await File.WriteAllTextAsync(GetStatePath(rootPath), json);
            await using OperationalStateStore store = CreateStore(rootPath);
            OperationalStateLoadResult result = await store.LoadAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(result.Status, Is.EqualTo(OperationalStateLoadStatus.Loaded));
                Assert.That(result.State.NextSequence, Is.EqualTo(9));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [TestCase("{\"schemaVersion\":2,\"nextSequence\":9,\"manualPause\":false,\"cleanShutdown\":true}")]
    [TestCase("{\"schemaVersion\":1,\"nextSequence\":0,\"manualPause\":false,\"cleanShutdown\":true}")]
    public async Task UnsupportedOrInvalidStateUsesSafeDefaults(string json) {
        string rootPath = CreateRootPath();
        try {
            await File.WriteAllTextAsync(GetStatePath(rootPath), json);
            await using OperationalStateStore store = CreateStore(rootPath);
            OperationalStateLoadResult result = await store.LoadAsync(CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(result.Status, Is.EqualTo(OperationalStateLoadStatus.Invalid));
                Assert.That(result.State, Is.EqualTo(OperationalState.Initial));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task OversizedStateIsRejectedBeforeJsonParsing() {
        string rootPath = CreateRootPath();
        try {
            await File.WriteAllBytesAsync(GetStatePath(rootPath), new byte[(64 * 1024) + 1]);
            await using OperationalStateStore store = CreateStore(rootPath);
            OperationalStateLoadResult result = await store.LoadAsync(CancellationToken.None);
            Assert.That(result.Status, Is.EqualTo(OperationalStateLoadStatus.Invalid));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task AtomicReplacementLeavesOneCanonicalPrivacySafeFile() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalStateStore store = CreateStore(rootPath);
            await store.SaveProgressAsync(12, true, CancellationToken.None);
            await store.MarkCleanShutdownAsync(13, false, CancellationToken.None);
            string statePath = GetStatePath(rootPath);
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllBytesAsync(statePath));
            string[] propertyNames = document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray();
            Assert.Multiple(() => {
                Assert.That(propertyNames, Is.EqualTo(ExpectedPropertyNames));
                Assert.That(File.Exists($"{statePath}.tmp"), Is.False);
                Assert.That(Directory.GetFiles(rootPath), Has.Length.EqualTo(1));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task ReplacementRetriesWhileDestinationIsTransientlyLockedAndSucceeds() {
        string rootPath = CreateRootPath();
        try {
            await using OperationalStateStore store = CreateStore(rootPath);
            await store.SaveProgressAsync(1, false, CancellationToken.None);
            using(FileStream exclusiveLock = new(GetStatePath(rootPath), FileMode.Open, FileAccess.Read, FileShare.None)) {
                Task saveTask = store.SaveProgressAsync(99, true, CancellationToken.None).AsTask();
                await Task.Delay(TimeSpan.FromMilliseconds(60));
                exclusiveLock.Dispose();
                await saveTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            OperationalStateLoadResult result = await store.LoadAsync(CancellationToken.None);
            Assert.That(result.State.NextSequence, Is.EqualTo(99));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static OperationalStateStore CreateStore(string rootPath) {
        return new OperationalStateStore(GetStatePath(rootPath));
    }
    static string GetStatePath(string rootPath) {
        return Path.Combine(rootPath, "state.json");
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"state-store-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
