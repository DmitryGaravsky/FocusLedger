# ADR 0004: Deterministic Rule-Based Classification

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

The tracker must classify applications and contexts locally, explain decisions, avoid network services, and let the user customize behavior.

## Decision

Use an ordered deterministic rule engine with built-in application adapters and user-editable JSON configuration. Each result includes a stable rule ID, category, safe context, and confidence.

No machine-learning model or remote classification service is used in the first stable release.

## Consequences

Positive:

- local-only and deterministic;
- explainable through rule IDs;
- easy to test and version;
- predictable privacy behavior;
- user customization without retraining.

Negative:

- rules require maintenance as application captions change;
- unknown activity can remain unclassified;
- complex regex must be constrained for performance and safety.

## Rejected alternatives

- Cloud classification: violates the no-network and privacy requirements.
- Local LLM/model: excessive footprint and uncertainty for a tray utility.
- Process-name-only classification: insufficient for browser and multi-purpose applications.
