# Documentation Index

This directory is the normative technical specification for FocusLedger. Documents are divided by concern so that humans and coding agents can load only the context required for a task.

## Product

- [`product/requirements.md`](product/requirements.md) — scope, functional requirements, non-functional requirements, supported platform, and acceptance principles.
- [`product/privacy-and-data-policy.md`](product/privacy-and-data-policy.md) — prohibited data, transient data handling, persisted data rules, retention, and privacy threat model.

## Architecture

- [`architecture/overview.md`](architecture/overview.md) — solution structure, runtime topology, concurrency model, component boundaries, and failure isolation.
- [`architecture/runtime-state-machine.md`](architecture/runtime-state-machine.md) — tracker, presence, foreground, meeting, and lifecycle state transitions.
- [`architecture/windows-platform-integration.md`](architecture/windows-platform-integration.md) — foreground hooks, idle detection, session notifications, power events, tray integration, Core Audio, startup, and single-instance control.

## Data

- [`data/event-model.md`](data/event-model.md) — JSONL envelope, event types, payload contracts, ordering, rollover, recovery, and examples.
- [`data/configuration.md`](data/configuration.md) — configuration schema, defaults, validation, reload, migration, and a complete example.

## Features

- [`features/activity-classification.md`](features/activity-classification.md) — application identity, title parsing, privacy normalization, classification rules, productivity labels, and browser behavior.
- [`features/meeting-detection.md`](features/meeting-detection.md) — multi-signal meeting detection, provider adapters, confidence, debounce, and manual override.
- [`features/reporting.md`](features/reporting.md) — interval reconstruction, metrics, standalone HTML, range reports, and data-quality reporting.

## Engineering

- [`engineering/testing-and-quality.md`](engineering/testing-and-quality.md) — test strategy, performance targets, long-running tests, privacy tests, and fault injection.
- [`engineering/build-release-and-operations.md`](engineering/build-release-and-operations.md) — .NET publication, GitHub Actions, releases, signing policy, dependency governance, and runtime operations.

## Delivery

- [`roadmap.md`](roadmap.md) — milestones, feature IDs, dependencies, acceptance criteria, release gates, and deferred features.

## Architectural decisions

- [`adr/0001-single-process-tray-agent.md`](adr/0001-single-process-tray-agent.md)
- [`adr/0002-event-sourced-jsonl-storage.md`](adr/0002-event-sourced-jsonl-storage.md)
- [`adr/0003-balanced-privacy.md`](adr/0003-balanced-privacy.md)
- [`adr/0004-rule-based-classification.md`](adr/0004-rule-based-classification.md)
- [`adr/0005-single-file-self-contained-deployment.md`](adr/0005-single-file-self-contained-deployment.md)

## Document conventions

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD NOT**, and **MAY** are normative. Feature IDs from the roadmap are stable identifiers and should be used in issues, pull requests, tests, and release notes.
