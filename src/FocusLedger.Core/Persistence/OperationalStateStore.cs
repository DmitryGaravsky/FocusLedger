using System.Text.Json;

namespace FocusLedger.Core.Persistence;

// Loads and atomically replaces the privacy-safe state file used for crash and sequence recovery.
public sealed class OperationalStateStore : IAsyncDisposable {
    const int MaximumStateFileBytes = 64 * 1024;
    readonly string stateFilePath;
    readonly string temporaryFilePath;
    readonly SemaphoreSlim stateGate = new(1, 1);
    bool disposed;
    public OperationalStateStore(string stateFilePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        this.stateFilePath = Path.GetFullPath(stateFilePath);
        temporaryFilePath = $"{this.stateFilePath}.tmp";
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await stateGate.WaitAsync().ConfigureAwait(false);
        stateGate.Release();
        stateGate.Dispose();
    }
    public async ValueTask<OperationalStateLoadResult> LoadAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { stateGate.Release(); }
    }
    // Marks the new run dirty before collection starts and returns the previous shutdown outcome.
    public async ValueTask<OperationalStateInitialization> BeginRunAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            OperationalStateLoadResult loaded = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            bool recoveredFromInvalidState = loaded.Status == OperationalStateLoadStatus.Invalid;
            bool wasPreviousShutdownClean = loaded.Status != OperationalStateLoadStatus.Invalid
                && (loaded.Status == OperationalStateLoadStatus.Missing || loaded.State.CleanShutdown);
            OperationalState runningState = loaded.State with { CleanShutdown = false };
            await SaveCoreAsync(runningState, cancellationToken).ConfigureAwait(false);
            return new OperationalStateInitialization(runningState, wasPreviousShutdownClean, recoveredFromInvalidState);
        }
        finally { stateGate.Release(); }
    }
    // Persists sequence and pause progress while retaining the dirty current-run marker.
    public async ValueTask SaveProgressAsync(long nextSequence, bool manualPause, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextSequence, 1);
        await SaveAsync(new OperationalState(1, nextSequence, manualPause, false), cancellationToken).ConfigureAwait(false);
    }
    // Atomically commits the final sequence and pause state after all event writers have flushed.
    public async ValueTask MarkCleanShutdownAsync(long nextSequence, bool manualPause, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextSequence, 1);
        await SaveAsync(new OperationalState(1, nextSequence, manualPause, true), cancellationToken).ConfigureAwait(false);
    }
    async ValueTask SaveAsync(OperationalState state, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await SaveCoreAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally { stateGate.Release(); }
    }
    async ValueTask<OperationalStateLoadResult> LoadCoreAsync(CancellationToken cancellationToken) {
        if(!File.Exists(stateFilePath))
            return new OperationalStateLoadResult(OperationalState.Initial, OperationalStateLoadStatus.Missing);
        try {
            FileInfo stateFile = new(stateFilePath);
            if(stateFile.Length is <= 0 or > MaximumStateFileBytes)
                return new OperationalStateLoadResult(OperationalState.Initial, OperationalStateLoadStatus.Invalid);
            byte[] utf8Json = await File.ReadAllBytesAsync(stateFilePath, cancellationToken).ConfigureAwait(false);
            OperationalState? state = JsonSerializer.Deserialize(utf8Json, OperationalStateJsonContext.Default.OperationalState);
            if(state is null || state.SchemaVersion != 1 || state.NextSequence < 1)
                return new OperationalStateLoadResult(OperationalState.Initial, OperationalStateLoadStatus.Invalid);
            return new OperationalStateLoadResult(state, OperationalStateLoadStatus.Loaded);
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch {
            return new OperationalStateLoadResult(OperationalState.Initial, OperationalStateLoadStatus.Invalid);
        }
    }
    async ValueTask SaveCoreAsync(OperationalState state, CancellationToken cancellationToken) {
        byte[] utf8Json = JsonSerializer.SerializeToUtf8Bytes(state, OperationalStateJsonContext.Default.OperationalState);
        string? directoryPath = Path.GetDirectoryName(stateFilePath);
        try {
            if(directoryPath is not null)
                Directory.CreateDirectory(directoryPath);
            using(FileStream temporaryStream = new(
                temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough)) {
                await temporaryStream.WriteAsync(utf8Json, cancellationToken).ConfigureAwait(false);
                await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                temporaryStream.Flush(true);
            }
            await ReplaceTemporaryFileAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch {
            throw new IOException("The operational state file could not be replaced atomically.");
        }
        finally {
            try { File.Delete(temporaryFilePath); }
            catch { }
        }
    }
    async ValueTask ReplaceTemporaryFileAsync(CancellationToken cancellationToken) {
        const int MaximumAttempts = 4;
        for(int attempt = 1; attempt <= MaximumAttempts; attempt++) {
            try {
                File.Move(temporaryFilePath, stateFilePath, true);
                return;
            }
            catch(Exception exception) when(exception is IOException or UnauthorizedAccessException) {
                if(attempt == MaximumAttempts)
                    throw;
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
