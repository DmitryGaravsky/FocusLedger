using FocusLedger.Core.Events;

namespace FocusLedger.Reporting.Reading;

public enum JsonlDataQualityIssueKind {
    MalformedEvent,
    IncompleteTrailingLine,
    InvalidEnvelope,
    UnsupportedSchema,
    InvalidPayload,
    LineTooLong
}

// Represents one streamed reader outcome without retaining the raw JSON line or unsafe file path.
public abstract record JsonlReadItem(string FileName, int LineNumber);

// Carries a validated schema-1 envelope and an optional typed payload for supported event contracts.
public sealed record JsonlEventReadItem(
    string FileName,
    int LineNumber,
    EventEnvelope Envelope,
    ActivityEvent? ActivityEvent) : JsonlReadItem(FileName, LineNumber);

// Carries only an enumerated data-quality category and safe location metadata.
public sealed record JsonlIssueReadItem(
    string FileName,
    int LineNumber,
    JsonlDataQualityIssueKind Kind) : JsonlReadItem(FileName, LineNumber);
