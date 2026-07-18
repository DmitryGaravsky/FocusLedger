# Product Requirements

**Status:** Approved baseline
**Target:** FocusLedger 1.0
**Language:** English
**License:** MIT

## 1. Product statement

FocusLedger is a local-only Windows activity tracker for personal use. It records application-focus transitions and system-presence transitions as privacy-normalized events, classifies the resulting activity, detects meetings using multiple signals, and produces standalone HTML reports.

The application has no main window. It runs as a Windows tray application with a message loop and exposes a context menu for operational commands.

## 2. Supported environment

The first stable release MUST support:

- Windows 10 22H2;
- Windows 11;
- x64 hardware and operating systems;
- `.NET 10` with `TargetFramework` set to `net10.0-windows`;
- self-contained, single-file `win-x64` publication;
- execution without administrator privileges;
- per-user execution and storage;
- concurrent use by different Windows users, with one process and one data directory per user.

ARM64, Windows Server, Windows 10 versions older than 22H2, and 32-bit Windows are outside the 1.0 support commitment.

## 3. Primary user journeys

### 3.1 Start and run continuously

The user launches one executable. FocusLedger starts without a console or main window, creates a tray icon, initializes collectors, and appends events to the current day's JSONL file.

### 3.2 Pause and resume

The user selects **Pause tracking** from the tray. FocusLedger records a pause transition and stops attributing foreground time until the user resumes. Manual pause state persists across process restart.

### 3.3 Generate a report

The user selects a predefined range from the tray:

- Today;
- Yesterday;
- Last 7 days;
- Current month.

FocusLedger reads the relevant JSONL files, generates one standalone HTML file, and opens it in the default browser for an interactive tray command. CLI generation does not open the report unless explicitly requested.

### 3.4 Configure classification

The user opens `config.json`, edits application/category/rule settings, and saves it. FocusLedger validates and atomically adopts the new immutable configuration snapshot. On validation failure, it keeps the last valid configuration and signals an error without stopping tracking.

### 3.5 Enable autostart

The user explicitly enables **Start with Windows** from the tray. FocusLedger creates a per-user startup entry. Autostart MUST NOT be enabled automatically.

## 4. Functional requirements

### 4.1 Process lifetime and tray

The application MUST:

- use `OutputType=WinExe`;
- have no main application window;
- run a Windows message loop;
- expose a `NotifyIcon` with a context menu;
- indicate at least Collecting, Idle, Paused, Meeting, and Error states;
- support pause, resume, report generation, folder access, configuration access, configuration reload, autostart toggle, and exit;
- enforce one instance per Windows user;
- route secondary-instance commands to the primary process.

### 4.2 Foreground application tracking

The application MUST:

- detect foreground-window changes primarily through WinEvent hooks;
- use periodic reconciliation to recover missed notifications;
- obtain process identity and top-level window caption where access is permitted;
- detect title changes for the current top-level foreground window;
- degrade to application-only classification when a caption cannot be read;
- never fail the main tracking pipeline because one application denies inspection.

### 4.3 Presence tracking

The application MUST distinguish:

- active user presence;
- idle state;
- locked workstation;
- disconnected user session;
- suspended system;
- manually paused tracking;
- unknown state during initialization or recovery.

Default idle threshold: 5 minutes.

Input activity below the threshold remains attributed to the current activity interval. Only the elapsed idle duration and transition are observed; no input contents are collected.

### 4.4 Power and session tracking

The application MUST track:

- lock and unlock;
- console and remote session connect/disconnect;
- system suspend and resume;
- logoff/shutdown signals where available;
- unclean restart recovery.

It MUST flush critical transitions promptly and reconstruct state after resume or reconnect.

### 4.5 Classification

The application MUST provide:

- application identity mapping;
- application-specific title parsers;
- privacy redaction and normalization before persistence;
- ordered classification rules;
- hierarchical categories;
- productivity disposition: Productive, Neutral, Unproductive, or Excluded;
- optional productivity weight from 0.0 to 1.0;
- an `unclassified` fallback that is not automatically considered unproductive.

Default application coverage for 1.0:

- Visual Studio;
- Visual Studio Code;
- JetBrains Rider;
- Windows Terminal;
- PowerShell;
- Google Chrome;
- Microsoft Edge;
- Mozilla Firefox;
- Microsoft Teams;
- Zoom;
- Slack;
- Microsoft Outlook;
- Microsoft Word;
- Microsoft Excel;
- Microsoft PowerPoint;
- Adobe Acrobat Reader/Acrobat;
- File Explorer;
- Notepad.

### 4.6 Browser context

For Chrome, Edge, and Firefox, the application SHOULD parse the active top-level window title and remove known browser suffixes. The first stable release MUST NOT require a browser extension and MUST NOT persist full URLs.

