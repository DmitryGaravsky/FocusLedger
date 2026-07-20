# Development Roadmap and Feature Catalog

**Status:** Approved planning baseline
**Status values:** Planned, In Progress, Blocked, Complete, Deferred

## 1. Roadmap principles

- Each feature has a stable ID.
- Issues, branches, pull requests, tests, and release notes should reference the feature ID.
- A milestone is complete only when every required feature and release gate is complete.
- Documentation and tests are part of each feature, not separate cleanup work.
- Privacy, event compatibility, and continuous-operation reliability are cross-cutting release gates.

## 2. Milestone summary

| Milestone | Goal | Planned release |
|---|---|---|
| M0 | Repository and architectural foundation | 0.1.0 |
| M1 | Reliable baseline collection and JSONL persistence | 0.2.0 |
| M2 | Tray operations, single instance, configuration, autostart | 0.3.0 |
| M3 | Privacy-safe classification and browser context | 0.4.0 |
| M4 | Standalone HTML reporting | 0.5.0 |
| M5 | Multi-signal meeting detection | 0.6.0 |
| M6 | Hardening, open-source release quality, and 1.0 stabilization | 1.0.0 |
| M7 | Advanced post-1.0 capabilities | future |

## 3. M0 — Repository and foundation

### M0 exit criteria

- solution and project boundaries exist;
- CI builds and tests on Windows;
- all normative documentation is committed;
- coding agents can identify feature IDs and acceptance criteria;
- no production collectors are required yet.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| FND-001 | Create .NET 10 solution and project structure | Complete | none | Four source projects and three test projects build in Release with nullable and warnings-as-errors enabled. |
| FND-002 | Establish shared build configuration | Complete | FND-001 | Common properties, analyzers, deterministic builds, and version metadata are applied consistently. |
| FND-003 | Define core abstractions | Complete | FND-001 | Signal, event, clock, configuration, writer, classifier, and platform-source contracts compile without Windows dependencies in Core. |
| FND-004 | Add GitHub Actions pull-request CI | Complete | FND-001 | Windows CI verifies repository policy, restores, builds, tests, sanitizes TRX, and uploads only test results. It does not publish, execute, or upload the application EXE. |
| FND-005 | Add repository governance | Complete | FND-004 | README, AGENTS, contribution guidance, security policy, issue templates, and MIT license are present. |
| FND-006 | Freeze schema-1 naming baseline | Complete | FND-003 | Event and configuration names are reviewed against docs; compatibility fixtures are created. |

## 4. M1 — Baseline collection and persistence

### M1 exit criteria

FocusLedger can run for a full day, correctly record foreground/presence/session/power transitions, roll over at midnight, survive restart, and produce privacy-safe JSONL without a tray-dependent workflow.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| CORE-001 | Implement tracker lifecycle state machine | Complete | FND-003 | Starting, Running, Paused, Stopping, Stopped, and Faulted transitions are deterministic and unit-tested. |
| CORE-002 | Implement presence state machine | Complete | FND-003 | Active, Idle, Locked, Disconnected, Suspended, and Unknown precedence is unit-tested. |
| CORE-003 | Implement serialized signal coordinator | Complete | CORE-001, CORE-002 | One bounded channel and one consumer own runtime state; duplicate/coalescible signals do not create duplicate events. |
| WIN-001 | Create hidden message-loop host | Complete | FND-001 | WinExe starts without console/main window and processes Windows messages until clean shutdown. |
| WIN-002 | Foreground WinEvent collector | Complete | WIN-001, CORE-003 | Foreground changes and selected title changes become signals; callbacks remain non-blocking. |
| WIN-003 | Foreground reconciliation sampler | Complete | WIN-002 | Missed hook events are corrected within the configured interval without duplicate event output. |
| WIN-004 | Process/application metadata wrapper | Complete | WIN-002 | Normal process identity is read; exits/access-denied/protected processes degrade safely. |
| WIN-005 | Idle detector | Complete | CORE-002 | Default 5-minute threshold uses last-input data and accurate threshold boundary semantics. |
| WIN-006 | WTS session collector | Complete | WIN-001, CORE-002 | Lock/unlock and local/remote connect/disconnect are recorded for the current user session. |
| WIN-007 | Power collector | Complete | WIN-001, CORE-002 | Suspend/resume closes attribution, flushes, and reconciles state after resume. |
| DATA-001 | Define source-generated event serialization | Complete | FND-006 | Schema-1 event fixtures serialize exactly and readers ignore unknown additive properties. |
| DATA-002 | Implement single JSONL writer | Complete | CORE-003, DATA-001 | One writer appends UTF-8 lines, supports concurrent readers, timed flush, and critical immediate flush. |
| DATA-003 | Implement daily rollover | Complete | DATA-002 | Local-midnight rollover writes day end/start and a complete state snapshot. |
| DATA-004 | Implement state file and clean-shutdown marker | Complete | DATA-002 | Sequence, manual pause, and clean shutdown recover without personal data. |
| DATA-005 | Implement crash-tolerant JSONL reader | Complete | DATA-001 | Incomplete trailing line is ignored; malformed middle lines produce safe data-quality errors. |
| OPS-001 | Add heartbeat and unclean-restart recovery | Complete | DATA-004 | Restart after forced termination emits recovery event and does not invent gap activity. |
| TEST-001 | Add M1 end-to-end scenario harness | Complete | WIN-002 through DATA-005 | Synthetic foreground, idle, lock, suspend, midnight, and crash scenarios pass. |

