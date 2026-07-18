# Runtime State Machines

**Status:** Approved baseline

## 1. Purpose

FocusLedger represents runtime behavior through orthogonal state machines. This prevents one overloaded state enum from producing ambiguous transitions and makes event reconstruction deterministic.

## 2. Tracker lifecycle state

```text
Starting -> Running <-> Paused
    |          |          |
    +----------+----------+-> Stopping -> Stopped
               |               |
               +-------> Faulted <------+
```

States:

- `Starting`: initialization, recovery, configuration load, hook registration.
- `Running`: collection enabled.
- `Paused`: collection intentionally suspended by the user.
- `Stopping`: graceful shutdown in progress.
- `Stopped`: terminal state.
- `Faulted`: persistence or unrecoverable coordinator failure.

Rules:

- Startup completes in `Running` or restores the persisted manual-pause choice by entering `Paused`.
- Shutdown may begin during `Starting`, `Running`, or `Paused`; this permits deterministic cancellation of incomplete initialization.
- Pause state persists across restart.
- `Paused` does not imply workstation idle or locked; presence continues to be observed for operational correctness but is not attributed as tracked application time.
- A fatal writer or coordinator failure during any non-terminal lifecycle phase transitions to terminal `Faulted`.
- Repeating the command that established `Running`, `Paused`, `Stopping`, or `Faulted` is idempotent. Other invalid transitions are rejected without changing state.
- `Stopped` and `Faulted` are terminal states. Resource cleanup after a fault does not change the recorded lifecycle state.

## 3. Presence state

```text
Unknown
  -> Active
  -> Idle
  -> SessionLocked
  -> SessionDisconnected
  -> SystemSuspended
```

The states are mutually exclusive for attribution. Precedence is:

```text
SystemSuspended
SessionDisconnected
SessionLocked
Idle
Active
Unknown
```

A higher-precedence state suppresses lower-precedence attribution. For example, a locked workstation is not additionally reported as idle even if no input occurs.

The Core state machine accepts complete immutable condition snapshots containing the resolved input-activity condition and current lock, disconnect, and suspend flags. Reapplying an equivalent snapshot is idempotent. Clearing a higher-precedence flag restores the lower condition supplied by the same reconciliation snapshot.

### 3.1 Active to Idle

Transition when monotonic elapsed time since the last observed user input reaches the configured threshold, default 5 minutes. The logical idle interval begins at `lastInputTime + threshold`, not at the later sampling moment.

### 3.2 Idle to Active

Transition when a subsequent sample reports input newer than the idle boundary. The application may use the sampled detection timestamp as the end when the exact input timestamp cannot be mapped safely to wall-clock time.

### 3.3 Lock and disconnect

Lock/disconnect transitions close any active or idle attribution interval at the transition timestamp. Unlock/reconnect triggers immediate reconciliation of foreground window and last-input state.

### 3.4 Suspend and resume

Suspend closes all active attribution and flushes immediately. Resume enters `Unknown`, refreshes session/idle/foreground state, then transitions to the resolved presence state.

## 4. Foreground context state

Foreground context contains:

- top-level HWND;
- process identity;
- transient raw caption;
- normalized application ID;
- safe context label;
- category;
- matched rule ID;
- classification confidence.

Persisted events never include the transient raw caption or full executable path.

Foreground changes are meaningful only for attribution while:

- tracker state is `Running`;
- presence state is `Active`.

The latest foreground context is still cached while idle, locked, or paused so that it can be reconciled on return, but it does not accumulate active time.

## 5. Meeting state

```text
None
  -> Candidate
  -> Confirmed
  -> EndingCandidate
  -> None
```

- `Candidate`: evidence exceeds the start threshold but has not satisfied debounce.
- `Confirmed`: meeting interval is active.
- `EndingCandidate`: evidence dropped below the continuation threshold but end debounce has not elapsed.
- `None`: no active meeting.

Manual override can transition directly to `Confirmed` or `None` and records `source=manual`.

Meeting state is independent of foreground context and presence. A confirmed meeting may continue while another application is foreground. During lock, disconnect, or suspend, the meeting detector should normally end or suspend attribution according to provider evidence and configuration; it must not assume the meeting continued through an unobservable suspend gap.

## 6. State-transition event rules

Events are emitted for semantic changes, not every sample.

Examples:

- repeated foreground hook for the same HWND/title classification: no event;
- title change producing the same safe context/category: no event unless configured to track context changes;
- Active to Idle: `idle.started`;
- Idle to Active: `idle.ended` followed by foreground reconciliation when needed;
- Running to Paused: `tracking.paused`;
- Paused to Running: `tracking.resumed` and a fresh state snapshot;
- meeting Candidate to Confirmed: `meeting.started`;
- daily rollover: `day.ended`, open new file, `day.started`, `state.snapshot`.

## 7. Initialization and recovery

At startup:

1. load the last valid configuration;
2. load persisted operational state, including manual pause;
3. detect whether the previous process stopped cleanly;
4. emit `tracker.started`;
5. emit `tracker.recovered_after_unclean_shutdown` when applicable;
6. resolve session, power, idle, foreground, and meeting state;
7. emit a complete `state.snapshot`.

No duration is invented for the gap between the last recorded heartbeat/event and the new process start. Reports show it as unobserved time.

## 8. Rollover behavior

At local midnight:

1. close open logical intervals at midnight in report reconstruction semantics;
2. write `day.ended` to the old file;
3. flush and close the old stream;
4. open the new daily file;
5. write `day.started`;
6. write `state.snapshot` carrying active states into the new day.

Monotonic timers continue across rollover. Only persistence partitioning changes.
