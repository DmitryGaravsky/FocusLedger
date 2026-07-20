# Implementation Notes and Roadmap Deviations

**Status:** Living document

## 1. Purpose

This document records issues found during code review that were deliberately not acted on at the time, together with the reasoning. It is not a task backlog and not a bug tracker; entries here are things a maintainer decided to accept, defer, or close without a code change. Review this document before `REL-006` (1.0 release candidate and stabilization) so a deferred item is not silently forgotten.

Each entry states what was found, why it was not actioned immediately, and what would trigger revisiting it. Remove an entry once it is resolved or once a decision makes it permanently moot; do not let entries accumulate indefinitely.

## 2. Test-coverage deferrals

### 2.1 `TEST-001` presence scenarios do not exercise real JSONL persistence

`tests/FocusLedger.Reporting.Tests/M1EndToEndScenarioTests.cs` asserts the idle/lock/suspend presence sequence purely against `PresenceStateMachine` in memory. Foreground switching, midnight rollover, and crash recovery are the only parts of the scenario harness that go through the real coordinator → writer → JSONL → reader path. The roadmap's `TEST-001` acceptance criterion ("synthetic foreground, idle, lock, suspend, midnight, and crash scenarios pass") is marked Complete without this distinction being visible.

**Why deferred:** wiring the presence legs through the real persistence path is a larger effort than the rest of the coverage gaps found in the same review pass, and the underlying `PresenceStateMachine` logic is already unit-tested in isolation (`PresenceStateMachineTests.cs`).

**Revisit when:** before `REL-006`, either extend the scenario harness to route idle/lock/suspend through the real writer/reader like the other legs, or narrow the `TEST-001` acceptance-criteria wording in `docs/roadmap.md` to match what is actually exercised end-to-end.

### 2.2 `RetentionMaintenance` reparse-point skip is untested

`docs/product/privacy-and-data-policy.md` §8 and `AGENTS.md` require retention cleanup to skip reparse-point roots and month directories so it cannot follow a link outside the storage root. `IsReparsePoint` in `src/FocusLedger.Core/Persistence/RetentionMaintenance.cs` implements this, but no test creates a junction/symlink to verify the skip actually happens.

**Why deferred:** low risk (simple directory-attribute guard, unlikely to silently regress) relative to the cost of provisioning a junction in a portable NUnit test.

**Revisit when:** M6 hardening, alongside other Windows-specific edge-case testing (`REL-002` support-matrix validation).

## 3. Investigated and closed without action

### 3.1 `PerUserSingleInstance` `Global\` mutex namespace

Initially flagged as a risk: creating a `Global\`-prefixed mutex might require `SeCreateGlobalPrivilege`, which a standard user connecting over RDP may lack, potentially preventing startup. Verified against Microsoft's kernel-object-namespace documentation: the privilege check is limited to file-mapping and symbolic-link objects and does not apply to `Mutex`. A related but narrower risk (integrity-level DACL mismatch between an elevated and a non-elevated launch of the same mutex name) does not apply here because FocusLedger never self-elevates, so both console and RDP sessions of the same user run at the same integrity level.

**Outcome:** no code change. `src/FocusLedger.Windows/SingleInstance/PerUserSingleInstance.cs:58` keeps the `Global\` prefix, which is required for the documented cross-session singleton guarantee.

### 3.2 `PerUserAutostart` case-insensitive command comparison

Flagged as a possible defect: `GetState()` compares the registry command with `StringComparison.OrdinalIgnoreCase`, so a value differing only in letter casing reads as `Enabled` rather than `InvalidPath`.

Verified against the documented purpose (`docs/architecture/windows-platform-integration.md` §8: detect whether the portable executable was moved). Windows paths are case-insensitive at the filesystem level; different Windows APIs can report the same physical executable path with different letter casing across runs without the file having moved. An ordinal (case-sensitive) comparison would produce spurious `InvalidPath` states for an executable that never moved, which would be the actual defect.

**Outcome:** no code change. `src/FocusLedger.Windows/Autostart/PerUserAutostart.cs:45` keeps `OrdinalIgnoreCase`. `PerUserAutostartTests.CaseDifferingConfiguredCommandIsStillEnabled` locks in the intended behavior so it is not "fixed" into a regression later.

## 4. Documented non-gaps

Recorded so they are not re-flagged in a future review.

- **No `FocusLedger.App.Tests` project.** `AGENTS.md` §3 lists only `FocusLedger.Core.Tests`, `FocusLedger.Windows.Tests`, and `FocusLedger.Reporting.Tests` as the test-project structure. The composition root (`FocusLedger.App`) is exercised indirectly through `M1EndToEndScenarioTests.cs`. Adding a fourth test project would require amending the documented structure first.
