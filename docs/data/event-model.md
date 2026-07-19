# Event Model and JSONL Contract

**Status:** Normative schema version 1 baseline

## 1. Storage layout

```text
%LocalAppData%\FocusLedger\
  config.json
  state.json
  data\
    2026-07\
      activity-2026-07-18.jsonl
  reports\
    activity-report-2026-07-18.html
  logs\
    diagnostic-2026-07-18.log
```

The daily partition is based on local calendar date at the time of writing. Every event also includes an absolute UTC timestamp and UTC offset.

## 2. JSONL rules

- One JSON object per line.
- UTF-8 without BOM.
- No JSON array wrapper.
- Append-only during normal operation.
- A writer opens the current file with sharing that permits readers.
- Unknown properties are ignored by readers.
- A malformed final line is treated as crash residue and ignored with a data-quality warning.
- A malformed non-final line is reported and skipped; report output identifies the file and line number without reproducing sensitive content.

`JsonlActivityEventWriter` is the sole append owner for its active file. It opens the stream with `FileShare.Read`, which permits report readers but rejects a second writer, and writes source-generated UTF-8 JSON followed by one LF byte without a BOM. A single asynchronous gate serializes appends, explicit flushes, timer flushes, and disposal.

The default flush interval is supplied by validated configuration and is two seconds. Critical lifecycle, tracking, session, power, meeting, day-end, and snapshot event types bypass the timer and flush immediately after their complete line is appended. Background flush failure makes the writer unavailable through a safe fixed error that does not include the activity-file path. Disposal cancels the timer, performs the final flush, and closes the stream deterministically.

## 3. Common envelope

Every event contains:

```json
{
  "schemaVersion": 1,
  "sequence": 182,
  "eventId": "019bb9c0-55ad-7368-b67c-290b2163c544",
  "timestampUtc": "2026-07-18T08:14:32.421Z",
  "utcOffsetMinutes": 120,
  "type": "foreground.changed"
}
```

Field semantics:

- `schemaVersion`: integer data-contract version.
- `sequence`: monotonically increasing per installation, persisted across daily files when possible.
- `eventId`: UUIDv7-compatible unique identifier.
- `timestampUtc`: RFC 3339 UTC timestamp with milliseconds or higher precision.
- `utcOffsetMinutes`: local UTC offset at the event instant.
- `type`: stable lowercase dotted event name.

Optional common fields:

- `source`: normalized source such as `win-event-hook`, `reconciliation`, `manual`, or `recovery`.
- `correlationId`: identifier connecting related operations such as report generation or meeting candidate/confirmation.

## 4. Application payload

```json
{
  "app": {
    "id": "visual-studio",
    "processName": "devenv.exe",
    "family": "development-environment"
  }
}
```

Rules:

- no full executable path;
- no command line;
- no PID in persisted data unless a future diagnostic event has a documented need;
- `id` is a normalized stable identifier;
- unknown executables may use a normalized lower-case file name as `id` when privacy validation permits it.

## 5. Classification payload

```json
{
  "context": {
    "label": "source-code",
    "privacy": "normalized"
  },
  "classification": {
    "category": "work.development",
    "disposition": "productive",
    "weight": 1.0,
    "ruleId": "builtin.visual-studio.source-code",
    "confidence": 0.95
  }
}
```

`context.label` is omitted when no safe label exists. Raw titles are never represented.

## 6. Event types

### 6.1 Lifecycle

- `tracker.started`
- `tracker.stopping`
- `tracker.stopped`
- `tracker.recovered_after_unclean_shutdown`
- `day.started`
- `day.ended`
- `state.snapshot`
- `heartbeat`

### 6.2 Tracking control

- `tracking.paused`
- `tracking.resumed`

### 6.3 Foreground

- `foreground.changed`
- `foreground.context_changed`
- `foreground.unavailable`

### 6.4 Presence and session

- `idle.started`
- `idle.ended`
- `session.locked`
- `session.unlocked`
- `session.connected`
- `session.disconnected`
- `session.logon`
- `session.logoff`

### 6.5 Power and time

- `system.suspending`
- `system.resumed`
- `system.time_changed`
- `system.timezone_changed`

### 6.6 Meeting

- `meeting.started`
- `meeting.context_changed`
- `meeting.ended`

Candidate meeting events remain diagnostic/internal by default and are not required in the activity stream.

### 6.7 Configuration and errors

- `configuration.reloaded`
- `configuration.reload_failed`
- `collector.error`
- `writer.error`

Error events must use safe enumerated codes and must not copy raw exception messages.

## 7. Complete examples

### 7.1 Tracker start

```json
{"schemaVersion":1,"sequence":1,"eventId":"019bb9b0-18ad-76a1-96e4-1d9db6e14b67","timestampUtc":"2026-07-18T06:58:03.101Z","utcOffsetMinutes":120,"type":"tracker.started","source":"interactive","version":"1.0.0"}
```

### 7.2 State snapshot

```json
{"schemaVersion":1,"sequence":2,"eventId":"019bb9b0-18ae-7519-b332-e8f29113c742","timestampUtc":"2026-07-18T06:58:03.142Z","utcOffsetMinutes":120,"type":"state.snapshot","trackerState":"running","presence":"active","meeting":{"state":"none"},"foreground":{"app":{"id":"visual-studio","processName":"devenv.exe","family":"development-environment"},"context":{"label":"source-code","privacy":"normalized"},"classification":{"category":"work.development","disposition":"productive","weight":1.0,"ruleId":"builtin.visual-studio.source-code","confidence":0.95}}}
```

### 7.3 Foreground change

