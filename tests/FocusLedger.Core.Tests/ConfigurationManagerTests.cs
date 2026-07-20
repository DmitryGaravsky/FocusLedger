using FocusLedger.Core.Configuration;

namespace FocusLedger.Core.Tests;

public sealed class ConfigurationManagerTests {
    [Test]
    public async Task FirstRunCreatesAndActivatesCompleteDefault() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                ConfigurationReloadResult result = await manager.InitializeAsync(CancellationToken.None);
                Assert.Multiple(() => {
                    Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.CreatedDefault));
                    Assert.That(File.Exists(configurationPath), Is.True);
                    Assert.That(manager.Current.Categories.Length, Is.EqualTo(25));
                    Assert.That(manager.Current.Privacy.Mode, Is.EqualTo("balanced"));
                });
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task ValidReloadAtomicallyReplacesSnapshot() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                await manager.InitializeAsync(CancellationToken.None);
                FocusLedgerConfiguration updated = manager.Current with {
                    Tracking = manager.Current.Tracking with { IdleThresholdSeconds = 600 }
                };
                await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(updated));
                ConfigurationReloadResult result = await manager.ReloadAsync(CancellationToken.None);
                Assert.Multiple(() => {
                    Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.Reloaded));
                    Assert.That(manager.Current.Tracking.IdleThresholdSeconds, Is.EqualTo(600));
                });
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task InvalidReloadKeepsPreviousSnapshotAndReturnsSafeErrors() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                await manager.InitializeAsync(CancellationToken.None);
                FocusLedgerConfiguration previous = manager.Current;
                FocusLedgerConfiguration invalid = previous with {
                    Privacy = previous.Privacy with { PersistRawWindowTitles = true }
                };
                await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(invalid));
                ConfigurationReloadResult result = await manager.ReloadAsync(CancellationToken.None);
                Assert.Multiple(() => {
                    Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.Invalid));
                    Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.UnsafePrivacySetting));
                    Assert.That(manager.Current, Is.SameAs(previous));
                });
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task WatcherDebouncesChangeAndPublishesReloadResult() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                await manager.InitializeAsync(CancellationToken.None);
                using(CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5))) {
                    TaskCompletionSource<ConfigurationReloadResult> reloadReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    Task watcherTask = manager.RunAsync((result, _) => {
                        reloadReceived.TrySetResult(result);
                        return ValueTask.CompletedTask;
                    }, cancellationSource.Token);
                    FocusLedgerConfiguration updated = manager.Current with {
                        Tracking = manager.Current.Tracking with { HeartbeatIntervalSeconds = 120 }
                    };
                    await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(updated));
                    ConfigurationReloadResult result = await reloadReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    await cancellationSource.CancelAsync();
                    Assert.ThrowsAsync<OperationCanceledException>(async () => await watcherTask);
                    Assert.Multiple(() => {
                        Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.Reloaded));
                        Assert.That(manager.Current.Tracking.HeartbeatIntervalSeconds, Is.EqualTo(120));
                    });
                }
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task WatcherKeepsValidSnapshotAfterInvalidChangeAndAcceptsRepair() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                await manager.InitializeAsync(CancellationToken.None);
                FocusLedgerConfiguration initial = manager.Current;
                using(CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(10))) {
                    TaskCompletionSource<ConfigurationReloadResult> invalidReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    TaskCompletionSource<ConfigurationReloadResult> repairedReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    Task watcherTask = manager.RunAsync((result, _) => {
                        if(result.Status == ConfigurationReloadStatus.Invalid)
                            invalidReceived.TrySetResult(result);
                        if(result.Status == ConfigurationReloadStatus.Reloaded
                            && manager.Current.Tracking.IdleThresholdSeconds == 420) {
                            repairedReceived.TrySetResult(result);
                        }
                        return ValueTask.CompletedTask;
                    }, cancellationSource.Token);
                    FocusLedgerConfiguration invalid = initial with {
                        Privacy = initial.Privacy with { PersistUrls = true }
                    };
                    await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(invalid));
                    ConfigurationReloadResult invalidResult = await invalidReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Multiple(() => {
                        Assert.That(invalidResult.Status, Is.EqualTo(ConfigurationReloadStatus.Invalid));
                        Assert.That(manager.Current, Is.SameAs(initial));
                    });
                    FocusLedgerConfiguration repaired = initial with {
                        Tracking = initial.Tracking with { IdleThresholdSeconds = 420 }
                    };
                    await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(repaired));
                    ConfigurationReloadResult repairedResult = await repairedReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    await cancellationSource.CancelAsync();
                    Assert.ThrowsAsync<OperationCanceledException>(async () => await watcherTask);
                    Assert.Multiple(() => {
                        Assert.That(repairedResult.Status, Is.EqualTo(ConfigurationReloadStatus.Reloaded));
                        Assert.That(manager.Current.Tracking.IdleThresholdSeconds, Is.EqualTo(420));
                    });
                }
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task OversizedConfigurationFileIsRejectedAsReadFailure() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            byte[] oversizedContent = new byte[(2 * 1024 * 1024) + 1];
            Array.Fill(oversizedContent, (byte)' ');
            await File.WriteAllBytesAsync(configurationPath, oversizedContent);
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                ConfigurationReloadResult result = await manager.InitializeAsync(CancellationToken.None);
                Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.ReadFailure));
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task ReadRetriesTransientSharingViolationAndSucceeds() {
        string rootPath = CreateRootPath();
        try {
            string configurationPath = Path.Combine(rootPath, "config.json");
            await File.WriteAllBytesAsync(configurationPath, ConfigurationSerializer.Serialize(BuiltInConfiguration.CreateDefault()));
            await using(ConfigurationManager manager = CreateManager(configurationPath)) {
                using(FileStream exclusiveLock = new(configurationPath, FileMode.Open, FileAccess.Read, FileShare.None)) {
                    Task<ConfigurationReloadResult> initializeTask = manager.InitializeAsync(CancellationToken.None).AsTask();
                    await Task.Delay(TimeSpan.FromMilliseconds(80));
                    exclusiveLock.Dispose();
                    ConfigurationReloadResult result = await initializeTask.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.That(result.Status, Is.EqualTo(ConfigurationReloadStatus.Loaded));
                }
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static ConfigurationManager CreateManager(string configurationPath) {
        return new ConfigurationManager(configurationPath, new ConfigurationValidator(), TimeProvider.System, TimeSpan.FromMilliseconds(50));
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"configuration-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
