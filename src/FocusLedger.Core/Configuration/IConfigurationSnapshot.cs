namespace FocusLedger.Core.Configuration;

// Marks an immutable validated configuration generation consumed by runtime components.
public interface IConfigurationSnapshot {
    int SchemaVersion { get; }
}
