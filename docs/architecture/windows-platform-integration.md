# Windows Platform Integration

**Status:** Approved baseline

## 1. Process model

FocusLedger is a `WinExe` that runs a Windows Forms `ApplicationContext` without a main form. Windows Forms is infrastructure for:

- the process message loop;
- `NotifyIcon`;
- `ContextMenuStrip`;
- a hidden native/message-only window where required.

No visible window is created during normal operation.

The message-loop host creates one `NativeWindow` whose parent is `HWND_MESSAGE`. It routes selected messages through small synchronous handlers and owns no forms. Shutdown requests post an internal `WM_APP` message, allowing cancellation or another process-lifetime component to exit the loop without blocking or disposing UI resources from a foreign thread.

## 2. Foreground tracking

### 2.1 Primary signal

Register `SetWinEventHook` for:

- `EVENT_SYSTEM_FOREGROUND`;
- selected `EVENT_OBJECT_NAMECHANGE` notifications.

Use out-of-context hooks. Callback code must copy minimal identifiers into an immutable signal and return quickly.

The callback persists no process or caption data. It copies only the opaque top-level window handle, observation kind, UTC observation time, and monotonic timestamp. Foreground switches are non-droppable signals. Filtered title changes are coalescible candidates; debounce, caption inspection, privacy normalization, and semantic comparison happen after callback handoff.

Hook registration failure releases any hook already registered and reports only the numeric Win32 error. Callback exceptions are contained at the native boundary and represented by aggregate privacy-safe counters.

### 2.2 Title-change filtering

`EVENT_OBJECT_NAMECHANGE` is noisy. Accept a title-change candidate only when:

- it refers to the current foreground top-level window;
- it is not an irrelevant child-object change;
- it survives a short debounce;
- a fresh caption produces a different normalized context or classification.

The WinEvent collector performs the first two filters. The serialized downstream processor performs debounce and the final semantic checks once title inspection and classification are available.

### 2.3 Reconciliation

Every second by default, call `GetForegroundWindow` and compare it with coordinator state. Reconciliation repairs missed hooks and startup races. It must not generate duplicate persisted events.

The hook collector and reconciliation sampler share a small atomic source-state containing only the latest opaque HWND. A producer reserves a changed HWND before calling the non-blocking sink. Successful reservations suppress duplicates from either source. Rejected writes roll the reservation back when it is still current, allowing the next sampler tick to repair the missed observation. A zero HWND is treated as temporarily unavailable and produces no foreground-change signal.

### 2.4 Process metadata

Use `GetWindowThreadProcessId` to identify the owning process. Read only metadata required for application identity. Expected failures include:

- process exits during inspection;
- access denied;
- protected process;
- elevated process at a higher integrity level;
- stale HWND/PID reuse.

All such failures degrade to limited identity and safe error codes.

Process inspection runs asynchronously outside the message-loop callback. It queries the PID and uses limited-information process access to read the executable path. The path, PID, product name, and file description remain transient and are excluded from JSON serialization and diagnostic string rendering. Only a normalized executable file name may later become part of persisted application identity.

Inspection outcomes are structured as success, limited identity, unavailable window, exited process, access denied, or other platform failure. Failure results contain only a numeric Win32 error and any safely obtained normalized process file name; exception messages and raw paths are not propagated.

### 2.5 Caption access

Use `GetWindowTextLength`/`GetWindowText` for top-level captions. Do not send arbitrary messages into another process to bypass normal restrictions. Do not persist the returned raw caption.

## 3. Idle detection

Use `GetLastInputInfo` on a periodic sampler, default once per second. The sampler observes only the tick count of the last input for the current session.

Requirements:

- handle tick-count arithmetic safely;
- calculate the threshold crossing rather than using only detection time;
- never install keyboard or mouse hooks;
- never identify which key/button caused activity;
- pause sampling during system suspend;
- reconcile immediately after resume/unlock/reconnect.

The detector compares the low 32 bits of `GetTickCount64` with the 32-bit last-input tick using unchecked unsigned arithmetic, preserving correct behavior across tick wrap. When a sample first exceeds the idle threshold, the signal timestamp is backdated to `last input + threshold`; it is not rounded to the later sampling time. The monotonic timestamp records the detection boundary for ordering, while the UTC timestamp records the logical idle transition.

Only Active and Idle semantic transitions are published. Repeated samples are suppressed. A failed platform sample emits no transition and is retried. A rejected non-blocking sink write does not commit detector state, allowing the next sample to retry. No key, button, coordinate, target application, or other input content is observed.

## 4. Session notifications

Register the hidden window through `WTSRegisterSessionNotification`. Handle at least:

- `WTS_SESSION_LOCK`;
- `WTS_SESSION_UNLOCK`;
- `WTS_CONSOLE_CONNECT`;
- `WTS_CONSOLE_DISCONNECT`;
- `WTS_REMOTE_CONNECT`;
- `WTS_REMOTE_DISCONNECT`;
- `WTS_SESSION_LOGON`;
- `WTS_SESSION_LOGOFF`.

