using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Events;

namespace FocusLedger.Core.Persistence;

enum DailyWriterState {
    Ready,
    AwaitingDayStarted,
    AwaitingStateSnapshot
}

public sealed record DailyJsonlActivityEventWriterMetrics(DateOnly? CurrentDate, long RolloverCount);

// Routes a pre-sequenced coordinator stream into independently analyzable local-day JSONL files.
public sealed class DailyJsonlActivityEventWriter : IActivityEventWriter {
    readonly string dataRootPath;
    readonly TimeSpan flushInterval;
    readonly TimeProvider timeProvider;
    readonly SemaphoreSlim writerGate = new(1, 1);
    JsonlActivityEventWriter? currentWriter;
    DateOnly? currentDate;
    DailyWriterState state;
    long rolloverCount;
    bool disposed;
    public DailyJsonlActivityEventWriter(string dataRootPath, TimeSpan flushInterval, TimeProvider timeProvider) {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(flushInterval, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.dataRootPath = Path.GetFullPath(dataRootPath);
        this.flushInterval = flushInterval;
        this.timeProvider = timeProvider;
    }
    public async ValueTask DisposeAsync() {
        if(disposed)
            return;
        disposed = true;
        await writerGate.WaitAsync()
            .ConfigureAwait(false);
        try {
            if(currentWriter is not null)
                await currentWriter.DisposeAsync()
                    .ConfigureAwait(false);
            currentWriter = null;
        }
        finally {
            writerGate.Release();
            writerGate.Dispose();
        }
    }
    public DailyJsonlActivityEventWriterMetrics GetMetrics() {
        return new DailyJsonlActivityEventWriterMetrics(currentDate, Interlocked.Read(ref rolloverCount));
    }
    public async ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(activityEvent);
        ObjectDisposedException.ThrowIf(disposed, this);
        await writerGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            ObjectDisposedException.ThrowIf(disposed, this);
            await AppendOrderedAsync(activityEvent, cancellationToken)
                .ConfigureAwait(false);
        }
        finally { writerGate.Release(); }
    }
    public async ValueTask FlushAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        await writerGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        try {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(currentWriter is not null)
                await currentWriter.FlushAsync(cancellationToken)
                    .ConfigureAwait(false);
        }
        finally { writerGate.Release(); }
    }
    async ValueTask AppendOrderedAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
        string eventType = activityEvent.Envelope.Type;
        DateOnly eventDate = ResolveLocalDate(activityEvent.Envelope);
        if(currentWriter is null && OpenInitialWriter(eventDate, eventType)) {
            await currentWriter!.AppendAsync(activityEvent, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        if(state == DailyWriterState.AwaitingDayStarted) {
            if(eventType != "day.started")
                throw new InvalidOperationException("A completed daily file must be followed by day.started.");
            await RotateAsync(eventDate)
                .ConfigureAwait(false);
            await currentWriter!.AppendAsync(activityEvent, cancellationToken)
                .ConfigureAwait(false);
            state = DailyWriterState.AwaitingStateSnapshot;
            return;
        }
        if(state == DailyWriterState.AwaitingStateSnapshot) {
            if(eventType != "state.snapshot")
                throw new InvalidOperationException("A new daily file must begin with state.snapshot after day.started.");
            await currentWriter!.AppendAsync(activityEvent, cancellationToken)
                .ConfigureAwait(false);
            state = DailyWriterState.Ready;
            return;
        }
        if(eventType == "day.ended") {
            await currentWriter!.AppendAsync(activityEvent, cancellationToken)
                .ConfigureAwait(false);
            state = DailyWriterState.AwaitingDayStarted;
            return;
        }
        if(eventType == "day.started")
            throw new InvalidOperationException("day.started is valid only while opening a new daily file.");
        if(eventDate != currentDate)
            throw new InvalidOperationException("A local-date transition requires day.ended, day.started, and state.snapshot events.");
        await currentWriter!.AppendAsync(activityEvent, cancellationToken)
            .ConfigureAwait(false);
    }
    bool OpenInitialWriter(DateOnly eventDate, string eventType) {
        string filePath = GetFilePath(eventDate);
        bool existingFileHasEvents = File.Exists(filePath) && new FileInfo(filePath).Length > 0;
        if(!existingFileHasEvents && eventType != "day.started")
            throw new InvalidOperationException("A new daily file must begin with day.started.");
        if(existingFileHasEvents && eventType == "day.started")
            throw new InvalidOperationException("An existing daily file cannot receive another day.started event.");
        currentWriter = new JsonlActivityEventWriter(filePath, flushInterval, timeProvider);
        currentDate = eventDate;
        state = existingFileHasEvents ? DailyWriterState.Ready : DailyWriterState.AwaitingStateSnapshot;
        return !existingFileHasEvents;
    }
    async ValueTask RotateAsync(DateOnly eventDate) {
        if(currentDate is not null && eventDate <= currentDate)
            throw new InvalidOperationException("Daily rollover must advance to a later local date.");
        await currentWriter!.DisposeAsync()
            .ConfigureAwait(false);
        currentWriter = new JsonlActivityEventWriter(GetFilePath(eventDate), flushInterval, timeProvider);
        currentDate = eventDate;
        Interlocked.Increment(ref rolloverCount);
    }
    string GetFilePath(DateOnly date) {
        return ActivityFileNaming.GetFilePath(dataRootPath, date);
    }
    static DateOnly ResolveLocalDate(EventEnvelope envelope) {
        TimeSpan offset = TimeSpan.FromMinutes(envelope.UtcOffsetMinutes);
        return DateOnly.FromDateTime(envelope.TimestampUtc.ToOffset(offset).DateTime);
    }
}
