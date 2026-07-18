namespace FocusLedger.Core.Classification;

// Returns only privacy-safe classification values that may cross into persistence and reporting.
public sealed record ClassificationResult(
    string Category,
    string? SafeContext,
    string RuleId,
    double Confidence);
