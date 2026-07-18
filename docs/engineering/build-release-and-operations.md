# Build, Release, and Operations

**Status:** Approved baseline

## 1. Build target

Primary project settings:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <OutputType>WinExe</OutputType>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <DebugType>embedded</DebugType>
    <PublishTrimmed>false</PublishTrimmed>
    <PublishReadyToRun>false</PublishReadyToRun>
  </PropertyGroup>
</Project>
```

Trimming and ReadyToRun remain disabled for the initial release until Windows Forms, COM, P/Invoke, source-generated serialization, and startup-size behavior are measured and tested.

Shared build policy is defined in the repository-level `Directory.Build.props`. Every project inherits:

- nullable reference types and implicit usings;
- warnings as errors;
- .NET analyzers at the latest recommended analysis level;
- build-time code-style enforcement;
- deterministic build settings;
- product, company, repository, license, language, and semantic-version metadata.

The initial shared version is `0.1.0`. Package versions are defined only in `Directory.Packages.props` through Central Package Management.

Project and package attribution use the public GitHub identity [Dmitrii Garavskii](https://github.com/DmitryGaravsky). No email address or employer is inferred when the corresponding profile field is not public.

## 2. Artifact model

The user-facing release contains one executable for `win-x64`. Documentation must state that the self-contained runtime may extract native components to `%TEMP%\.net` during execution.

Release assets should include:

- `FocusLedger-win-x64.exe`;
- SHA-256 checksum file;
- SBOM in CycloneDX JSON;
- release notes;
- optional detached signature if signing is configured.

## 3. GitHub repository conventions

Recommended branches and automation:

- protected `main`;
- pull requests required;
- conventional or clearly categorized commit messages;
- issues and PRs reference roadmap feature IDs;
- Dependabot or Renovate for dependency PRs, configured to avoid automatic merges without tests;
- CodeQL for C#;
- secret scanning;
- dependency review;
- release workflow triggered by signed/versioned tag.

## 4. CI workflow

Pull-request CI:

1. restore with locked dependencies when lock files are introduced;
2. build Release;
3. run unit tests;
4. run reporting golden tests;
5. run privacy canary tests;
6. run static analyzers and formatting check;
7. build `win-x64` single-file artifact;
8. smoke-test artifact startup/CLI on a Windows runner where feasible;
9. generate SBOM;
10. upload test and build artifacts.

Scheduled CI:

- extended Windows integration tests;
- endurance smoke test;
- dependency/security scan;
- report fixture compatibility.

## 5. Versioning

Use semantic versioning:

- `0.x`: contracts may evolve with documented migration;
- `1.0.0`: stable event schema/configuration compatibility commitment begins;
- patch: fixes with compatible data contracts;
- minor: additive features and compatible schema additions;
- major: intentional breaking changes with migration documentation.

The executable version, report generator version, and event writer version should be available in diagnostics and lifecycle events without including machine/user data.

## 6. Open-source dependency policy

Every dependency must:

- have an OSI-compatible license compatible with MIT distribution;
- have a documented purpose;
- be actively maintained or intentionally pinned with rationale;
- not add telemetry or network behavior;
- not require redistribution of prohibited assets;
- be represented in the SBOM.

Prefer BCL and Windows APIs over large dependencies for tray, JSON, logging, templating, and HTML generation. Avoid a templating dependency unless it materially improves safety and maintenance.

## 7. Code signing

Code signing is desirable but not required for the first public preview. The project must not claim the binary is signed unless the release workflow verifies the signature. If signing is later added, secrets reside only in GitHub Environments or an external signing service and are never available to pull-request workflows from forks.

## 8. Release process

A stable release requires:

- all milestone features complete;
- release gates in `docs/roadmap.md` satisfied;
- privacy review passed;
- Windows 10/11 manual smoke tests passed;
- checksum and SBOM generated;
- no high/critical known dependency vulnerabilities without documented risk acceptance;
- documentation synchronized;
- changelog/release notes describing schema/config changes;
- portable executable tested from a clean directory and user profile.

## 9. Operational directories

Default directories:

```text
%LocalAppData%\FocusLedger\data
%LocalAppData%\FocusLedger\reports
%LocalAppData%\FocusLedger\logs
```

The program may create directories lazily. Failure to create/write the data directory is fatal to collection. Failure to create a report is non-fatal. Failure to write diagnostics must not recursively generate more logging failures.

## 10. Retention maintenance

Once per day after startup or rollover:

- delete activity files older than 365 days;
- delete diagnostics older than 14 days;
- leave reports unchanged by default;
- operate only within the resolved FocusLedger storage root;
- never follow links/reparse points outside the root;
- log only safe counts and dates.

## 11. No automatic update mechanism

The 1.0 application does not check GitHub or any other service for updates. Users obtain updates manually from GitHub Releases. This preserves the no-network invariant.
