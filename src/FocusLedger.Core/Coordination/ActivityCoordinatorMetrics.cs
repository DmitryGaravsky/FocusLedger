namespace FocusLedger.Core.Coordination;

// Exposes bounded, privacy-safe pipeline counters for diagnostics and operational tests.
public sealed record ActivityCoordinatorMetrics(
    int Capacity,
    int QueueDepth,
    long CoalescedSignalCount,
    long RejectedSignalCount,
    long ProcessedSignalCount);
