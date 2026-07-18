namespace FocusLedger.Core.Classification;

// Converts one transient application context into a deterministic privacy-safe classification.
public interface IActivityClassifier {
    // Allows adapters and rule evaluation to honor cancellation without blocking the coordinator.
    ValueTask<ClassificationResult> ClassifyAsync(
        ClassificationRequest request,
        CancellationToken cancellationToken);
}
