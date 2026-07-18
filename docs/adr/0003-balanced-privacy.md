# ADR 0003: Balanced Privacy as the Default

- **Status:** Accepted
- **Date:** 2026-07-18

## Context

Useful classification may require reading a top-level window title, but raw titles often contain document names, customer names, email addresses, paths, meeting participants, or URLs.

## Decision

Use Balanced Privacy by default:

- raw titles may be inspected transiently in memory;
- raw titles, URLs, paths, meeting names, and user identifiers are not persisted;
- parsers and rules may emit only constant or allowlisted safe context labels;
- a final privacy validator rejects unsafe event values;
- diagnostics follow an even stricter structured-data policy.

Strict mode is also supported. Detailed raw-title persistence is outside the 1.0 baseline.

## Consequences

Positive:

- useful category and context reporting without building a detailed personal-content archive;
- lower impact if files are exposed;
- clear, testable privacy boundary.

Negative:

- reports cannot show exact historical document/tab names;
- user rules must map patterns to constants;
- some unknown browser activity remains unclassified.

## Rejected alternatives

- Persist all titles and redact later: violates data minimization and makes accidental leakage likely.
- Never read titles: substantially reduces classification quality, especially for browsers.
- Hash raw titles: hashes remain stable identifiers and can leak repetition or be dictionary-attacked; they are not safe context labels.
