using System.Text.Json;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;
using FocusLedger.Reporting.Reading;

namespace FocusLedger.Reporting.Tests;

public sealed class M1EndToEndScenarioTests {
    static readonly DateTimeOffset BeforeMidnight = new(2026, 7, 19, 23, 50, 0, TimeSpan.Zero);
    static readonly DateTimeOffset AtMidnight = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    static readonly PresenceState[] ExpectedPresenceStates = [
        PresenceState.Active,
        PresenceState.Idle,
        PresenceState.SessionLocked,
        PresenceState.Idle,
        PresenceState.SystemSuspended,
        PresenceState.Idle,
        PresenceState.Active
    ];
    [Test]
    public async Task SyntheticM1DayCrashAndRecoveryRemainOrderedAndPrivacySafe() {
        string rootPath = CreateRootPath();
        try {
            string dataPath = Path.Combine(rootPath, "data");
            string secondDayPath = ActivityFileNaming.GetFilePath(dataPath, new DateOnly(2026, 7, 20));
            await using(OperationalEventSession firstSession = CreateSession(rootPath)) {
                OperationalSessionInitialization initialization = await firstSession.InitializeAsync(CancellationToken.None);
                Assert.That(initialization.RecoveryRequired, Is.False);
                await using(DailyJsonlActivityEventWriter writer = new(dataPath, TimeSpan.FromHours(1), TimeProvider.System)) {
                    await AppendDayStartAsync(writer, firstSession, BeforeMidnight, "active");
                    await AppendForegroundAsync(writer, firstSession, BeforeMidnight, "visual-studio", "devenv.exe", "development-environment");
                    await AppendForegroundAsync(writer, firstSession, BeforeMidnight.AddMinutes(2), "chrome", "chrome.exe", "web-browser");
                    await AppendForegroundAsync(writer, firstSession, BeforeMidnight.AddMinutes(4), "windows-terminal", "windowsterminal.exe", "terminal");
                    AssertPresenceScenario();
                    await writer.AppendAsync(
                        new DayBoundaryActivityEvent(await firstSession.CreateEnvelopeAsync("day.ended", AtMidnight, "rollover", CancellationToken.None)),
                        CancellationToken.None);
                    await AppendDayStartAsync(writer, firstSession, AtMidnight, "idle");
                }
                EventEnvelope interruptedEnvelope = await firstSession.CreateEnvelopeAsync(
                    "heartbeat",
                    AtMidnight.AddSeconds(30),
                    "operational",
                    CancellationToken.None);
                await File.AppendAllTextAsync(secondDayPath, $"{{\"schemaVersion\":1,\"sequence\":{interruptedEnvelope.Sequence}");
            }
            IReadOnlyList<JsonlReadItem> secondDayItems = await ReadAllAsync(secondDayPath);
            Assert.Multiple(() => {
                Assert.That(secondDayItems[0], Is.TypeOf<JsonlEventReadItem>());
                Assert.That(secondDayItems[1], Is.TypeOf<JsonlEventReadItem>());
                Assert.That(((JsonlIssueReadItem)secondDayItems[2]).Kind, Is.EqualTo(JsonlDataQualityIssueKind.IncompleteTrailingLine));
            });
            await using OperationalEventSession recoveredSession = CreateSession(rootPath);
            OperationalSessionInitialization recovery = await recoveredSession.InitializeAsync(CancellationToken.None);
            OperationalActivitySignal recoverySignal = new(
                OperationalActivitySignalKind.RecoveredAfterUncleanShutdown,
                AtMidnight.AddMinutes(1),
                1);
            OperationalActivityEvent recoveryEvent = await recoveredSession.CreateEventAsync(recoverySignal, CancellationToken.None);
            using JsonDocument recoveryJson = JsonDocument.Parse(ActivityEventJsonSerializer.Serialize(recoveryEvent));
            Assert.Multiple(() => {
                Assert.That(recovery.RecoveryRequired, Is.True);
                Assert.That(recovery.NextSequence, Is.EqualTo(10));
                Assert.That(recoveryEvent.Envelope.Sequence, Is.EqualTo(10));
                Assert.That(recoveryJson.RootElement.TryGetProperty("duration", out _), Is.False);
                Assert.That(recoveryJson.RootElement.TryGetProperty("foreground", out _), Is.False);
                Assert.That(recoveryJson.RootElement.TryGetProperty("presence", out _), Is.False);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static async ValueTask AppendDayStartAsync(
        DailyJsonlActivityEventWriter writer,
        OperationalEventSession session,
        DateTimeOffset timestamp,
        string presence) {
        EventEnvelope dayStarted = await session.CreateEnvelopeAsync("day.started", timestamp, "rollover", CancellationToken.None);
        await writer.AppendAsync(new DayBoundaryActivityEvent(dayStarted), CancellationToken.None);
        EventEnvelope snapshotEnvelope = await session.CreateEnvelopeAsync("state.snapshot", timestamp, "rollover", CancellationToken.None);
        StateSnapshotActivityEvent snapshot = new(snapshotEnvelope, "running", presence, new MeetingSnapshotEventData("none"), null);
        await writer.AppendAsync(snapshot, CancellationToken.None);
    }
    static async ValueTask AppendForegroundAsync(
        DailyJsonlActivityEventWriter writer,
        OperationalEventSession session,
        DateTimeOffset timestamp,
        string applicationId,
        string processName,
        string family) {
        EventEnvelope envelope = await session.CreateEnvelopeAsync("foreground.changed", timestamp, "synthetic", CancellationToken.None);
        ForegroundActivityEvent activityEvent = new(
            envelope,
            "active",
            new ApplicationEventData(applicationId, processName, family),
            null,
            new ClassificationEventData("work.development", "productive", 1, "synthetic.safe", 1));
        await writer.AppendAsync(activityEvent, CancellationToken.None);
    }
    static void AssertPresenceScenario() {
        PresenceStateMachine stateMachine = new();
        PresenceState[] states = [
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Active, false, false, false)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, false, false, false)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, true, false, false)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, false, false, false)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, false, false, true)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Idle, false, false, false)).CurrentState,
            stateMachine.Apply(new PresenceConditions(PresenceActivityState.Active, false, false, false)).CurrentState
        ];
        Assert.That(states, Is.EqualTo(ExpectedPresenceStates));
    }
    static OperationalEventSession CreateSession(string rootPath) {
        return new OperationalEventSession(new OperationalStateStore(Path.Combine(rootPath, "state.json")), TimeZoneInfo.Utc);
    }
    static async Task<IReadOnlyList<JsonlReadItem>> ReadAllAsync(string filePath) {
        List<JsonlReadItem> items = [];
        await foreach(JsonlReadItem item in CrashTolerantJsonlReader.ReadAsync(filePath, CancellationToken.None))
            items.Add(item);
        return items;
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"m1-scenario-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
}
