# Privacy and Data Policy

**Status:** Normative
**Default mode:** Balanced Privacy

## 1. Privacy objective

FocusLedger must provide useful personal activity analysis while minimizing the collection and persistence of personal information. Privacy controls are architectural constraints, not optional UI preferences.

The default Balanced Privacy mode permits transient inspection of a top-level window caption so the application can classify activity. The raw caption is never persisted. Only a normalized, non-personal context label may be written to the event stream.

## 2. Data classes

### 2.1 Prohibited data

FocusLedger MUST NOT collect, persist, transmit, or derive:

- keystrokes or typed text;
- keyboard scan codes;
- mouse coordinates or clicked controls;
- clipboard contents;
- screenshots;
- OCR output;
- document contents;
- page contents;
- email contents;
- chat-message contents;
- audio samples;
- camera images;
- speech-to-text output;
- network traffic;
- browser history;
- geolocation;
- credentials, tokens, cookies, or session identifiers;
- file-system inventory;
- data from another Windows user's session.

### 2.2 Transient inspection data

The following data MAY exist briefly in memory while processing the current foreground context:

- top-level raw window caption;
- process ID;
- top-level window handle;
- executable path required to establish process identity;
- UI Automation values from an explicitly enabled future adapter;
- Core Audio session metadata.

Transient data MUST:

- stay within the process;
- never be written to the activity stream;
- never be written to diagnostic logs;
- never be included in exception messages intentionally;
- be transformed or discarded immediately after classification;
- not be retained in unbounded caches;
- not be copied into telemetry because telemetry does not exist.

### 2.3 Persisted activity data

The default event stream MAY contain:

- event type;
- timestamps and UTC offset;
- sequence and event identifiers;
- presence state;
- normalized application ID;
- normalized executable file name where needed;
- safe application family;
- normalized context label from an allowlist or deterministic parser;
- category ID;
- productivity disposition and weight;
- classification rule ID;
- classification confidence;
- meeting provider ID;
- meeting confidence and non-personal evidence codes;
- error category and numeric platform error code.

It MUST NOT contain:

- raw title;
- full URL;
- browser query string;
- file path;
- Windows username;
- machine name;
- email address;
- document name unless transformed into a configured safe label;
- meeting title;
- contact or participant name;
- free-form exception text from external applications.

## 3. Privacy modes

### 3.1 Strict

Persist only application identity, category, state, and timing. No normalized title context is stored.

### 3.2 Balanced

Default. Raw titles are processed in memory. Persisted context must come from:

- a built-in safe label;
- an explicitly configured replacement label;
- a parser that returns an enumerated context kind;
- a rule that intentionally replaces the matched text with a constant.

Examples of acceptable persisted labels:

```text
source-code
pull-request
technical-documentation
web-meeting
youtube
microsoft-learn
email-client
spreadsheet
```

Examples of unacceptable persisted values:

```text
Customer-X security review.docx
John Smith - Microsoft Teams
alice@example.com - Outlook
C:\Users\dmitry\Projects\SecretProject\solution.sln
https://example.com/account?id=12345
```

### 3.3 Detailed

A future opt-in mode may persist raw titles, but it is outside the 1.0 privacy baseline. It must not be implemented casually under the existing `Balanced` name. Adding it requires a dedicated ADR, prominent warnings, separate tests, and explicit configuration.

## 4. Normalization pipeline

Every title-dependent classification follows this order:

```text
Raw top-level caption
  -> application-specific parser
  -> structural suffix removal
  -> sensitive-pattern redaction
  -> configured replacement
  -> safe context allowlist check
  -> category classification
  -> persisted event
```

If no safe normalized context is produced, the event stores no context label. Classification may still use the transient title to select a category, provided the rule ID and category are safe to persist.

## 5. Sensitive-pattern handling

Before any title-derived value becomes persistable, the normalizer MUST reject or redact likely:

- email addresses;
- user-profile paths;
- absolute Windows paths;
- URLs;
- GUID-like identifiers when not an internal enumerated ID;
- long digit sequences;
- query parameters;
- UNC paths;
- IP addresses;
- text exceeding the configured safe-label length;
- strings not produced by a trusted parser or explicit replacement rule.

Redaction is a safety net, not a substitute for allowlist-based output.

## 6. Diagnostics policy

Diagnostic logs are for operational failures only. They MAY include:

- component name;
- operation name;
- exception type;
- stack trace from FocusLedger code;
- numeric HRESULT or Win32 error;
- normalized application ID;
- rule ID;
- elapsed duration;
- retry count;
- bounded queue depth.

They MUST NOT include raw external values. Exception handling must avoid logging `Exception.Message` when the message may embed a window title, file path, URL, COM value, or configuration content. Safe structured error codes are preferred.

## 7. Reports

Reports contain only information already permitted in persisted activity events or safe derived metrics. Report generation MUST HTML-encode every dynamic string. The report MUST NOT load remote resources, emit tracking pixels, or embed machine-specific file paths.

## 8. Local storage and retention

Default storage:

```text
%LocalAppData%\FocusLedger
```

Default retention:

- activity JSONL: 365 days;
- diagnostics: 14 days;
- reports: no automatic deletion in 1.0 unless a future configurable policy is implemented.

Retention maintenance runs during startup after a valid configuration snapshot is available and before the current daily writer is opened. A retention value of `N` preserves the current local calendar day and the preceding `N - 1` calendar days. Cleanup recognizes only canonical `activity-YYYY-MM-DD.jsonl` files in their matching `data\YYYY-MM` partition and canonical `diagnostic-YYYY-MM-DD.log` files directly under `logs`; unknown, misplaced, report, configuration, and state files are never deleted. Reparse-point roots and month directories are skipped so cleanup cannot follow a link outside the storage root. An inaccessible or undeletable file is counted as a safe maintenance failure and does not prevent other eligible files from being processed or stop activity tracking.

Files are not encrypted by default. The privacy model relies on data minimization and the Windows user-profile access boundary. Users can delete data by deleting files. A future encryption feature must not prevent local report generation or reliable recovery without an explicit key-management design.

## 9. Network prohibition

The application MUST NOT:

- send telemetry;
- check for updates;
- download classifications;
- resolve cloud identities;
- query web APIs;
- upload crash reports;
- load report assets from a CDN;
- expose a listening TCP/UDP endpoint.

Local named pipes used for same-user process control are allowed and are not network transport.

## 10. Privacy verification

The test suite MUST include canary values resembling personal data and assert that they never appear in:

- JSONL output;
- diagnostic logs;
- HTML reports;
- state files;
- configuration-validation errors;
- test result attachments;
- crash-recovery artifacts.

A release is blocked if any canary leaks.
