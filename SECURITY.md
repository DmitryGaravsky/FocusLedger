# Security Policy

## Supported versions

FocusLedger is under pre-1.0 development. Until a stable release is published, security fixes are made only on the current `main` branch. After 1.0, this section will list the supported release lines.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting from the repository **Security** tab. Include:

- the affected commit or release;
- the security or privacy impact;
- minimal reproduction steps;
- whether the issue can expose persisted or transient personal data;
- any suggested mitigation.

Do not include real personal information, raw window titles, URLs, local paths, credentials, tokens, activity files, or unsanitized logs. Use synthetic placeholders and the canary patterns documented in [`docs/engineering/testing-and-quality.md`](docs/engineering/testing-and-quality.md).

If private vulnerability reporting is temporarily unavailable, open a public issue containing only a request for a private reporting channel. Do not disclose vulnerability details in that issue.

The maintainer will acknowledge a private report when it is reviewed, validate the impact, and coordinate remediation and disclosure. Response times are best effort because FocusLedger is a personal open-source project and does not provide a commercial support SLA.

## Security scope

Security and privacy reports include:

- persistence of data prohibited by the privacy policy;
- access to another Windows user's session or data;
- named-pipe authorization bypass;
- unsafe HTML report generation;
- path traversal or deletion outside the FocusLedger storage root;
- unintended network communication;
- release artifact or dependency-chain compromise;
- denial of service caused by unbounded queues or external values.

General bugs and feature requests should use the public issue templates.

