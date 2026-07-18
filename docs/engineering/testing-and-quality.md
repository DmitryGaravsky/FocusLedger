# Testing and Quality Strategy

**Status:** Approved baseline

## 1. Quality goals

FocusLedger is a continuously running application that handles sensitive transient data. Correctness, privacy, graceful degradation, and resource stability are release-critical.

## 2. Test layers

All automated test projects use NUnit. NuGet package versions are managed centrally through `Directory.Packages.props`.

### 2.1 Unit tests

`FocusLedger.Core.Tests` covers:

- tracker lifecycle transitions;
- presence precedence;
- idle-threshold boundary calculation;
- foreground deduplication;
- meeting hysteresis;
- midnight rollover;
- monotonic versus wall-clock behavior;
- classification rule ordering;
- regex timeout behavior;
- safe-context validation;
- privacy redaction;
- configuration validation;
- sequence allocation.

### 2.2 Reporting tests

`FocusLedger.Reporting.Tests` covers:

- event parsing;
- unknown property tolerance;
- malformed final line;
- malformed middle line;
- schema-version rejection;
- de-duplication;
- interval reconstruction;
- precedence rules;
- focus-session calculations;
- meeting overlays;
- lost-time calculations;
- DST transitions;
- HTML encoding;
- deterministic golden reports.

### 2.3 Windows integration tests

`FocusLedger.Windows.Tests` covers where CI permits:

- named mutex and pipe ACLs;
- secondary-instance command routing;
- registry autostart creation/removal in isolated test keys where possible;
- hidden-window message routing;
- foreground wrapper behavior;
- safe handling of invalid/stale HWND and PID;
- process access denied;
- Core Audio enumeration abstraction;
- deterministic disposal of hooks and COM objects.

Tests that require an interactive desktop must be clearly tagged and runnable on a dedicated Windows test environment.

## 3. Scenario tests

Required end-to-end scenarios:

1. Start while an application is active.
2. Switch among three applications with title changes.
3. Remain inactive past the 5-minute threshold and return.
4. Lock and unlock while an application remains open.
5. Disconnect and reconnect an RDP session.
6. Suspend and resume.
7. Cross local midnight while Running, Idle, Paused, and in a meeting.
8. Change the system clock backward and forward.
9. Change timezone.
10. Crash with a partially written final line and restart.
11. Save invalid configuration, then repair it.
12. Start and end a meeting while switching foreground applications.
13. Lose Core Audio access while meeting detection continues with reduced confidence.
14. Move the portable executable after enabling autostart.
15. Run a secondary CLI command while the primary instance is active.

## 4. Privacy tests

Use canary values:

```text
alice.sensitive@example.test
C:\Users\Alice\SecretCustomer\Q4-plan.docx
https://example.test/account?token=secret-value
Secret Customer Meeting with John Smith
192.0.2.44
```

Tests must assert that canaries do not appear in:

- JSONL;
- `state.json`;
- diagnostics;
- HTML reports;
- configuration error output;
- exception snapshots;
- test attachments.

Also test that classification can still produce safe constants such as `pull-request` or `web-meeting` from those inputs.

## 5. Fault injection

Inject failures for:

- file open/write/flush;
- disk full;
- directory unavailable;
- unauthorized registry access;
- access denied reading process metadata;
- hook registration failure;
- WTS registration failure;
- COM initialization/session enumeration failure;
- configuration watcher overflow;
- malformed JSONL;
- cancellation during report generation;
- queue saturation;
- report output replacement failure.

Each fault has an expected safe behavior and diagnostic code.

## 6. Performance and endurance

### 6.1 Microbenchmarks

Benchmark:

- classification lookup;
- regex rules with timeouts;
- event serialization;
- JSONL parsing;
- interval reduction;
- report metric aggregation.

### 6.2 Long-running tests

Run at least a 24-hour synthetic endurance scenario before 1.0 release. Observe:

- CPU average;
- working set trend;
- handle count;
- thread count;
- COM object disposal;
- event queue depth;
- file size;
- report generation over accumulated data.

No unbounded growth is acceptable.

## 7. Compatibility tests

Maintain fixtures for every supported event schema and configuration schema. Current readers must process all supported historical fixtures. A schema-breaking change requires migration tests and ADR documentation.

## 8. Static quality gates

Recommended gates:

- nullable enabled;
- warnings as errors;
- analyzers enabled;
- NUnit analyzer diagnostics enabled for every test project;
- no project-level NuGet package versions outside `Directory.Packages.props`;
- `dotnet format FocusLedger.slnx` runs after the final code edit;
- `dotnet format FocusLedger.slnx --verify-no-changes` passes before handoff;
- changed text files use CRLF line endings;
- dependency vulnerability scan;
- license review;
- secret scan;
- no unexpected network API usage;
- no prohibited capture APIs;
- generated P/Invoke signatures reviewed for trimming/single-file behavior.

## 9. Manual release checklist

Before release:

- run on Windows 10 22H2 and current Windows 11;
- verify first start, tray, pause persistence, reports, autostart, exit;
- inspect data and diagnostics for privacy leakage;
- test elevated foreground application degradation;
- test sleep/resume and lock/unlock;
- verify no outbound connections using an independent network monitor;
- verify the published artifact runs without installed .NET;
- verify only the documented temporary extraction behavior occurs;
- scan the release binary and SBOM.
