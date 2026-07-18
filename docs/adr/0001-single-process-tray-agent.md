# ADR 0001: Single-Process Tray Agent with No Main Window

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

The application must run without a visible main UI while providing a system-tray icon, menu commands, foreground hooks, session notifications, and power-message handling.

## Decision

Use one per-user `WinExe` process targeting `net10.0-windows`. Run a Windows Forms `ApplicationContext` and message loop without a main form. Host `NotifyIcon`, the context menu, and a hidden native/message-only window in the same process.

Enforce one process per Windows user and route secondary CLI commands to the primary process through a same-user named pipe.

## Consequences

Positive:

- simple portable deployment;
- direct access to user-session Windows events;
- no service installation or elevation;
- tray and collectors share one lifecycle.

Negative:

- Windows Forms is present as infrastructure even though there is no main GUI;
- the message-loop thread must be carefully protected from blocking operations;
- a process crash affects all subsystems, requiring flush/recovery design.

## Rejected alternatives

- Windows service: wrong session boundary, requires installation/elevation, complicates active-window access.
- Separate tray UI and background service: unnecessary deployment and IPC complexity for personal use.
- Console application: produces an unwanted console and still needs a message loop for reliable tray/system integration.
