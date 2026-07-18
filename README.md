# FocusLedger

FocusLedger is a privacy-first Windows activity tracker built on .NET 10. It runs as a portable, self-contained, single-file tray application without a main window. The application records application-focus, presence, session, power, pause, and meeting events to daily JSON Lines files and generates standalone HTML reports.

This repository is intended to be published as open source under the MIT License. All source code, documentation, configuration keys, event names, log messages, and release notes are written in English.

## Product goals

- Track which application is active and how long it remains in focus.
- Account for user idle time, workstation lock, session disconnection, sleep, resume, and manual pause.
- Classify activity into configurable categories such as development, code review, communication, meetings, learning, administration, and distraction.
- Detect meeting start and end using multiple signals rather than relying only on the foreground window.
- Store an append-only, privacy-normalized event stream in one JSONL file per local calendar day.
- Generate a self-contained HTML report for a day or date range.
- Operate locally without network communication, administrator rights, an installer, or a separately installed .NET runtime.
- Provide a system tray icon and menu for status, pause/resume, report generation, configuration access, startup registration, and exit.

## Non-goals

FocusLedger is not an employee-monitoring, surveillance, parental-control, keylogging, screenshot, audio-recording, or browser-history product. It does not collect keystrokes, typed text, clipboard contents, screenshots, OCR output, document contents, audio, camera data, network traffic, geolocation, browser history, or data from other Windows users.

## Target platform

- Windows 10 22H2 and Windows 11
- x64 for the first stable release
- `net10.0-windows`
- self-contained, single-file `win-x64` publication
- no administrative privileges
- per-user data under `%LocalAppData%\FocusLedger`

## Documentation map

Start with [`docs/README.md`](docs/README.md). Coding agents must also read [`AGENTS.md`](AGENTS.md) before changing the repository.

The primary specification documents are:

- [`docs/product/requirements.md`](docs/product/requirements.md)
- [`docs/product/privacy-and-data-policy.md`](docs/product/privacy-and-data-policy.md)
- [`docs/architecture/overview.md`](docs/architecture/overview.md)
- [`docs/data/event-model.md`](docs/data/event-model.md)
- [`docs/data/configuration.md`](docs/data/configuration.md)
- [`docs/roadmap.md`](docs/roadmap.md)

## Product identity

`FocusLedger` is the repository, executable, product, data-directory, and namespace prefix. Any future rename must be handled as an explicit migration because it affects autostart registration, named mutexes, named pipes, configuration locations, and report metadata.

## License

The project is planned for release under the MIT License. Third-party dependencies must be compatible with MIT distribution and documented in release artifacts.

FocusLedger is created and maintained by [Dmitrii Garavskii](https://github.com/DmitryGaravsky).
