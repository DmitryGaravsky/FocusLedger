using FocusLedger.Core.Events;

namespace FocusLedger.Core.Abstractions;

// Owns ordered append and flush operations for the single persisted event stream.
public interface IActivityEventWriter : IAsyncDisposable {
    // Appends one fully normalized event without exposing storage implementation details.
    ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken);
    // Makes buffered events durable at lifecycle and other critical transition boundaries.
    ValueTask FlushAsync(CancellationToken cancellationToken);
}