UI Automation access to the address bar is excluded from the default 1.0 implementation. The architecture MAY allow a future opt-in adapter.

### 4.7 Meeting detection

The application MUST detect meeting start and end independently from foreground-window attribution. Detection MUST combine multiple signals and support:

- Microsoft Teams;
- Zoom;
- Google Meet in Chrome and Edge;
- Slack Huddles;
- Webex.

Microphone or audio-session activity alone MUST NOT be treated as definitive proof of a meeting. Meeting titles MUST NOT be persisted by default. Manual **Start meeting** and **End meeting** commands SHOULD be included by 1.0 as override mechanisms.

### 4.8 Storage

The application MUST:

- store data under `%LocalAppData%\FocusLedger`;
- create one JSONL file per local calendar day;
- use UTF-8 without BOM;
- append complete JSON objects one per line;
- include schema version, sequence, event ID, UTC timestamp, and UTC offset in every event;
- permit concurrent report reads with the active writer;
- tolerate an incomplete final line after a crash;
- create a state snapshot at the beginning of each daily file;
- default to 365-day activity-data retention;
- default to 14-day diagnostic-log retention.

### 4.9 Reporting

The application MUST generate one self-contained HTML file with no network dependencies. Reports MUST support:

- one day;
- predefined ranges;
- an explicit date range through CLI;
- tracked, active, idle, locked/disconnected, paused, meeting, productive, neutral, unproductive, excluded, and unclassified durations;
- a chronological timeline;
- category and application summaries;
- focus-session metrics;
- context-switch metrics;
- meeting summaries;
- lost-time analysis;
- data-quality indicators.

### 4.10 Command line

The application SHOULD support:

```text
FocusLedger.exe --status
FocusLedger.exe --pause
FocusLedger.exe --resume
FocusLedger.exe --quit
FocusLedger.exe --report today
FocusLedger.exe --report yesterday
FocusLedger.exe --report last-7-days
FocusLedger.exe --report current-month
FocusLedger.exe --report-from 2026-07-01 --report-to 2026-07-18
FocusLedger.exe --report today --open
FocusLedger.exe --open-config
FocusLedger.exe --open-data
FocusLedger.exe --enable-startup
FocusLedger.exe --disable-startup
FocusLedger.exe --validate-config
```

## 5. Default category taxonomy

The built-in taxonomy is:

```text
work.development
work.code-review
work.research
work.documentation
work.communication
work.meeting
work.administration
work.planning
work.management
work.mentoring
work.security
learning.technical
learning.language
personal.communication
personal.administration
personal.entertainment
distraction.social
distraction.video
distraction.news
distraction.games
system
idle
locked
paused
unclassified
```

Users may replace or extend this taxonomy through configuration.

## 6. Working schedule behavior

Working schedule support MUST be configurable but disabled by default. When disabled, reports show all tracked activity and do not claim that outside-hours activity is wasted time. When enabled, reports distinguish inside-schedule and outside-schedule activity. Activity outside the schedule is still collected and shown separately; it is not discarded.

## 7. Non-functional requirements

| Measure | 1.0 target |
|---|---:|
| Average CPU during normal collection | below 0.3% on a contemporary desktop |
| Working set | below 80 MB after steady state |
| Normal event-data size | below 5 MB per day |
| Graceful-shutdown event loss | zero |
| Crash-related loss | at most the configured flush interval, default 2 seconds |
| Daily report generation | below 2 seconds for normal data volume |
| Monthly report generation | below 10 seconds for normal data volume |
| Network connections | zero |
| Administrator rights | not required |
| Installed .NET runtime | not required |

Performance targets are release gates, not guarantees for pathological title-change rates or damaged input files.

## 8. Quality requirements

The 1.0 release MUST include:

- unit tests for platform-neutral state and classification logic;
- Windows integration tests where CI permits;
- deterministic clock tests;
- crash/incomplete-line recovery tests;
- daylight-saving and manual-clock-change tests;
- suspend/resume and lock/unlock scenario tests;
- long-running resource tests;
- privacy leakage tests for events, diagnostics, configuration errors, and reports;
- configuration migration and validation tests;
- event-schema compatibility tests.

## 9. Explicit exclusions for 1.0

The following are deferred:

- browser extension;
- persisted full URLs;
- screenshots or OCR;
- keystroke or mouse-event recording;
- cloud synchronization;
- remote dashboard;
- telemetry or update checks;
- automatic editing of historical activity;
- machine-wide service mode;
- plugin loading from third-party assemblies;
- mobile or non-Windows clients;
- automatic inference that the user was speaking rather than listening during a meeting.
