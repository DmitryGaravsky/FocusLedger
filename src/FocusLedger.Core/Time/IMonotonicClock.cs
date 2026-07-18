namespace FocusLedger.Core.Time;

// Provides elapsed-time measurements that remain stable across wall-clock and timezone changes.
public interface IMonotonicClock {
    // Captures an opaque monotonic timestamp at an observation boundary.
    long GetTimestamp();
    // Converts two timestamps from the same clock into a non-wall-clock duration.
    TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp);
}