## 5. M2 — Tray, commands, configuration, and autostart

### M2 exit criteria

The user can operate the application entirely through the tray or CLI, run one instance per user, modify validated configuration, and explicitly enable autostart.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| UX-001 | Implement tray icon and state tooltip | Complete | WIN-001, CORE-001, CORE-002 | Active, Idle, Paused, Meeting placeholder, and Error states are represented. |
| UX-002 | Implement tray command menu | Complete | UX-001 | Pause/resume, folders, config, reload, report placeholders, autostart, and exit behave by state. |
| OPS-002 | Implement persisted manual pause | Complete | CORE-001, DATA-004, UX-002 | Pause survives restart and attribution remains stopped until explicit resume. |
| OPS-003 | Implement per-user single instance | Complete | WIN-001 | Second interactive launch does not create another collector. |
| OPS-004 | Implement same-user named-pipe commands | Complete | OPS-003 | CLI commands are validated, bounded, ACL-restricted, and acknowledged. |
| CFG-001 | Implement configuration model and defaults | Complete | FND-006 | Complete documented default config can be serialized and loaded. |
| CFG-002 | Implement configuration validation | Planned | CFG-001 | Duplicate IDs, invalid references, unsafe privacy settings, regex errors, and invalid ranges are rejected safely. |
| CFG-003 | Implement atomic hot reload | Planned | CFG-002 | Valid config replaces snapshot; invalid config leaves previous snapshot active and signals an error. |
| WIN-008 | Implement per-user autostart | Planned | UX-002 | Explicit command creates/removes HKCU Run entry and detects moved executable path. |
| OPS-005 | Implement data/config/report folder commands | Planned | UX-002 | User-commanded shell operations open only known local paths. |
| OPS-006 | Implement retention maintenance | Planned | CFG-001, DATA-003 | Activity and diagnostics retention runs safely inside storage root. |
| TEST-002 | Add command/config/autostart integration tests | Planned | OPS-004, CFG-003, WIN-008 | Same-user command, malformed command, invalid config, reload, and registry scenarios pass. |

## 6. M3 — Privacy and classification

### M3 exit criteria

