using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Channels;

namespace FocusLedger.Core.Configuration;

public enum ConfigurationReloadStatus {
    CreatedDefault,
    Loaded,
    Reloaded,
    Invalid,
    ReadFailure
}

public sealed record ConfigurationReloadResult(
    ConfigurationReloadStatus Status,
    ImmutableArray<ConfigurationValidationError> Errors);

// Owns first-run configuration creation, validation, atomic activation, and bounded file-change observation.
public sealed class ConfigurationManager : IConfigurationSnapshotProvider<FocusLedgerConfiguration>, IAsyncDisposable {
    const int MaximumConfigurationBytes = 2 * 1024 * 1024;
    readonly string configurationPath;
    readonly string temporaryPath;
    readonly ConfigurationValidator validator;
    readonly TimeProvider timeProvider;
    readonly TimeSpan debounceInterval;
    readonly Channel<bool> reloadSignals;
    readonly SemaphoreSlim reloadGate = new(1, 1);
    FileSystemWatcher? watcher;
    FocusLedgerConfiguration current;
    bool initialized;
    bool disposed;
    public ConfigurationManager(
        string configurationPath,
        ConfigurationValidator validator,
        TimeProvider timeProvider,
        TimeSpan debounceInterval) {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(debounceInterval, TimeSpan.Zero);
        this.configurationPath = Path.GetFullPath(configurationPath);
        if(Path.GetDirectoryName(this.configurationPath) is null)
            throw new ArgumentException("Configuration path must not be a root path.", nameof(configurationPath));
        temporaryPath = $"{this.configurationPath}.tmp";
        this.validator = validator;
        this.timeProvider = timeProvider;
        this.debounceInterval = debounceInterval;
        current = BuiltInConfiguration.CreateDefault();
        reloadSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        watcher?.Dispose();
        watcher = null;
        reloadSignals.Writer.TryComplete();
        await reloadGate.WaitAsync()
            .ConfigureAwait(false);
        reloadGate.Release();
        reloadGate.Dispose();
    }
    public FocusLedgerConfiguration Current {
        get { return Volatile.Read(ref current); }
    }
    public async ValueTask<ConfigurationReloadResult> InitializeAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await reloadGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if(initialized)
                throw new InvalidOperationException("The configuration manager is already initialized.");
            ConfigurationReloadResult result;
            if(!File.Exists(configurationPath)) {
                await WriteDefaultIfMissingAsync(cancellationToken)
                    .ConfigureAwait(false);
                if(File.Exists(configurationPath))
                    result = await LoadAndActivateCoreAsync(ConfigurationReloadStatus.CreatedDefault, cancellationToken)
                        .ConfigureAwait(false);
                else
                    result = Failure(ConfigurationReloadStatus.ReadFailure);
            }
            else
                result = await LoadAndActivateCoreAsync(ConfigurationReloadStatus.Loaded, cancellationToken)
                    .ConfigureAwait(false);
            initialized = true;
            StartWatcher();
            return result;
        }
        finally { reloadGate.Release(); }
    }
    public async ValueTask<ConfigurationReloadResult> ReloadAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await reloadGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if(!initialized)
                throw new InvalidOperationException("The configuration manager must be initialized first.");
            return await LoadAndActivateCoreAsync(ConfigurationReloadStatus.Reloaded, cancellationToken)
                .ConfigureAwait(false);
        }
        finally { reloadGate.Release(); }
    }
    public async Task RunAsync(
        Func<ConfigurationReloadResult, CancellationToken, ValueTask> resultHandler,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(resultHandler);
        ObjectDisposedException.ThrowIf(disposed, this);
        if(!initialized)
            throw new InvalidOperationException("The configuration manager must be initialized first.");
        await foreach(bool reloadRequested in reloadSignals.Reader.ReadAllAsync(cancellationToken)) {
            if(!reloadRequested)
                continue;
            await Task.Delay(debounceInterval, timeProvider, cancellationToken)
                .ConfigureAwait(false);
            while(reloadSignals.Reader.TryRead(out _)) { }
            ConfigurationReloadResult result = await ReloadAsync(cancellationToken)
                .ConfigureAwait(false);
            await resultHandler(result, cancellationToken)
                .ConfigureAwait(false);
        }
    }
    async ValueTask<ConfigurationReloadResult> LoadAndActivateCoreAsync(
        ConfigurationReloadStatus successStatus,
        CancellationToken cancellationToken) {
        try {
            byte[] json = await ReadWithRetryAsync(cancellationToken)
                .ConfigureAwait(false);
            FocusLedgerConfiguration? candidate = ConfigurationSerializer.Deserialize(json);
            if(candidate is null)
                return Failure(ConfigurationReloadStatus.Invalid);
            ConfigurationValidationResult validation = validator.Validate(candidate);
            if(!validation.IsValid)
                return new ConfigurationReloadResult(ConfigurationReloadStatus.Invalid, validation.Errors);
            Volatile.Write(ref current, candidate);
            return new ConfigurationReloadResult(successStatus, []);
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception exception)
            when(exception is IOException or UnauthorizedAccessException) {
            return Failure(ConfigurationReloadStatus.ReadFailure);
        }
        catch(JsonException) {
            return Failure(ConfigurationReloadStatus.Invalid);
        }
    }
    async ValueTask<byte[]> ReadWithRetryAsync(CancellationToken cancellationToken) {
        const int MaximumAttempts = 4;
        for(int attempt = 1; attempt <= MaximumAttempts; attempt++) {
            try {
                FileInfo file = new(configurationPath);
                if(!file.Exists || file.Length is <= 0 or > MaximumConfigurationBytes)
                    throw new IOException("The configuration file is unavailable or exceeds the allowed size.");
                return await File.ReadAllBytesAsync(configurationPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch(Exception exception)
                when(exception is IOException or UnauthorizedAccessException) {
                if(attempt == MaximumAttempts)
                    throw;
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        throw new IOException("The configuration file could not be read.");
    }
    async ValueTask WriteDefaultIfMissingAsync(CancellationToken cancellationToken) {
        string? directoryPath = Path.GetDirectoryName(configurationPath);
        if(directoryPath is not null)
            Directory.CreateDirectory(directoryPath);
        byte[] json = ConfigurationSerializer.Serialize(BuiltInConfiguration.CreateDefault());
        try {
            using(FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough)) {
                await stream.WriteAsync(json, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporaryPath, configurationPath, false);
        }
        catch(IOException)
            when(File.Exists(configurationPath)) { }
        finally {
            try { File.Delete(temporaryPath); }
            catch { }
        }
    }
    void StartWatcher() {
        string directoryPath = Path.GetDirectoryName(configurationPath)!;
        watcher = new FileSystemWatcher(directoryPath, Path.GetFileName(configurationPath)) {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Changed += HandleFileChange;
        watcher.Created += HandleFileChange;
        watcher.Renamed += HandleFileRename;
        watcher.EnableRaisingEvents = true;
    }
    void HandleFileChange(object sender, FileSystemEventArgs args) {
        reloadSignals.Writer.TryWrite(true);
    }
    void HandleFileRename(object sender, RenamedEventArgs args) {
        reloadSignals.Writer.TryWrite(true);
    }
    static ConfigurationReloadResult Failure(ConfigurationReloadStatus status) {
        return new ConfigurationReloadResult(status, []);
    }
}
