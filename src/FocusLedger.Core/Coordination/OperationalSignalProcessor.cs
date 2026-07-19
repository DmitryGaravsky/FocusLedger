using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.Signals;

namespace FocusLedger.Core.Coordination;

// Maps operational signals to sequence-reserved events without synthesizing activity for process gaps.
public sealed class OperationalSignalProcessor : IActivitySignalProcessor {
    readonly OperationalEventSession eventSession;
    public OperationalSignalProcessor(OperationalEventSession eventSession) {
        ArgumentNullException.ThrowIfNull(eventSession);
        this.eventSession = eventSession;
    }
    public async ValueTask<IReadOnlyList<ActivityEvent>> ProcessAsync(
        ActivitySignal signal,
        ActivityRuntimeState runtimeState,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(runtimeState);
        if(signal is not OperationalActivitySignal operationalSignal)
            throw new ArgumentException("The operational processor accepts only operational signals.", nameof(signal));
        OperationalActivityEvent activityEvent = await eventSession.CreateEventAsync(operationalSignal, cancellationToken).ConfigureAwait(false);
        return new ActivityEvent[] { activityEvent };
    }
}
