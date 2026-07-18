# System Architecture

**Status:** Approved baseline

## 1. Architectural style

FocusLedger is a single-user, single-process, event-driven Windows tray agent. It uses an internal event pipeline and append-only persistence. Platform-specific collectors produce signals; a single state-machine consumer turns signals into privacy-normalized domain events; one writer appends those events to the active JSONL file.

Optional subsystems such as title adapters, Core Audio inspection, UI Automation, and report generation are failure-isolated from foreground tracking.

## 2. Planned solution structure

```text
src/
  FocusLedger.App/
    Program.cs
    TrackerApplicationContext.cs
    CompositionRoot.cs
    CommandLine/
  FocusLedger.Core/
    Abstractions/
    Classification/
    Configuration/
    Events/
    Privacy/
    State/
    Time/
  FocusLedger.Windows/
    Audio/
    Foreground/
    Idle/
    Interop/
    Power/
    Processes/
    Session/
    SingleInstance/
    Startup/
    Tray/
  FocusLedger.Reporting/
    Aggregation/
    Html/
    Metrics/
    Reading/
tests/
  FocusLedger.Core.Tests/
  FocusLedger.Windows.Tests/
  FocusLedger.Reporting.Tests/
```

## 3. Runtime topology

```text
Windows message-loop thread
  |- NotifyIcon and tray menu
  |- hidden native/message-only window
  |- WinEvent callback handoff
  |- WTS and power message handoff

Collector tasks
  |- foreground reconciliation timer
  |- idle-state sampler
  |- meeting detector
  |- retention maintenance
  |- configuration watcher

Bounded Channel<ActivitySignal>
  -> single ActivityCoordinator consumer
       -> state transitions
       -> privacy normalization
       -> classification
       -> Channel<ActivityEvent>
            -> single JsonlEventWriter

On-demand report task
  -> JSONL readers
  -> interval reconstruction
  -> metrics
  -> standalone HTML writer
```

## 4. Component responsibilities

### 4.1 `FocusLedger.App`

Responsibilities:

- parse process command line;
- acquire the per-user singleton lock;
- forward commands when another instance is active;
- create and dispose the dependency graph;
- start the Windows Forms message loop;
- coordinate graceful shutdown;
- map fatal state to tray indication and process exit code.

It contains no classification or report business logic.

### 4.2 `FocusLedger.Core`

Responsibilities:

- define `ActivitySignal` and `ActivityEvent` contracts;
- implement tracker/presence/foreground/meeting state machines;
- normalize and classify application context;
- enforce privacy policy;
- define configuration models and validation contracts;
- expose platform-neutral clocks and identifiers;
- provide deterministic interval logic.

It has no Windows-specific references.

### 4.3 `FocusLedger.Windows`

Responsibilities:

- Win32 P/Invoke declarations and safe wrappers;
- foreground event hooks;
- process metadata inspection;
- idle detection;
- WTS session notifications;
- suspend/resume notifications;
- Windows Forms tray UI;
- Core Audio session enumeration;
- per-user registry autostart;
- named mutex and named pipe;
- safe shell-opening commands initiated by the user.

Windows API results are translated into platform-neutral signals before entering the core coordinator.

### 4.4 `FocusLedger.Reporting`

Responsibilities:

- stream JSONL without loading an entire year into memory;
- ignore a malformed trailing line while surfacing data-quality status;
- read supported schema versions;
- reconstruct mutually exclusive presence/activity intervals;
- compute report metrics;
- HTML-encode dynamic data;
- write a single offline HTML file.

It does not mutate activity files.

## 5. Concurrency model

### 5.1 Message-loop thread

The message-loop thread MUST remain responsive. Callbacks enqueue small immutable signals and return. It MUST NOT perform:

- file writes;
- report generation;
- Core Audio enumeration;
- cross-process UI Automation;
- process-path inspection that may block;
- configuration parsing.

### 5.2 Signal channel

A bounded `Channel<ActivitySignal>` serializes all externally observed changes. The channel policy must distinguish:

- non-droppable transitions: pause, resume, lock, unlock, suspend, resume, shutdown, meeting start/end;
- coalescible updates: repeated title changes, foreground reconciliation duplicates, heartbeat requests.

Queue saturation must create a safe diagnostic and coalesce low-priority updates rather than block the Windows callback thread indefinitely.

### 5.3 Coordinator

One consumer owns mutable runtime state. No collector may directly edit the state model or append persisted events. The coordinator:

1. validates signal ordering;
2. updates state machines;
3. emits zero or more events;
4. applies privacy policy;
5. assigns sequence numbers;
6. forwards events to the writer.

### 5.4 Event writer

One writer owns the current `FileStream`. It supports concurrent readers through `FileShare.Read`. It flushes on a timer and immediately for critical lifecycle transitions.

## 6. Time model

Two notions of time are required:

- wall-clock time for event timestamps, local-day rollover, report labels, and UTC offset;
- monotonic time for duration and debounce calculations.

`TimeProvider` supplies wall-clock time. A monotonic-clock abstraction based on `Stopwatch.GetTimestamp()` supplies elapsed time. State transitions store both where required.

The system must handle:

- daylight-saving transitions;
- manual clock changes;
- NTP corrections;
- suspend gaps;
- timezone changes;
- midnight rollover while a state remains active.

## 7. Failure isolation

### 7.1 Core collection priority

Foreground, presence, pause, session, and power tracking are critical. The following are optional and must fail independently:

- detailed title parser;
- UI Automation adapter;
- meeting provider adapter;
- Core Audio evidence;
- configuration hot reload;
- report generation;
- retention cleanup.

### 7.2 Circuit breakers

Cross-process or COM integrations SHOULD use a per-adapter circuit breaker. After repeated failures, the adapter is disabled for a cooldown period and tracking falls back to simpler signals.

### 7.3 Error states

A recoverable subsystem failure changes the tray status to warning/error but does not exit. A fatal writer failure stops collection attribution because data cannot be persisted reliably, records what it safely can, and presents a persistent error state.

## 8. Process and identity model

Application identity is normalized from process metadata. Raw process paths are not persisted. The normalizer prefers:

1. configured application mapping;
2. known product/application family;
3. package identity where available;
4. normalized executable file name;
5. `unknown-application`.

PID and HWND are ephemeral runtime identifiers and are not stable report keys.

## 9. Extensibility

The first stable release uses built-in adapters and configuration-driven rules. It does not load arbitrary third-party assemblies. Extensibility points are internal interfaces so a later release can add safe, signed, or out-of-process adapters without changing the core event model.

## 10. Security boundaries

- Named-pipe commands are restricted to the same Windows user.
- The application does not elevate itself.
- It does not bypass User Interface Privilege Isolation.
- Access-denied results are expected and must degrade gracefully.
- Configuration is untrusted local input and must be validated before activation.
- HTML report content is untrusted data and must be encoded.
