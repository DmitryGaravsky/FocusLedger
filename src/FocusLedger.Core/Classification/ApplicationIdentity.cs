namespace FocusLedger.Core.Classification;

// Carries the normalized application identity that is safe to use in rules and persisted events.
public sealed record ApplicationIdentity(
    string Id,
    string ProcessName,
    string? Family);