Raw titles remain transient, default application coverage is classified, browser tab titles can influence classification without being persisted, and privacy canary tests pass.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| PRIV-001 | Implement privacy validator | Planned | CFG-002 | Unsafe context, path, URL, email, IP, and long-ID values are rejected before persistence. |
| PRIV-002 | Implement Strict and Balanced modes | Planned | PRIV-001 | Strict persists no context; Balanced persists only validated constant/allowlisted context. |
| PRIV-003 | Implement safe diagnostics policy | Planned | PRIV-001 | Errors use fixed codes and cannot leak title/path/config canaries. |
| CLS-001 | Implement application identity registry | Planned | CFG-001, WIN-004 | Default application list maps processes to stable IDs/families without paths. |
| CLS-002 | Implement ordered rule engine | Planned | CLS-001, CFG-003 | Priority, matching, category, constant context, rule ID, and confidence are deterministic. |
| CLS-003 | Implement Chromium title parser | Planned | CLS-002, PRIV-001 | Browser suffix is removed transiently; matched rules emit constants; raw tab title never persists. |
| CLS-004 | Implement Firefox title parser | Planned | CLS-002, PRIV-001 | Same privacy and classification behavior as supported Firefox captions. |
| CLS-005 | Implement IDE/document safe parsers | Planned | CLS-002, PRIV-001 | IDE project/file and Office document names are never persisted; functional contexts remain useful. |
| CLS-006 | Implement category/disposition model | Planned | CFG-001 | Hierarchical categories, disposition, and weight drive persisted classification and reports. |
| CLS-007 | Implement regex safety controls | Planned | CLS-002 | Regex length, input length, timeout, compilation errors, and catastrophic-pattern tests are handled. |
| TEST-003 | Add privacy canary suite | Planned | PRIV-001 through CLS-005 | Canary personal values never appear in events, state, diagnostics, or test artifacts. |
| TEST-004 | Add built-in classification fixtures | Planned | CLS-001 through CLS-006 | Default applications and browser/IDE/document scenarios classify as documented. |

## 7. M4 — Standalone reporting

### M4 exit criteria

The user can generate correct offline reports for all required ranges. Reports are deterministic, encoded, privacy-safe, and explain data-quality limitations.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| RPT-001 | Implement multi-file event reader | Planned | DATA-005 | Range reader streams files, de-duplicates event IDs, and reports safe parse issues. |
| RPT-002 | Implement interval reducer | Planned | RPT-001, CORE-002 | Presence/tracker/foreground precedence and meeting overlay produce correct intervals. |
| RPT-003 | Implement summary metrics | Planned | RPT-002, CLS-006 | Required duration and productivity totals match golden fixtures. |
| RPT-004 | Implement timeline and application/category views | Planned | RPT-002 | Timeline, category, application, and safe-context sections render correct totals. |
| RPT-005 | Implement focus and fragmentation metrics | Planned | RPT-002 | Focus thresholds/gaps and context-switch metrics are documented and tested. |
| RPT-006 | Implement meeting and lost-time metrics | Planned | RPT-002 | Meeting overlay and explicitly unproductive categories are calculated without counting idle/unknown as wasted. |
| RPT-007 | Implement data-quality section | Planned | RPT-001, RPT-002 | Gaps, malformed lines, recovery, unavailable collectors, and unclassified time are visible. |
| RPT-008 | Implement standalone HTML renderer | Planned | RPT-003 through RPT-007 | One file uses embedded assets, no remote resources, safe encoding, and no `innerHTML` with untrusted data. |
| RPT-009 | Add tray and CLI report commands | Planned | UX-002, OPS-004, RPT-008 | Required preset/date-range commands generate files; interactive command opens report, CLI needs `--open`. |
| TEST-005 | Add report golden/privacy tests | Planned | RPT-008 | Deterministic fixtures pass and canary values cannot execute or leak. |

## 8. M5 — Meeting detection

### M5 exit criteria

Meetings are detected across the required providers using multi-signal confidence and hysteresis. Foreground tracking remains independent and manual overrides are available.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| MTG-001 | Implement meeting state machine | Planned | CORE-003 | Candidate, Confirmed, EndingCandidate, and None transitions satisfy threshold/debounce tests. |
| MTG-002 | Implement Core Audio evidence source | Planned | WIN-004 | Provider process sessions are observed without audio capture; COM failures degrade safely. |
| MTG-003 | Implement Teams adapter | Planned | MTG-001, MTG-002, CLS-001 | Teams call evidence reaches stable start/end behavior in fixtures. |
| MTG-004 | Implement Zoom adapter | Planned | MTG-001, MTG-002, CLS-001 | Zoom behavior meets confidence/debounce tests. |
| MTG-005 | Implement Google Meet adapter | Planned | MTG-001, MTG-002, CLS-003 | Browser safe context plus audio evidence detects calls without URL persistence. |
| MTG-006 | Implement Slack Huddles adapter | Planned | MTG-001, MTG-002, CLS-001 | Slack process/window/audio evidence works with false-positive controls. |
| MTG-007 | Implement Webex adapter | Planned | MTG-001, MTG-002, CLS-001 | Webex process/window/audio evidence works with safe failures. |
| MTG-008 | Implement manual meeting override | Planned | MTG-001, UX-002 | Tray commands start/end a manual meeting and persisted events identify manual source. |
| MTG-009 | Integrate meeting overlay into reports | Planned | MTG-003 through MTG-008, RPT-006 | Reports show provider, duration, confidence, manual status, and foreground distribution. |
| TEST-006 | Add meeting false-positive/failure suite | Planned | MTG-002 through MTG-007 | Media playback, idle provider processes, adapter failure, and audio loss do not create unstable meetings. |

