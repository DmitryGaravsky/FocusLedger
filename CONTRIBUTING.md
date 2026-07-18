# Contributing to FocusLedger

Thank you for helping improve FocusLedger. Contributions must preserve the project's local-only, privacy-first design and follow the specifications under [`docs/`](docs/README.md).

## Before contributing

- Read [`AGENTS.md`](AGENTS.md) and the normative documents it references.
- Search existing issues before opening a new one.
- Use the relevant roadmap feature ID from [`docs/roadmap.md`](docs/roadmap.md) when one exists.
- Keep proposals within the documented 1.0 scope. Discuss deferred or architecture-changing work in an issue before implementation.
- Do not include raw window titles, URLs, document names, user paths, usernames, machine names, email addresses, credentials, or other personal data in issues, pull requests, logs, screenshots, fixtures, or test artifacts.

## Development environment

Development requires:

- Windows 10 22H2 or Windows 11;
- the .NET 10 SDK selected by [`global.json`](global.json);
- Git with CRLF checkout behavior compatible with the repository policy.

Restore, build, and test the solution with:

```powershell
dotnet restore FocusLedger.slnx
dotnet build FocusLedger.slnx --configuration Release --no-restore
dotnet test FocusLedger.slnx --configuration Release --no-build
```

All test projects use NUnit, and all NuGet versions are managed centrally through [`Directory.Packages.props`](Directory.Packages.props).

## Making a change

1. Create a focused branch from the current `main` branch.
2. Add or update acceptance tests before completing behavioral changes.
3. Add privacy and failure-path tests whenever the affected feature handles external values, persistence, diagnostics, configuration, or reports.
4. Update the relevant documentation and roadmap item in the same change.
5. Keep commits focused and use clear English commit messages.
6. Run the final validation commands before opening a pull request.

The required C# style rules are defined in [`AGENTS.md`](AGENTS.md) and enforced by repository policy checks. In particular, use ordinary line comments only, omit redundant `private` modifiers, avoid expression-bodied implementations, and do not leave blank lines between methods or inside method bodies.

## Final validation

After the final code edit, run:

```powershell
dotnet format FocusLedger.slnx --no-restore
dotnet format FocusLedger.slnx --no-restore --verify-no-changes
dotnet build FocusLedger.slnx --configuration Release --no-restore
dotnet test FocusLedger.slnx --configuration Release --no-build
pwsh ./eng/verify-repository.ps1
```

Verify that changed text files use CRLF line endings. The pull-request workflow repeats the policy, format, build, and test checks.

## Pull requests

A pull request must:

- explain the problem and the chosen solution;
- reference its issue and roadmap feature ID where applicable;
- describe testing and failure behavior;
- state the privacy and data-contract impact;
- include documentation changes for user-visible or architectural behavior;
- avoid unrelated refactoring;
- pass CI without warnings.

Maintainers may request changes when a contribution weakens privacy, compatibility, graceful degradation, or the no-network invariant even if its tests pass.

## Reporting security issues

Do not report vulnerabilities or suspected privacy leaks in a public issue. Follow [`SECURITY.md`](SECURITY.md) instead.

