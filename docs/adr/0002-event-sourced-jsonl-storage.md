# ADR 0002: Append-Only JSONL Event Storage

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

Activity must be durable, inspectable, easy to append, resilient to crashes, and suitable for regenerating reports as analysis evolves.

## Decision

Persist semantic state transitions as one JSON object per line in a daily JSONL file. Use one writer, append-only operation, periodic flush, critical-transition flush, and a crash-tolerant reader.

Reports reconstruct intervals from events rather than relying on precomputed mutable summaries.

## Consequences

Positive:

- simple atomic append behavior;
- human-inspectable local files;
- report logic can evolve without recollecting data;
- partial final writes are easy to identify;
- daily files are independently portable.

Negative:

- interval reconstruction is more complex;
- schema compatibility becomes a public contract;
- corrections require future compensating events rather than in-place editing;
- report readers must handle gaps and duplicates.

## Rejected alternatives

- SQLite: robust but less transparent, introduces mutable schema and a larger native/runtime surface for the first release.
- One JSON document per day: expensive and fragile for continuous append.
- Direct HTML summaries only: loses source events and prevents improved future analysis.
