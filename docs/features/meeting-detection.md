# Meeting Detection

**Status:** Approved baseline

## 1. Objective

Detect the start and end of a real-time call or meeting even when the meeting application is not foreground. Detection is probabilistic and combines independent evidence. No audio content is captured.

## 2. Supported providers for 1.0

- Microsoft Teams;
- Zoom;
- Google Meet in Google Chrome;
- Google Meet in Microsoft Edge;
- Slack Huddles;
- Cisco Webex.

## 3. Evidence model

Evidence codes are non-personal enumerations:

- `known-process`;
- `meeting-window`;
- `meeting-safe-context`;
- `active-audio-render-session`;
- `active-audio-capture-session`;
- `provider-call-state`;
- `manual-override`.

Possible scoring baseline:

| Evidence | Suggested weight |
|---|---:|
| known process running | 0.15 |
| provider meeting/call window | 0.45 |
| safe browser context identifies Google Meet | 0.45 |
| active audio render session for provider process | 0.20 |
| active audio capture session for provider process | 0.25 |
| explicit provider call-state adapter | 0.70 |
| manual override | 1.00 |

Weights are implementation guidance, not a frozen public contract. The detector should cap aggregate confidence at 1.0 and avoid double-counting equivalent signals.

## 4. State and hysteresis

Default thresholds:

- start confidence: 0.80;
- continue confidence: 0.60;
- start debounce: 10 seconds;
- end debounce: 15 seconds.

Hysteresis prevents rapid start/end flapping. A candidate is confirmed only after the start threshold remains satisfied for the full start debounce. A confirmed meeting ends only after confidence remains below the continuation threshold for the full end debounce.

## 5. Provider adapters

Each adapter returns:

```text
ProviderId
ObservedEvidence[]
Confidence
ObservationTimestamp
SafeOperationalStatus
```

Adapters must not return meeting names, participant names, email addresses, room names, or URLs for persistence.

### 5.1 Microsoft Teams

Use known process identity, window patterns transformed into safe evidence, and Core Audio process sessions. Support both current and legacy executable names when present.

### 5.2 Zoom

Use the Zoom process, known meeting-window state, and audio sessions. The mere presence of `zoom.exe` is insufficient.

### 5.3 Google Meet

Require a supported browser process plus a title classification that yields the constant safe context `web-meeting`, normally combined with audio evidence. No URL is required or stored.

### 5.4 Slack Huddles

Use Slack process identity, safe call-window evidence where reliable, and audio sessions.

### 5.5 Webex

Use known Webex process families, safe meeting-window evidence, and audio sessions.

## 6. Foreground independence

Confirmed meeting state remains active when the user switches to an IDE, document, or browser tab. Foreground activity continues to be classified normally. Reports provide both:

- meeting duration;
- foreground application/category during the meeting.

This enables metrics such as the percentage of meeting time spent in the meeting UI versus taking notes or reviewing code.

## 7. Presence interaction

- Lock, disconnect, or suspend normally ends observable meeting attribution unless provider evidence remains reliable and configuration explicitly permits continuation.
- Resume triggers immediate detector reconciliation.
- Idle state alone does not end a meeting; listening without input is common.
- Manual pause stops activity attribution but may still retain meeting operational state for a correct resume snapshot. Reports exclude paused duration according to tracker rules.

## 8. Manual override

Tray commands:

- **Start meeting manually**;
- **End meeting manually**.

Manual start records provider `manual` unless the current detector has a known provider. Manual override must be visible in event `source`/evidence and report data quality.

## 9. Persisted events

Persist only confirmed transitions:

- `meeting.started`;
- `meeting.context_changed` when provider changes without ending;
- `meeting.ended`.

Candidate observations remain in bounded in-memory state and optional privacy-safe diagnostics.

## 10. False-positive controls

The detector must not classify these as meetings solely by audio use:

- music or video playback;
- voice recording;
- gaming voice chat unless explicitly configured;
- browser media playback;
- an idle meeting application process with no call state.

## 11. Failure isolation

Audio or provider-adapter failures:

- do not stop foreground tracking;
- lower confidence or disable only the affected evidence source;
- use circuit breaker cooldown;
- write a safe diagnostic code;
- surface data quality in reports when meeting detection was unavailable for material periods.

## 12. Deferred capabilities

- distinguishing speaking from listening;
- participant or meeting-name tracking;
- calendar correlation;
- browser extension/native messaging;
- Discord, Telegram, WhatsApp, Skype, and Jitsi adapters;
- automatic correction of historical meeting intervals.
