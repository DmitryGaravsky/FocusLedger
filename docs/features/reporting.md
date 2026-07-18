# Reporting Specification

**Status:** Approved baseline

## 1. Objective

Reporting transforms append-only events into human-readable activity intervals and metrics. It must be deterministic, privacy-safe, offline, and resilient to partial or imperfect input.

## 2. Output

Each report is one HTML file containing embedded CSS and JavaScript. It must:

- open through `file://`;
- require no server;
- load no remote resources;
- contain no tracking code;
- HTML-encode all dynamic strings;
- include the FocusLedger version and report-generation timestamp;
- state the input date range and local timezone context.

## 3. Input processing

The report reader streams all matching daily JSONL files in chronological order. It:

- validates the common envelope;
- supports known schema versions;
- de-duplicates by event ID;
- sorts by timestamp and sequence where files are merged;
- ignores an incomplete trailing line;
- records malformed-line and unsupported-schema data-quality issues;
- does not expose raw malformed content in the report.

## 4. Interval reconstruction

The reducer reconstructs:

- tracker Running/Paused intervals;
- Active/Idle/Locked/Disconnected/Suspended/Unknown intervals;
- foreground application/category/context intervals;
- meeting intervals;
- observed versus unobserved gaps.

Precedence for exclusive time accounting:

```text
Unobserved
Suspended
Disconnected
Locked
Paused
Idle
Active foreground activity
```

Meeting is an overlay dimension and may overlap Active foreground activity.

## 5. Required metrics

### 5.1 Summary

- report span;
- observed time;
- unobserved time;
- tracked Running time;
- active time;
- idle time;
- locked time;
- disconnected time;
- suspended time;
- paused time;
- meeting time;
- productive, neutral, unproductive, excluded, and unclassified active time.

### 5.2 Timeline

Chronological intervals with:

- start/end local time;
- duration;
- presence state;
- application;
- category;
- safe context when available;
- meeting overlay;
- data-quality marker when inferred from recovery boundaries.

Intervals shorter than the configured display threshold may be merged into adjacent equivalent intervals or grouped as short transitions, without changing totals.

### 5.3 Category summary

For each category:

- active duration;
- percentage of active time;
- interval count;
- first/last occurrence;
- longest continuous interval;
- productivity disposition and weight.

### 5.4 Application summary

For each normalized application:

- active duration;
- percentage of active time;
- foreground switch count;
- first/last seen;
- dominant category;
- safe contexts when enabled.

### 5.5 Focus analysis

- number of focus sessions;
- total focus time;
- average session duration;
- longest session;
- context switches per active hour;
- fragmentation score with documented formula;
- neutral gaps absorbed by focus-session rules.

Default focus session:

- productive duration at least 10 minutes;
- neutral gaps no longer than 60 seconds may be bridged;
- idle/locked/paused/disconnected/suspended intervals always break a focus session.

### 5.6 Meeting analysis

- meeting count;
- total meeting duration;
- average and longest meeting;
- provider distribution;
- manual versus automatic detection;
- confidence distribution;
- foreground category distribution during meetings;
- meeting-detector unavailable periods.

### 5.7 Lost-time analysis

Lost time is derived only from categories explicitly configured as `unproductive`. It does not include:

- unknown/unclassified time by default;
- idle time;
- locked/disconnected/suspended time;
- paused time;
- outside-schedule time merely because it is outside schedule.

When a working schedule is enabled, the report separates inside-schedule and outside-schedule values.

### 5.8 Data quality

Show:

- unclean shutdown recovery;
- unobserved gaps;
- malformed lines;
- unsupported schema files;
- unavailable collectors/adapters;
- access-denied foreground observations;
- unclassified active time;
- manual meeting overrides;
- configuration changes during the report range.

## 6. User interactions

The report may include local client-side controls for:

- collapsing sections;
- filtering timeline by category/application;
- toggling meeting overlays;
- switching duration units;
- hiding intervals below a threshold.

All data required by these controls is embedded in the report file.

## 7. File naming

Examples:

```text
activity-report-2026-07-18.html
activity-report-2026-07-12-to-2026-07-18.html
activity-report-2026-07.html
```

If a file exists, an interactive generation may replace the canonical report atomically. A CLI option may support a distinct output path in a later milestone.

## 8. Security

- Encode strings for HTML text, attribute, and JavaScript contexts separately.
- Prefer serializing embedded report data as JSON in a safe script block with escaping for `<`, `>`, `&`, U+2028, and U+2029.
- Do not insert strings using `innerHTML`.
- Do not use `eval`, external scripts, or remote fonts.
- Do not expose local absolute input paths in the report.

## 9. CLI behavior

Interactive tray generation opens the report by default. CLI generation is non-interactive:

```text
--report today
--report yesterday
--report last-7-days
--report current-month
--report-from 2026-07-01 --report-to 2026-07-18
```

Add `--open` to open the generated file.

## 10. Determinism

Given identical input events, configuration snapshot, report range, and report-version logic, metric totals must be identical. Generation timestamp and output-file metadata may differ. Golden-file tests should normalize volatile metadata.
