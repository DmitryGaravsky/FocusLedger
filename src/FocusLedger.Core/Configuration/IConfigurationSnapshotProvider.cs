namespace FocusLedger.Core.Configuration;

// Exposes the last valid immutable snapshot without coupling consumers to reload mechanics.
public interface IConfigurationSnapshotProvider<out TSnapshot>
    where TSnapshot : IConfigurationSnapshot {
    TSnapshot Current { get; }
}