```json
{"schemaVersion":1,"sequence":182,"eventId":"019bb9c0-55ad-7368-b67c-290b2163c544","timestampUtc":"2026-07-18T08:14:32.421Z","utcOffsetMinutes":120,"type":"foreground.changed","source":"win-event-hook","presence":"active","app":{"id":"visual-studio","processName":"devenv.exe","family":"development-environment"},"context":{"label":"source-code","privacy":"normalized"},"classification":{"category":"work.development","disposition":"productive","weight":1.0,"ruleId":"builtin.visual-studio.source-code","confidence":0.95}}
```

### 7.4 Idle transition

```json
{"schemaVersion":1,"sequence":219,"eventId":"019bb9c8-f4f2-7757-9849-73632e459c19","timestampUtc":"2026-07-18T08:37:00.000Z","utcOffsetMinutes":120,"type":"idle.started","source":"last-input-sampler","thresholdSeconds":300}
```

```json
{"schemaVersion":1,"sequence":220,"eventId":"019bb9cc-0b20-7ce5-8e18-c83de9b2df6d","timestampUtc":"2026-07-18T08:41:12.209Z","utcOffsetMinutes":120,"type":"idle.ended","source":"last-input-sampler"}
```

### 7.5 Meeting start

```json
{"schemaVersion":1,"sequence":481,"eventId":"019bb9d8-5687-7c31-82b1-c96570924450","timestampUtc":"2026-07-18T08:42:11.182Z","utcOffsetMinutes":120,"type":"meeting.started","source":"detector","meeting":{"provider":"microsoft-teams","confidence":0.92,"evidence":["known-process","meeting-window","active-audio-session"]},"classification":{"category":"work.meeting","disposition":"productive","weight":1.0,"ruleId":"builtin.meeting.confirmed","confidence":0.92}}
```

### 7.6 Safe error

```json
{"schemaVersion":1,"sequence":512,"eventId":"019bb9e3-49e6-78ca-8e82-4ff07e4e532a","timestampUtc":"2026-07-18T09:05:04.774Z","utcOffsetMinutes":120,"type":"collector.error","component":"foreground-process-inspector","errorCode":"access-denied","nativeCode":5,"recoverable":true}
```

## 8. Heartbeats

Default heartbeat interval: 60 seconds while the tracker is running or paused. A heartbeat supports:

- identifying process-liveness gaps;
- bounding unobserved time after a crash;
- operational diagnostics.

Heartbeat events should remain compact and need not repeat full foreground context unless used as a periodic state snapshot by design.

## 9. Sequence and recovery

`state.json` stores the next sequence number, manual pause state, clean-shutdown marker, and minimal writer metadata. It contains no raw titles or full paths.

The schema-1 state contract contains exactly `schemaVersion`, `nextSequence`, `manualPause`, and `cleanShutdown`. Startup atomically changes `cleanShutdown` to `false` before collectors begin. Graceful shutdown sets it to `true` only after the activity writer has flushed and records the next unused sequence together with the current manual-pause choice.

State updates write a bounded source-generated UTF-8 document to `state.json.tmp`, flush it, and atomically replace `state.json`. A missing file represents a clean first run. A dirty marker represents an unclean previous run. Malformed, oversized, or unsupported state is reset to privacy-safe defaults and reported only through an enumerated recovery result; invalid file content and paths are never copied into an error. Unknown additive properties are ignored.

If sequence persistence is damaged, the writer may recover by scanning recent valid events and selecting a larger next value. Duplicate sequence values across a severe recovery are tolerated by readers when `eventId` differs; `eventId` remains the primary uniqueness key.

## 10. Daily rollover

The old file ends with `day.ended`. The new file begins with `day.started` and `state.snapshot`. If the process was not running at midnight, the next start creates the current file without synthesizing events for the gap.

The coordinator creates the rollover triplet before assigning and emitting the triggering activity event, preserving global sequence order. `DailyJsonlActivityEventWriter` strictly routes that pre-sequenced stream: `day.ended` remains in the old file, `day.started` advances to `data/{yyyy-MM}/activity-{yyyy-MM-dd}.jsonl`, and `state.snapshot` must immediately follow in the new file. A date transition without this ordered triplet is rejected rather than silently creating an incomplete daily partition.

The partition date is calculated from `timestampUtc` and the event's persisted `utcOffsetMinutes`, so UTC midnight does not incorrectly split a local day. A restart during an existing local day appends to the existing file without adding another day-start marker. Advancing across multiple dates creates only the current target file and does not synthesize activity for days when the process was not running.

## 11. Compatibility policy

Schema 1 readers:

- ignore unknown properties;
- accept unknown event types as timeline-neutral metadata;
- validate required envelope fields;
- expose unsupported schema versions as data-quality errors rather than silently misinterpreting them.

Core event serialization uses generated `System.Text.Json` metadata rather than reflection-based contract discovery. The schema-1 foreground contract has a flattened envelope and typed privacy-safe application, context, and classification payloads. Null optional common fields and absent safe context are omitted. Compatibility tests deserialize the normative fixture, serialize it back to the canonical property set, and verify that additive unknown properties at envelope and nested-payload levels are ignored.

Additive optional fields do not require a schema increment. Renaming/removing fields or changing semantics does.

## 12. Naming baseline fixtures

Schema 1 property names and event type spellings are frozen by:

- `tests/FocusLedger.Core.Tests/Fixtures/Compatibility/schema-1-foreground-event.json`;
- `tests/FocusLedger.Core.Tests/Fixtures/Compatibility/schema-1-event-types.json`.

The fixtures mirror this normative document and are compatibility inputs, not generated snapshots. A rename, removal, or semantic change requires the compatibility process above. Additive optional fields may extend future fixtures without removing the schema 1 names.
