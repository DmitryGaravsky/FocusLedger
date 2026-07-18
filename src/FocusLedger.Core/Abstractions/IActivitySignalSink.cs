using FocusLedger.Core.Signals;

namespace FocusLedger.Core.Abstractions;

// Accepts platform-neutral signals at the boundary of the serialized coordinator pipeline.
public interface IActivitySignalSink {
    // Provides a non-blocking path for Windows callbacks and other latency-sensitive producers.
    bool TryWrite(ActivitySignal signal);
    // Provides a cancellable path for signals that must wait for bounded channel capacity.
    ValueTask WriteAsync(ActivitySignal signal, CancellationToken cancellationToken);
}
