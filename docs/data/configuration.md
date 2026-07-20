# Configuration Specification

**Status:** Normative schema version 1 baseline
**Path:** `%LocalAppData%\FocusLedger\config.json`

## 1. Principles

Configuration is user-editable JSON. It is treated as untrusted local input. The application reads, validates, compiles, and atomically activates an immutable configuration snapshot.

The executable contains a built-in default configuration. On first start, it writes a complete user configuration only if no file exists.

## 2. Top-level sections

```text
schemaVersion
tracking
privacy
storage
reporting
startup
categories
applications
titleParsers
classificationRules
meetingDetectors
diagnostics
```

## 3. Complete default example

```json
{
  "schemaVersion": 1,
  "tracking": {
    "idleThresholdSeconds": 300,
    "foregroundReconciliationIntervalMilliseconds": 1000,
    "idleSamplingIntervalMilliseconds": 1000,
    "eventFlushIntervalMilliseconds": 2000,
    "heartbeatIntervalSeconds": 60,
    "persistManualPause": true,
    "workingSchedule": {
      "enabled": false,
      "timeZone": "Europe/Madrid",
      "days": ["monday", "tuesday", "wednesday", "thursday", "friday"],
      "start": "09:00",
      "end": "18:00"
    }
  },
  "privacy": {
    "mode": "balanced",
    "persistRawWindowTitles": false,
    "persistUrls": false,
    "persistExecutablePaths": false,
    "persistMeetingTitles": false,
    "maximumSafeContextLength": 64,
    "rejectEmailAddresses": true,
    "rejectWindowsPaths": true,
    "rejectUrls": true,
    "rejectIpAddresses": true,
    "rejectLongNumericIdentifiers": true
  },
  "storage": {
    "rootDirectory": "%LocalAppData%\\FocusLedger",
    "activityRetentionDays": 365,
    "reportRetentionDays": 0,
    "createYearMonthDirectories": true,
    "allowConcurrentReportReads": true
  },
  "reporting": {
    "openAfterInteractiveGeneration": true,
    "includeTimeline": true,
    "includeApplications": true,
    "includeSafeContexts": true,
    "includeFocusMetrics": true,
    "includeMeetingMetrics": true,
    "includeLostTimeMetrics": true,
    "includeDataQuality": true,
    "minimumDisplayedIntervalSeconds": 5,
    "focusSessionMinimumSeconds": 600,
    "focusSessionMaximumNeutralGapSeconds": 60
  },
  "startup": {
    "enableAutomatically": false,
    "registryValueName": "FocusLedger",
    "arguments": "--autostart"
  },
  "categories": [
    {"id":"work.development","displayName":"Development","disposition":"productive","weight":1.0},
    {"id":"work.code-review","displayName":"Code Review","disposition":"productive","weight":1.0},
    {"id":"work.research","displayName":"Research","disposition":"productive","weight":1.0},
    {"id":"work.documentation","displayName":"Documentation","disposition":"productive","weight":1.0},
    {"id":"work.communication","displayName":"Work Communication","disposition":"productive","weight":0.9},
    {"id":"work.meeting","displayName":"Meeting","disposition":"productive","weight":1.0},
    {"id":"work.administration","displayName":"Work Administration","disposition":"neutral","weight":0.6},
    {"id":"work.planning","displayName":"Planning","disposition":"productive","weight":1.0},
    {"id":"work.management","displayName":"Management","disposition":"productive","weight":0.9},
    {"id":"work.mentoring","displayName":"Mentoring","disposition":"productive","weight":1.0},
    {"id":"work.security","displayName":"Security Work","disposition":"productive","weight":1.0},
    {"id":"learning.technical","displayName":"Technical Learning","disposition":"productive","weight":0.9},
    {"id":"learning.language","displayName":"Language Learning","disposition":"productive","weight":0.9},
    {"id":"personal.communication","displayName":"Personal Communication","disposition":"neutral","weight":0.3},
    {"id":"personal.administration","displayName":"Personal Administration","disposition":"neutral","weight":0.4},
    {"id":"personal.entertainment","displayName":"Personal Entertainment","disposition":"neutral","weight":0.0},
    {"id":"distraction.social","displayName":"Social Media","disposition":"unproductive","weight":0.0},
    {"id":"distraction.video","displayName":"Entertainment Video","disposition":"unproductive","weight":0.0},
    {"id":"distraction.news","displayName":"News Browsing","disposition":"unproductive","weight":0.0},
    {"id":"distraction.games","displayName":"Games","disposition":"unproductive","weight":0.0},
    {"id":"system","displayName":"System","disposition":"excluded","weight":0.0},
    {"id":"idle","displayName":"Idle","disposition":"excluded","weight":0.0},
    {"id":"locked","displayName":"Locked","disposition":"excluded","weight":0.0},
    {"id":"paused","displayName":"Paused","disposition":"excluded","weight":0.0},
    {"id":"unclassified","displayName":"Unclassified","disposition":"neutral","weight":0.0}
  ],
  "applications": [
    {"id":"visual-studio","processNames":["devenv.exe"],"family":"development-environment","defaultCategory":"work.development"},
    {"id":"visual-studio-code","processNames":["code.exe"],"family":"development-environment","defaultCategory":"work.development"},
    {"id":"rider","processNames":["rider64.exe"],"family":"development-environment","defaultCategory":"work.development"},
    {"id":"windows-terminal","processNames":["windowsterminal.exe"],"family":"terminal","defaultCategory":"work.development"},
    {"id":"powershell","processNames":["powershell.exe","pwsh.exe"],"family":"terminal","defaultCategory":"work.development"},
    {"id":"google-chrome","processNames":["chrome.exe"],"family":"browser","defaultCategory":"unclassified","titleParser":"chromium-browser"},
    {"id":"microsoft-edge","processNames":["msedge.exe"],"family":"browser","defaultCategory":"unclassified","titleParser":"chromium-browser"},
    {"id":"mozilla-firefox","processNames":["firefox.exe"],"family":"browser","defaultCategory":"unclassified","titleParser":"firefox-browser"},
    {"id":"microsoft-teams","processNames":["ms-teams.exe","teams.exe"],"family":"communication","defaultCategory":"work.communication"},
    {"id":"zoom","processNames":["zoom.exe"],"family":"communication","defaultCategory":"work.communication"},
    {"id":"slack","processNames":["slack.exe"],"family":"communication","defaultCategory":"work.communication"},
    {"id":"outlook","processNames":["outlook.exe","olk.exe"],"family":"email","defaultCategory":"work.communication"},
    {"id":"word","processNames":["winword.exe"],"family":"office","defaultCategory":"work.documentation"},
    {"id":"excel","processNames":["excel.exe"],"family":"office","defaultCategory":"work.administration"},
    {"id":"powerpoint","processNames":["powerpnt.exe"],"family":"office","defaultCategory":"work.documentation"},
    {"id":"adobe-acrobat","processNames":["acrobat.exe","acrord32.exe"],"family":"document-reader","defaultCategory":"work.documentation"},
    {"id":"file-explorer","processNames":["explorer.exe"],"family":"system-shell","defaultCategory":"system"},
    {"id":"notepad","processNames":["notepad.exe"],"family":"text-editor","defaultCategory":"work.documentation"}
  ],
  "titleParsers": [
    {"id":"chromium-browser","type":"known-browser-suffix","suffixes":[" - Google Chrome"," - Microsoft Edge"],"emitSafeLabelsOnly":true},
    {"id":"firefox-browser","type":"known-browser-suffix","suffixes":[" — Mozilla Firefox"," - Mozilla Firefox"],"emitSafeLabelsOnly":true}
  ],
  "classificationRules": [
    {"id":"builtin.github.pull-request","priority":1000,"enabled":true,"applicationFamilies":["browser"],"titlePattern":"(?i)pull request|github","category":"work.code-review","safeContext":"pull-request"},
    {"id":"builtin.microsoft-learn","priority":950,"enabled":true,"applicationFamilies":["browser"],"titlePattern":"(?i)microsoft learn|\\.net documentation","category":"learning.technical","safeContext":"technical-documentation"},
    {"id":"builtin.google-meet","priority":940,"enabled":true,"applicationFamilies":["browser"],"titlePattern":"(?i)google meet|meet -","category":"work.meeting","safeContext":"web-meeting"},
    {"id":"builtin.youtube","priority":500,"enabled":true,"applicationFamilies":["browser"],"titlePattern":"(?i)youtube","category":"distraction.video","safeContext":"youtube"},
    {"id":"builtin.development-environment","priority":100,"enabled":true,"applicationFamilies":["development-environment","terminal"],"category":"work.development","safeContext":"source-code"}
  ],
  "meetingDetectors": [
    {"id":"microsoft-teams","enabled":true,"provider":"microsoft-teams","processNames":["ms-teams.exe","teams.exe"],"startConfidence":0.8,"continueConfidence":0.6,"startDebounceSeconds":10,"endDebounceSeconds":15,"useAudioEvidence":true,"persistTitle":false},
    {"id":"zoom","enabled":true,"provider":"zoom","processNames":["zoom.exe"],"startConfidence":0.8,"continueConfidence":0.6,"startDebounceSeconds":10,"endDebounceSeconds":15,"useAudioEvidence":true,"persistTitle":false},
    {"id":"google-meet","enabled":true,"provider":"google-meet","processNames":["chrome.exe","msedge.exe"],"requiredSafeContext":"web-meeting","startConfidence":0.8,"continueConfidence":0.6,"startDebounceSeconds":10,"endDebounceSeconds":15,"useAudioEvidence":true,"persistTitle":false},
    {"id":"slack-huddles","enabled":true,"provider":"slack-huddles","processNames":["slack.exe"],"startConfidence":0.85,"continueConfidence":0.65,"startDebounceSeconds":10,"endDebounceSeconds":15,"useAudioEvidence":true,"persistTitle":false},
    {"id":"webex","enabled":true,"provider":"webex","processNames":["webex.exe","ciscocollabhost.exe"],"startConfidence":0.8,"continueConfidence":0.6,"startDebounceSeconds":10,"endDebounceSeconds":15,"useAudioEvidence":true,"persistTitle":false}
  ],
  "diagnostics": {
    "enabled": true,
    "minimumLevel": "information",
    "retentionDays": 14,
    "includeStackTraces": true,
    "includeRawWindowTitles": false,
    "includeExecutablePaths": false,
    "includeConfigurationContent": false
  }
}
```

