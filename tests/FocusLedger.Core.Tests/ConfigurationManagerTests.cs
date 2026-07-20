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
    static ConfigurationManager CreateManager(string configurationPath) {
        return new ConfigurationManager(configurationPath, new ConfigurationValidator(), TimeProvider.System, TimeSpan.FromMilliseconds(50));
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"configuration-manager-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