## 9. M6 — Hardening and 1.0 release

### M6 exit criteria

All product requirements, performance targets, privacy gates, compatibility gates, open-source governance, and Windows support tests pass.

| ID | Feature | Status | Dependencies | Acceptance criteria |
|---|---|---|---|---|
| PERF-001 | Optimize steady-state collection | Planned | M1-M5 | Average CPU and memory meet documented targets in endurance scenario. |
| REL-001 | 24-hour endurance test | Planned | PERF-001 | No unbounded handles, threads, memory, queue, or COM growth. |
| REL-002 | Windows 10/11 support matrix validation | Planned | M1-M5 | Manual and automated smoke tests pass on both supported OS families. |
| REL-003 | Schema/config compatibility suite | Planned | DATA-001, CFG-001, RPT-001 | Supported fixtures remain readable and migrations preserve user rules. |
| REL-004 | Single-file clean-machine validation | Planned | FND-001 | Published EXE runs without installed .NET and behavior matches documented extraction policy. |
| SEC-001 | Security/privacy release review | Planned | PRIV-003, TEST-003, TEST-005 | No canary leaks, no outbound network, pipe ACL correct, report encoding verified. |
| OSS-001 | Finalize MIT/open-source repository files | Planned | FND-005 | License, contribution, code of conduct, security policy, and support boundaries are clear. |
| OSS-002 | Dependency license and SBOM gate | Planned | FND-004 | Every dependency is compatible/documented and release SBOM is generated. |
| REL-005 | GitHub release workflow | Planned | OSS-002, REL-004 | Tagged release publishes EXE, checksums, SBOM, and notes reproducibly. |
| DOC-001 | User and developer documentation review | Planned | all | Installation-free use, config, privacy, reports, troubleshooting, and architecture are current. |
| REL-006 | 1.0 release candidate and stabilization | Planned | all M6 items | No open blocker; high-severity bugs resolved; release checklist signed off. |

## 10. M7 — Deferred feature backlog

| ID | Feature | Status | Rationale / prerequisite |
|---|---|---|---|
| ADV-001 | Optional UI Automation browser-domain adapter | Deferred | Requires explicit privacy review, robust timeouts, and per-browser testing. |
| ADV-002 | Browser extension and Native Messaging | Deferred | Breaks one-file operational model and expands security/privacy surface. |
| ADV-003 | Historical interval correction | Deferred | Requires append-only correction events and report semantics. |
| ADV-004 | Rule creation from current activity | Deferred | Needs privacy-safe UX without exposing raw titles. |
| ADV-005 | Additional meeting providers | Deferred | Add Discord, Telegram, WhatsApp, Skype, or Jitsi only with reliable evidence. |
| ADV-006 | ARM64 release | Deferred | Requires separate RID, CI, and manual validation. |
| ADV-007 | Encrypted local storage | Deferred | Requires explicit key management and report-access design. |
| ADV-008 | Configurable report export formats | Deferred | CSV/JSON/Markdown after HTML model stabilizes. |
| ADV-009 | Signed adapter/plugin model | Deferred | Arbitrary in-process plugin loading is prohibited in 1.0. |
| ADV-010 | Working-schedule calendar exceptions | Deferred | Requires holiday/custom-day model without network lookup. |

## 11. Cross-cutting release gates

Every milestone after M0 must satisfy:

- no network communication;
- no raw title/path/URL persistence in default mode;
- event writer remains single-owner;
- message loop remains non-blocking;
- tests and documents updated;
- new dependencies pass license/security review;
- failure of an optional adapter cannot stop core collection;
- all new persisted fields have compatibility tests.

## 12. Suggested issue labels

```text
area:core
area:windows
area:data
area:privacy
area:classification
area:meeting
area:reporting
area:operations
area:documentation
kind:feature
kind:bug
kind:architecture
kind:test
priority:blocker
priority:high
priority:normal
status:blocked
```
