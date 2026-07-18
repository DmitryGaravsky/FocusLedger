# Instructions for Coding Agents

## 1. Authority and source order

Before making changes, read the relevant documents in this order:

1. `docs/product/requirements.md`
2. `docs/product/privacy-and-data-policy.md`
3. `docs/architecture/overview.md`
4. subsystem-specific documents under `docs/`
5. accepted decisions under `docs/adr/`
6. `docs/roadmap.md`

If documents conflict, privacy requirements take precedence, followed by accepted ADRs, product requirements, architecture documents, and roadmap descriptions. Do not silently resolve a conflict in code. Add or amend an ADR and update the affected documents.

## 2. Required design principles

Every implementation must preserve these invariants:

- The application performs no network communication.
- Raw window titles and raw URLs are never persisted in the default Balanced Privacy mode.
- Raw titles may exist only transiently in memory while parsing, redacting, normalizing, and classifying the current context.
- Diagnostic logs must not contain raw titles, URLs, document names, user-profile paths, machine names, Windows usernames, email addresses, or configuration values that may contain personal data.
- The main activity stream is append-only JSONL.
- Only one component may append to the current JSONL file.
- State transitions are serialized through a single consumer.
- Foreground tracking must continue when optional classifiers, UI Automation adapters, audio inspection, or report generation fail.
- No feature may require administrator rights.
- No feature may inspect another Windows user's session.
- The application must remain useful when an optional Windows API is unavailable or access is denied.

## 3. Repository structure

The planned solution structure is:

```text
src/
  FocusLedger.App/
  FocusLedger.Core/
  FocusLedger.Windows/
  FocusLedger.Reporting/
tests/
  FocusLedger.Core.Tests/
  FocusLedger.Windows.Tests/
  FocusLedger.Reporting.Tests/
docs/
```

Project responsibilities are strict:

- `FocusLedger.Core` contains domain types, state machines, privacy normalization, classification contracts, configuration models, and platform-neutral logic.
- `FocusLedger.Windows` contains Win32, Windows Forms infrastructure, session/power integration, process inspection, Core Audio integration, autostart, and named-pipe transport.
- `FocusLedger.Reporting` reads persisted events, reconstructs intervals, computes metrics, and creates standalone HTML.
- `FocusLedger.App` is the composition root and owns process lifetime.

`FocusLedger.Core` must not reference Windows Forms, UI Automation, registry APIs, Core Audio COM interfaces, or P/Invoke declarations.

## 4. Implementation workflow

For every roadmap feature:

1. Confirm the feature ID and dependencies in `docs/roadmap.md`.
2. Write or update acceptance tests before completing the implementation.
3. Add privacy-focused tests when the feature handles titles, process metadata, configuration, diagnostics, or reports.
4. Add failure-path tests for unavailable APIs, access denied, malformed data, clock changes, cancellation, and partial files where applicable.
5. Update the roadmap feature status only after all acceptance criteria are met.
6. Update documentation in the same pull request as behavioral changes.
7. After the final code edit, run `dotnet format FocusLedger.slnx` before the final build and test pass.
8. Verify that changed text files use CRLF line endings before handing off the work.

## 5. Coding requirements

- Use nullable reference types and treat compiler warnings as errors.
- Use NUnit as the test framework for all test projects.
- Manage all NuGet package versions centrally through `Directory.Packages.props`; project files must not declare package versions.
- Use the most restrictive effective visibility. Omit the `private` modifier wherever `private` is the C# default, and widen visibility only when required by a caller, framework, or documented extension boundary.
- Do not use expression-bodied implementations for properties, property accessors, or methods.
- Keep a short property accessor on one line when its implementation is a single simple statement, for example `get { return value; }`, instead of expanding the accessor body across multiple lines.
- Do not use block comments (`/* ... */`) or documentation comments (`///`). Use ordinary line comments only when a comment is necessary.
- Do not leave blank lines between method declarations or inside method bodies. When a method needs logical sections, separate them with an ordinary line comment that explains the purpose of the following section.
- Prefer immutable records for persisted events, configuration snapshots, and state-machine inputs.
- Use `System.Text.Json` source generation for event and configuration serialization when practical.
- Do not use reflection-based plugin loading in the first stable release.
- Use dependency injection only at process composition boundaries; do not turn simple domain objects into service-locator clients.
- Use `TimeProvider` for wall-clock dependencies and an injectable monotonic-clock abstraction for duration-sensitive logic.
- Use bounded or explicitly monitored channels. A queue that can grow without limit is prohibited.
- Every background operation must support cancellation and deterministic shutdown.
- Do not block the Windows message-loop thread on file I/O, UI Automation, report generation, process inspection, or Core Audio calls.
- Never call external processes or open URLs without a direct user command.
- Escape all report content for its HTML context.

## 6. Event compatibility

Persisted event names and JSON property names are public data-contract surface. Changes require one of:

- a backward-compatible additive change;
- a schema-version increment plus a documented reader migration;
- an ADR explaining why compatibility is intentionally broken before version 1.0.

Readers must ignore unknown properties and preserve the ability to read earlier supported schema versions.

## 7. Definition of Done

A feature is complete only when:

- implementation and tests pass on the supported Windows CI matrix;
- privacy invariants are verified;
- failure behavior is documented and tested;
- JSONL and configuration compatibility are considered;
- performance impact is measured when the feature runs continuously;
- user-facing behavior and configuration are documented;
- the relevant roadmap item is updated;
- `dotnet format FocusLedger.slnx --verify-no-changes` passes;
- changed text files use CRLF line endings;
- no diagnostics or test snapshots contain raw personal data.

## 8. Prohibited shortcuts

Do not:

- store raw titles temporarily on disk;
- use screenshots or OCR to classify activity;
- use low-level keyboard or mouse hooks;
- treat microphone use as definitive proof of a meeting;
- make unknown activity automatically unproductive;
- infer productivity from input frequency;
- include CDN-hosted scripts, fonts, styles, or images in reports;
- add telemetry, crash upload, update checks, analytics, or remote configuration;
- introduce a browser extension in the first stable release;
- enable autostart without an explicit user action.
