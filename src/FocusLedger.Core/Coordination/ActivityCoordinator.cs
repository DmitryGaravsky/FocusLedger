using System.Threading.Channels;
using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Events;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;

namespace FocusLedger.Core.Coordination;

// Serializes all collector signals through one bounded queue and one runtime-state owner.
public sealed class ActivityCoordinator : IActivitySignalSink, IAsyncDisposable {
    readonly Channel<ActivitySignal> channel;
    readonly IActivitySignalProcessor signalProcessor;
    readonly IActivityEventWriter eventWriter;
    readonly ActivityRuntimeState runtimeState = new();
    readonly List<ActivitySignal> pendingCoalescibleSignals = [];
    readonly Lock pendingSignalsLock = new();
    readonly int capacity;
    int queueDepth;
    long coalescedSignalCount;
    long rejectedSignalCount;
    long processedSignalCount;
    int completionRequested;
    int runStarted;
    public ActivityCoordinator(IActivitySignalProcessor signalProcessor, IActivityEventWriter eventWriter, int capacity) {
        ArgumentNullException.ThrowIfNull(signalProcessor);
        ArgumentNullException.ThrowIfNull(eventWriter);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.signalProcessor = signalProcessor;
        this.eventWriter = eventWriter;
        this.capacity = capacity;
        channel = Channel.CreateBounded<ActivitySignal>(new BoundedChannelOptions(capacity) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }
    public TrackerLifecycleState TrackerState {
        get { return runtimeState.TrackerLifecycle.State; }
    }
    public PresenceState PresenceState {
        get { return runtimeState.Presence.State; }
    }
    // Returns a numeric snapshot that is safe to include in diagnostics.
    public ActivityCoordinatorMetrics GetMetrics() {
        return new ActivityCoordinatorMetrics(
            capacity,
            Volatile.Read(ref queueDepth),
            Interlocked.Read(ref coalescedSignalCount),
            Interlocked.Read(ref rejectedSignalCount),
            Interlocked.Read(ref processedSignalCount));
    }
    // Provides the non-blocking callback path and reports saturation to the producer.
    public bool TryWrite(ActivitySignal signal) {
        ArgumentNullException.ThrowIfNull(signal);
        ThrowIfCompleted();
        if(!TryReserveCoalescible(signal)) {
            Interlocked.Increment(ref coalescedSignalCount);
            return true;
        }
        if(channel.Writer.TryWrite(signal)) {
            Interlocked.Increment(ref queueDepth);
            return true;
        }
        ReleaseCoalescible(signal);
        Interlocked.Increment(ref rejectedSignalCount);
        return false;
    }
    // Preserves non-droppable signals with cancellable backpressure and coalesces pending duplicates.
    public async ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signal);
        ThrowIfCompleted();
        if(!TryReserveCoalescible(signal)) {
            Interlocked.Increment(ref coalescedSignalCount);
            return;
        }
        try {
            await channel.Writer.WriteAsync(signal, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref queueDepth);
        }
        catch {
            ReleaseCoalescible(signal);
            throw;
        }
    }
    // Runs exactly one consumer until completion or cancellation and writes emitted events in signal order.
    public async Task RunAsync(CancellationToken cancellationToken) {
        if(Interlocked.Exchange(ref runStarted, 1) != 0) {
            throw new InvalidOperationException("The activity coordinator consumer can run only once.");
        }
        await foreach(ActivitySignal signal in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
            Interlocked.Decrement(ref queueDepth);
            try {
                IReadOnlyList<ActivityEvent> events = await signalProcessor.ProcessAsync(signal, runtimeState, cancellationToken).ConfigureAwait(false);
                foreach(ActivityEvent activityEvent in events) {
                    await eventWriter.AppendAsync(activityEvent, cancellationToken).ConfigureAwait(false);
                }
                Interlocked.Increment(ref processedSignalCount);
            }
            catch {
                if(runtimeState.TrackerLifecycle.State is not TrackerLifecycleState.Stopped and not TrackerLifecycleState.Faulted) {
                    runtimeState.TrackerLifecycle.Fault();
                }
                throw;
            }
            finally {
                ReleaseCoalescible(signal);
            }
        }
    }
    // Completes producer admission while allowing the consumer to drain accepted signals.
    public void Complete() {
        if(Interlocked.Exchange(ref completionRequested, 1) == 0) {
            channel.Writer.TryComplete();
        }
    }
    public async ValueTask DisposeAsync() {
        Complete();
        await eventWriter.DisposeAsync().ConfigureAwait(false);
    }
    bool TryReserveCoalescible(ActivitySignal signal) {
        if(signal.Delivery != SignalDelivery.Coalescible) {
            return true;
        }
        lock(pendingSignalsLock) {
            if(pendingCoalescibleSignals.Exists(signal.CanCoalesceWith)) {
                return false;
            }
            pendingCoalescibleSignals.Add(signal);
            return true;
        }
    }
    void ReleaseCoalescible(ActivitySignal signal) {
        if(signal.Delivery != SignalDelivery.Coalescible) {
            return;
        }
        lock(pendingSignalsLock) {
            pendingCoalescibleSignals.RemoveAll(pendingSignal => ReferenceEquals(pendingSignal, signal));
        }
    }
    void ThrowIfCompleted() {
        if(Volatile.Read(ref completionRequested) != 0) {
            throw new ChannelClosedException();
        }
    }
}
