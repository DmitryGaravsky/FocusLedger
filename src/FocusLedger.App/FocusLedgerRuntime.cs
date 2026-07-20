namespace FocusLedger.App;

using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Configuration;
using FocusLedger.Core.Coordination;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;

// Composes the durable operational state and the single daily activity writer owned by the process.
sealed class FocusLedgerRuntime : IAsyncDisposable {
    readonly OperationalEventSession eventSession;
    readonly DailyJsonlActivityEventWriter eventWriter;
    readonly ManualPauseController pauseController;
    readonly ConfigurationManager configurationManager;
    readonly TimeProvider timeProvider;
    readonly string storageRootPath;
    readonly string dataRootPath;
    readonly SemaphoreSlim operationGate = new(1, 1);
    bool initialized;
    bool stopped;
    bool disposed;
    int configurationError;
    public FocusLedgerRuntime(string storageRootPath, TimeProvider timeProvider) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRootPath);
        ArgumentNullException.ThrowIfNull(timeProvider);
        string resolvedRootPath = Path.GetFullPath(storageRootPath);
        this.timeProvider = timeProvider;
        this.storageRootPath = resolvedRootPath;
        dataRootPath = Path.Combine(resolvedRootPath, "data");
        OperationalStateStore stateStore = new(Path.Combine(resolvedRootPath, "state.json"));
        eventSession = new OperationalEventSession(stateStore, timeProvider.LocalTimeZone);
        eventWriter = new DailyJsonlActivityEventWriter(dataRootPath, TimeSpan.FromSeconds(2), timeProvider);
        pauseController = new ManualPauseController(eventSession, eventWriter);
        configurationManager = new ConfigurationManager(
            Path.Combine(resolvedRootPath, "config.json"),
            new ConfigurationValidator(),
            timeProvider,
            TimeSpan.FromMilliseconds(500));
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await operationGate.WaitAsync()
            .ConfigureAwait(false);
        operationGate.Release();
        operationGate.Dispose();
        await pauseController.DisposeAsync()
            .ConfigureAwait(false);
        await configurationManager.DisposeAsync()
            .ConfigureAwait(false);
        await eventWriter.DisposeAsync()
            .ConfigureAwait(false);
        await eventSession.DisposeAsync()
            .ConfigureAwait(false);
    }
    public TrackerLifecycleState State {
        get { return pauseController.State; }
    }
    public bool HasConfigurationError {
        get { return Volatile.Read(ref configurationError) != 0; }
    }
    public FocusLedgerConfiguration Configuration {
        get { return configurationManager.Current; }
    }
    public async ValueTask<TrackerLifecycleState> InitializeAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            if(initialized)
                throw new InvalidOperationException("The application runtime is already initialized.");
            ConfigurationReloadResult configurationResult = await configurationManager.InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            UpdateConfigurationError(configurationResult);
            DateTimeOffset observedAt = timeProvider.GetLocalNow();
            FocusLedgerConfiguration configuration = configurationManager.Current;
            RetentionMaintenance retentionMaintenance = new(storageRootPath);
            await Task.Run(() => retentionMaintenance.Run(
                DateOnly.FromDateTime(observedAt.DateTime),
                configuration.Storage.ActivityRetentionDays,
                configuration.Diagnostics.RetentionDays,
                cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            ManualPauseInitialization initialization = await pauseController.InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
            if(!CurrentActivityFileHasEvents(observedAt))
                await WriteInitialDayBoundaryAsync(observedAt, cancellationToken).ConfigureAwait(false);
            await AppendOperationalEventAsync(OperationalActivitySignalKind.TrackerStarted, observedAt, cancellationToken).ConfigureAwait(false);
            if(initialization.RecoveryRequired)
                await AppendOperationalEventAsync(OperationalActivitySignalKind.RecoveredAfterUncleanShutdown, observedAt, cancellationToken).ConfigureAwait(false);
            await eventWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            initialized = true;
            return pauseController.State;
        }
        finally { operationGate.Release(); }
    }
    public Task RunConfigurationAsync(Func<bool, CancellationToken, ValueTask> resultHandler, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(resultHandler);
        return configurationManager.RunAsync(async (result, token) => {
            UpdateConfigurationError(result);
            await resultHandler(HasConfigurationError, token)
                .ConfigureAwait(false);
        }, cancellationToken);
    }
    public async ValueTask<TrackerLifecycleState> SetPausedAsync(bool paused, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ThrowIfNotRunning();
            await pauseController.SetPausedAsync(paused, timeProvider.GetLocalNow(), cancellationToken).ConfigureAwait(false);
            return pauseController.State;
        }
        finally { operationGate.Release(); }
    }
    public async ValueTask StopAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if(stopped)
                return;
            if(!initialized)
                throw new InvalidOperationException("The application runtime must be initialized first.");
            await eventWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            await eventSession.MarkCleanShutdownAsync(cancellationToken).ConfigureAwait(false);
            stopped = true;
        }
        finally { operationGate.Release(); }
    }
    async ValueTask WriteInitialDayBoundaryAsync(DateTimeOffset observedAt, CancellationToken cancellationToken) {
        EventEnvelope dayStartedEnvelope = await eventSession.CreateEnvelopeAsync("day.started", observedAt, "startup", cancellationToken).ConfigureAwait(false);
        await eventWriter.AppendAsync(new DayBoundaryActivityEvent(dayStartedEnvelope), cancellationToken).ConfigureAwait(false);
        EventEnvelope snapshotEnvelope = await eventSession.CreateEnvelopeAsync("state.snapshot", observedAt, "startup", cancellationToken).ConfigureAwait(false);
        string trackerState = pauseController.State == TrackerLifecycleState.Paused ? "paused" : "running";
        StateSnapshotActivityEvent snapshot = new(snapshotEnvelope, trackerState, "unknown", new MeetingSnapshotEventData("none"), null);
        await eventWriter.AppendAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }
    async ValueTask AppendOperationalEventAsync(
        OperationalActivitySignalKind kind,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken) {
        OperationalActivitySignal signal = new(kind, observedAt, 0);
        OperationalActivityEvent activityEvent = await eventSession.CreateEventAsync(signal, cancellationToken).ConfigureAwait(false);
        await eventWriter.AppendAsync(activityEvent, cancellationToken).ConfigureAwait(false);
    }
    bool CurrentActivityFileHasEvents(DateTimeOffset observedAt) {
        DateOnly date = DateOnly.FromDateTime(observedAt.DateTime);
        string filePath = Path.Combine(
            dataRootPath,
            date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"activity-{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.jsonl");
        return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
    }
    void ThrowIfNotRunning() {
        if(!initialized || stopped)
            throw new InvalidOperationException("The application runtime is not accepting commands.");
    }
    void UpdateConfigurationError(ConfigurationReloadResult result) {
        bool hasError = result.Status is ConfigurationReloadStatus.Invalid or ConfigurationReloadStatus.ReadFailure;
        Volatile.Write(ref configurationError, hasError ? 1 : 0);
    }
}