Only events for the current process user's session are attributed. Remote Desktop is supported for the current user; other sessions are ignored.

Registration uses `NOTIFY_FOR_THIS_SESSION`, and every delivered message is additionally filtered against the session ID resolved for the current FocusLedger process. Persistable signals contain only the transition kind and timestamps; they never contain a Windows user name, remote endpoint, client name, or session identifier.

The WTS callback path performs only constant-time mapping and a non-blocking signal write. Registration, session-resolution, and unregistration failures are counted as platform failures and degrade without stopping foreground or idle collection. Unknown WTS notification codes are ignored. The collector unregisters the same hidden window during deterministic disposal.

## 5. Power notifications

Handle suspend/resume through `WM_POWERBROADCAST` and/or `SystemEvents.PowerModeChanged`, with one abstraction producing normalized signals.

On suspend:

- enqueue a critical `SystemSuspending` signal;
- flush the event writer;
- stop polling that assumes an interactive desktop.

On resume:

- enqueue `SystemResumed`;
- re-register or verify hooks if needed;
- reconcile session, idle, foreground, and audio state.

The hidden HWND is explicitly registered through `RegisterSuspendResumeNotification`; relying on an unregistered broadcast is insufficient for a message-only window. Both automatic and user-present resume codes map to one semantic `Resumed` transition, and repeated suspend or resume notifications are suppressed after a successful pipeline write.

Suspend and resume signals are non-droppable. Their serialized consumer is responsible for closing attribution, immediately flushing the future event writer on suspend, and initiating collector reconciliation on resume. The window callback only performs constant-time mapping and a non-blocking signal write. Failed registration, failed unregistration, and rejected writes are measured without stopping other collectors; a rejected transition remains eligible for retry.

## 6. System tray

### 6.1 Icon states

The icon and tooltip must represent:

- Collecting / Active;
- Collecting / Idle;
- Paused;
- Meeting detected;
- Error.

Meeting may be represented by an overlay or tooltip suffix so it does not hide a persistence error.

The tray presentation resolves simultaneous conditions in the order Error, Meeting, Paused, Idle, and Active. This keeps a persistence failure visible while allowing meeting detection to act as the first implementation's placeholder overlay. The initial implementation uses Windows system icons and privacy-safe fixed tooltips; it never places application or window context in shell-visible text.

### 6.2 Menu

Initial menu:

```text
FocusLedger — Collecting

Pause tracking
Resume tracking

Generate report
  Today
  Yesterday
  Last 7 days
  Current month

Open latest report
Open reports folder
Open data folder
Open configuration
Reload configuration

Start meeting manually
End meeting manually

Start with Windows

Exit
```

Commands that do not apply to the current state are disabled rather than removed.

The fixed menu is capability-driven. The composition root enables an item only after its subsystem supplies a handler; future commands remain visible but disabled. State rules independently make Pause/Resume and Start/End meeting mutually exclusive. The autostart item reflects persisted state with a check mark, and Exit is available from the initial application composition.

### 6.3 Notifications

Balloon/toast-like notifications are limited to:

- tracking paused/resumed;
- invalid configuration;
- writer failure;
- invalid autostart path;
- report generation failure.

Normal foreground changes do not create notifications.

## 7. Single instance and local command transport

Use a per-user named mutex and named pipe. Names include a stable product ID plus the current user's SID-derived safe identifier.

Requirements:

- only the same Windows user can connect;
- a secondary process forwards one command and exits;
- command payloads are bounded and schema-validated;
- malformed commands do not crash the primary process;
- no TCP or HTTP listener is used.

## 8. Autostart

Use:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

The value points to the current executable with `--autostart`. Enabling autostart requires an explicit tray or CLI command.

At startup, verify whether the configured path still matches the running executable. If the portable executable was moved, mark autostart as invalid and offer a user command to rewrite it. Do not silently modify registry state unless the user enabled the repair action.

## 9. Core Audio integration

Use Core Audio session enumeration as meeting evidence. Map audio sessions to process IDs where available. Audio integration must not:

- capture or record audio;
- inspect sample content;
- treat audio render/capture alone as definitive meeting proof;
- block the message-loop thread;
- cause foreground tracking to fail.

COM resources must be released deterministically.

## 10. UI Automation

UI Automation is not required for the 1.0 browser implementation. If used for a specific application adapter, it must:

- run off the message-loop and coordinator threads;
- have strict timeouts/cancellation boundaries where technically possible;
- redact all returned values;
- use a circuit breaker;
- fall back to top-level caption analysis;
- remain disabled unless the adapter has explicit tests and privacy review.

## 11. Shutdown

Support:

- tray Exit;
- command-line `--quit`;
- logoff/shutdown notifications;
- cancellation from process lifetime.

Graceful shutdown sequence:

1. stop accepting non-critical collector updates;
2. enqueue stopping transition;
3. stop timers and hooks;
4. finish queued critical transitions;
5. flush and close the writer;
6. persist clean-shutdown state;
7. dispose tray resources and exit the message loop.

Forced process termination cannot be made lossless; periodic flush and heartbeat bound the expected loss.
