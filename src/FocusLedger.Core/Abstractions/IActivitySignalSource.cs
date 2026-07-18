namespace FocusLedger.Core.Abstractions;

// Runs one platform collector and publishes only platform-neutral signals into Core.
public interface IActivitySignalSource : IAsyncDisposable {
    // Keeps the collector active until cancellation while preserving deterministic shutdown.
    Task RunAsync(IActivitySignalSink signalSink, CancellationToken cancellationToken);
}
