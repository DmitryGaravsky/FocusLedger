# Activity Classification

**Status:** Approved baseline

## 1. Objective

Classification converts transient foreground observations into privacy-safe, stable categories suitable for reports. It must remain deterministic, configurable, and explainable through rule IDs.

## 2. Input model

Transient input may contain:

- process name;
- executable metadata;
- package identity;
- top-level window class;
- raw top-level caption;
- known adapter output;
- current meeting state.

Only normalized application identity, safe context, category, rule ID, and confidence may be persisted.

## 3. Application identity resolution

Resolution order:

1. exact configured process-name mapping;
2. exact package/application identity mapping;
3. known built-in product mapping;
4. normalized executable file name;
5. `unknown-application`.

Application IDs are lowercase kebab-case. A process path is never used as an application ID.

## 4. Title parsing

### 4.1 General rules

A parser may use a raw title to determine structure but must return a structured result:

```text
ContextKind
SafeLabel
ClassificationHints
PrivacyDecision
Confidence
```

It must not return a raw remainder as a safe label by default.

### 4.2 Browser parsing

Chrome, Edge, and Firefox commonly expose the active tab title in the top-level caption. The 1.0 parser:

- removes a known browser suffix;
- applies classification patterns in memory;
- emits only constant safe labels from matched rules;
- does not persist the tab title;
- does not persist a URL;
- does not enumerate background tabs;
- does not distinguish browser profiles or private mode.

### 4.3 IDE parsing

IDE titles may expose solution, repository, branch, or file names. Built-in behavior should classify the activity as `source-code` without persisting those names. User rules may map a title pattern to a constant project label such as `product-development` or `security-research`.

### 4.4 Office and document applications

Document names are not persisted. Word, PowerPoint, Acrobat, and Notepad map to safe functional contexts. Excel defaults to administration but can be reclassified through rules.

## 5. Rule engine

Rules are evaluated in descending priority. A rule may match:

- application ID;
- application family;
- process name;
- window class;
- title regex evaluated transiently;
- safe parser hint;
- active meeting provider;
- optional time/schedule conditions in a future version.

A rule outputs:

- category ID;
- constant safe context label;
- optional productivity override;
- confidence;
- terminal/non-terminal behavior.

Default evaluation precedence:

1. manual override;
2. confirmed meeting override;
3. application-specific adapter rule;
4. explicit application-ID rule;
5. application-family/title rule;
6. application default category;
7. `unclassified`.

## 6. Explainability

Every persisted classification includes a stable `ruleId`. Reports may expose the rule ID in a data-quality/debug section. A future UI can use it to create or refine rules without storing raw historical titles.

## 7. Productivity semantics

Categories have:

- `disposition`: productive, neutral, unproductive, or excluded;
- `weight`: 0.0 through 1.0.

Rules:

- `unclassified` is neutral by default;
- idle, locked, paused, disconnected, and suspended time are excluded from active-productivity percentages;
- meeting time is productive by default but remains separately visible;
- outside-schedule time is not automatically unproductive;
- input frequency is never used as a productivity score.

## 8. Context switches and focus

A context switch occurs when the effective report category or safe context changes while tracking is Running and presence is Active. Repeated events within a configurable merge window may be collapsed during reporting.

A focus session is a continuous productive interval meeting the report threshold, default 10 minutes, allowing only configured neutral gaps of at most 60 seconds.

## 9. Built-in classification intentions

| Application/family | Default category | Safe context |
|---|---|---|
| Visual Studio / VS Code / Rider | `work.development` | `source-code` |
| Windows Terminal / PowerShell | `work.development` | `terminal` or `source-code` |
| Teams / Slack / Outlook | `work.communication` | provider-specific constant |
| Confirmed meeting | `work.meeting` | `meeting` |
| Word / PowerPoint / Acrobat | `work.documentation` | `document-work` |
| Excel | `work.administration` | `spreadsheet` |
| File Explorer | `system` | `file-management` |
| Unknown browser page | `unclassified` | omitted |

## 10. Privacy failure behavior

If a parser cannot prove that an output label is safe, it returns no label. Classification may still select a category using the transient match. The privacy validator is the final gate before event creation and must reject unsafe output even when a parser or user rule is defective.

## 11. Performance

- Cache process-to-application mapping by process identity with bounded lifetime.
- Cache compiled regex in the immutable configuration snapshot.
- Avoid re-reading static process metadata on every title change.
- Limit title length before regex processing.
- Set regex timeouts.
- Use circuit breakers for adapters that cross process boundaries.
