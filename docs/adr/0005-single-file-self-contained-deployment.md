# ADR 0005: Self-Contained Single-File Windows Deployment

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

The application is intended for personal use and open-source GitHub releases. It should be easy to copy and run without an installer or installed .NET runtime.

## Decision

Publish the first stable release as a self-contained single-file `win-x64` executable targeting .NET 10. Store configuration and data in `%LocalAppData%\FocusLedger`, never beside the executable.

Allow documented runtime extraction of native components to `%TEMP%\.net` when required by .NET single-file behavior. Keep trimming and ReadyToRun disabled until measured and validated.

## Consequences

Positive:

- one user-facing executable;
- no installer or administrator rights;
- no separate .NET installation;
- easy GitHub release distribution.

Negative:

- larger executable;
- RID-specific release;
- native runtime components may be extracted temporarily;
- moved executables can invalidate the autostart registry path.

## Rejected alternatives

- Framework-dependent deployment: requires a compatible .NET runtime.
- MSI/MSIX installer: adds installation lifecycle and may conflict with the portable objective.
- Multi-file self-contained directory: operationally less convenient for the intended use.