When `createYearMonthDirectories` is enabled, daily activity files use one combined calendar folder such as `data/2026-07/activity-2026-07-18.jsonl`. The setting does not create separate year and month directory levels.

## 4. Validation

Validation failures include:

- unsupported schema version;
- duplicate category, application, parser, rule, or detector IDs;
- reference to a missing category/parser/application;
- invalid regex;
- out-of-range confidence, weight, interval, or retention values;
- `persistRawWindowTitles=true` in Balanced or Strict mode;
- URL persistence enabled in Balanced or Strict mode;
- unsafe or absolute storage path outside permitted user-local policy when such restriction is enabled;
- invalid working-schedule time zone or interval;
- invalid process name format;
- unsafe `safeContext` value.

Validation errors are reported using JSON paths and safe fixed messages. The offending raw value is not copied into diagnostics when it may contain personal data.

`ConfigurationValidator` returns at most 256 immutable errors. Each error contains only a JSON path, an enumerated code, and a fixed message. Validation covers schema support, identifiers, references, privacy switches, regular expressions, numeric ranges, working schedules, process names, safe contexts, meeting thresholds, and diagnostic privacy settings.

## 5. Hot reload

- Watch the file with `FileSystemWatcher`.
- Debounce changes for 500 ms.
- Read with retry to tolerate editor replace/write behavior.
- Deserialize and validate off the message-loop thread.
- Compile regex and lookups before activation.
- Replace the active immutable snapshot atomically.
- Keep the previous snapshot on failure.
- Emit `configuration.reloaded` or `configuration.reload_failed`.

## 6. Atomic writes

When FocusLedger writes configuration or migration output:

1. write a complete temporary file in the same directory;
2. flush it;
3. replace/rename atomically;
4. preserve a single backup for migration failures when appropriate.

## 7. Migration

Configuration schema changes require explicit migrations. The application must never silently discard unknown user rules. Before modifying a user file, create a backup and record the old/new schema versions in a safe diagnostic event.

## 8. Naming baseline fixture

`tests/FocusLedger.Core.Tests/Fixtures/Compatibility/schema-1-configuration-names.json` freezes the schema 1 section and property names. It is a compact naming fixture rather than the complete built-in default configuration. The complete default remains normative in section 3 and will be covered by serialization fixtures when `CFG-001` is implemented.

Renaming or removing a schema 1 property requires a schema-version increment and documented migration. Backward-compatible optional additions do not require a version increment, but readers must continue to accept this fixture.

The implementation represents collection-valued sections with `ImmutableArray<T>` and exposes the complete built-in baseline through `BuiltInConfiguration.CreateDefault()`. Serialization and loading use the source-generated `ConfigurationJsonContext`; reflection-based serialization is not part of the configuration boundary.
