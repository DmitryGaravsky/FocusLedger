using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Coordination;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;
using FocusLedger.Core.State;

namespace FocusLedger.Core.Tests;

public sealed class ManualPauseControllerTests {
    static readonly DateTimeOffset PauseTime = new(2026, 7, 20, 8, 15, 0, TimeSpan.Zero);
    static readonly string[] ExpectedEventTypes = ["tracking.paused", "tracking.resumed"];
    static readonly long[] ExpectedSequences = [1, 2];
    [Test]
    public async Task PauseIsPersistedFlushedAndRestoredByNextRun() {
        string rootPath = CreateRootPath();
        try {
            RecordingEventWriter writer = new();
            await using(OperationalEventSession firstSession = CreateSession(rootPath)) {
                await using ManualPauseController controller = new(firstSession, writer);
                ManualPauseInitialization initialization = await controller.InitializeAsync(CancellationToken.None);
                TrackerLifecycleTransition transition = await controller.SetPausedAsync(true, PauseTime, CancellationToken.None);
                Assert.Multiple(() => {
                    Assert.That(initialization.State, Is.EqualTo(TrackerLifecycleState.Running));
                    Assert.That(transition.CurrentState, Is.EqualTo(TrackerLifecycleState.Paused));
                    Assert.That(controller.State, Is.EqualTo(TrackerLifecycleState.Paused));
                    Assert.That(writer.FlushCount, Is.EqualTo(1));
                });
            }
            await using OperationalEventSession secondSession = CreateSession(rootPath);
            OperationalSessionInitialization restored = await secondSession.InitializeAsync(CancellationToken.None);
            Assert.That(restored.ManualPause, Is.True);
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task PauseAndResumeEmitCanonicalManualEvents() {
        string rootPath = CreateRootPath();
        try {
            RecordingEventWriter writer = new();
            await using OperationalEventSession session = CreateSession(rootPath);
            await using ManualPauseController controller = new(session, writer);
            await controller.InitializeAsync(CancellationToken.None);
            await controller.SetPausedAsync(true, PauseTime, CancellationToken.None);
            await controller.SetPausedAsync(false, PauseTime.AddMinutes(5), CancellationToken.None);
            TrackingControlActivityEvent[] events = writer.Events.Cast<TrackingControlActivityEvent>().ToArray();
            Assert.Multiple(() => {
                Assert.That(events.Select(activityEvent => activityEvent.Type), Is.EqualTo(ExpectedEventTypes));
                Assert.That(events.Select(activityEvent => activityEvent.Sequence), Is.EqualTo(ExpectedSequences));
                Assert.That(events, Has.All.Property(nameof(TrackingControlActivityEvent.Source)).EqualTo("manual"));
                Assert.That(writer.FlushCount, Is.EqualTo(2));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task RepeatedCommandIsIdempotent() {
        string rootPath = CreateRootPath();
        try {
            RecordingEventWriter writer = new();
            await using OperationalEventSession session = CreateSession(rootPath);
            await using ManualPauseController controller = new(session, writer);
            await controller.InitializeAsync(CancellationToken.None);
            await controller.SetPausedAsync(true, PauseTime, CancellationToken.None);
            TrackerLifecycleTransition repeated = await controller.SetPausedAsync(true, PauseTime.AddSeconds(1), CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(repeated.Changed, Is.False);
                Assert.That(writer.Events, Has.Count.EqualTo(1));
                Assert.That(writer.FlushCount, Is.EqualTo(1));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task SerializedEventContainsNoActivityContext() {
        string rootPath = CreateRootPath();
        try {
            RecordingEventWriter writer = new();
            await using OperationalEventSession session = CreateSession(rootPath);
            await using ManualPauseController controller = new(session, writer);
            await controller.InitializeAsync(CancellationToken.None);
            await controller.SetPausedAsync(true, PauseTime, CancellationToken.None);
            string json = System.Text.Encoding.UTF8.GetString(ActivityEventJsonSerializer.Serialize(writer.Events.Single()));
            Assert.That(json, Is.EqualTo("{\"schemaVersion\":1,\"sequence\":1,\"eventId\":\"" + writer.Events.Single().Envelope.EventId + "\",\"timestampUtc\":\"2026-07-20T08:15:00+00:00\",\"utcOffsetMinutes\":0,\"type\":\"tracking.paused\",\"source\":\"manual\"}"));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static OperationalEventSession CreateSession(string rootPath) {
        return new OperationalEventSession(new OperationalStateStore(Path.Combine(rootPath, "state.json")), TimeZoneInfo.Utc);
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"manual-pause-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
    sealed class RecordingEventWriter : IActivityEventWriter {
        public List<ActivityEvent> Events { get; } = [];
        public int FlushCount { get; set; }
        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
        public ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
            Events.Add(activityEvent);
            return ValueTask.CompletedTask;
        }
        public ValueTask FlushAsync(CancellationToken cancellationToken) {
            FlushCount++;
            return ValueTask.CompletedTask;
        }
    }
}
