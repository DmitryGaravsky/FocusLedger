using FocusLedger.Core.Events;
using FocusLedger.Core.Signals;

namespace FocusLedger.Core.Coordination;

// Reduces one serialized signal into privacy-normalized events while updating coordinator-owned state.
public interface IActivitySignalProcessor {
    // Returns only events that passed privacy and semantic-change checks.
    ValueTask<IReadOnlyList<ActivityEvent>> ProcessAsync(
        ActivitySignal signal,
        ActivityRuntimeState runtimeState,
        CancellationToken cancellationToken);
}
