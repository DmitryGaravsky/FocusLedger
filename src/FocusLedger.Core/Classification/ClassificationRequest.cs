using System.Text.Json.Serialization;

namespace FocusLedger.Core.Classification;

// Carries transient classification input while preventing a raw title from becoming serialized output.
public sealed class ClassificationRequest {
    public ClassificationRequest(ApplicationIdentity application, string? rawWindowTitle) {
        ArgumentNullException.ThrowIfNull(application);
        // Retain the sensitive title only for the lifetime of the current classification operation.
        Application = application;
        RawWindowTitle = rawWindowTitle;
    }
    public ApplicationIdentity Application { get; }
    [JsonIgnore]
    public string? RawWindowTitle { get; }
    // Keep diagnostic string rendering independent from every transient external value.
    public override string ToString() {
        return "ClassificationRequest { sensitive values redacted }";
    }
}
