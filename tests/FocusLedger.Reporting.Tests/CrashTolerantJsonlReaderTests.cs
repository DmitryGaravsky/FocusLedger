using System.Text;
using FocusLedger.Core.Events;
using FocusLedger.Reporting.Reading;

namespace FocusLedger.Reporting.Tests;

public sealed class CrashTolerantJsonlReaderTests {
    [Test]
    public async Task ValidKnownAndUnknownEventsStreamInLineOrder() {
        string rootPath = CreateRootPath();
        try {
            string filePath = GetActivityFilePath(rootPath);
            string foreground = Encoding.UTF8.GetString(ActivityEventJsonSerializer.Serialize(CreateForegroundEvent(1)));
            string unknown = CreateEnvelopeJson(2, "future.event", "\"futureProperty\":true");
            await File.WriteAllTextAsync(filePath, $"{foreground}\n{unknown}\n");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            Assert.Multiple(() => {
                Assert.That(items, Has.Count.EqualTo(2));
                Assert.That(((JsonlEventReadItem)items[0]).ActivityEvent, Is.TypeOf<ForegroundActivityEvent>());
                Assert.That(((JsonlEventReadItem)items[1]).ActivityEvent, Is.Null);
                Assert.That(((JsonlEventReadItem)items[1]).Envelope.Type, Is.EqualTo("future.event"));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task MalformedMiddleLineIsReportedAndFollowingEventSurvives() {
        string rootPath = CreateRootPath();
        try {
            string filePath = GetActivityFilePath(rootPath);
            await File.WriteAllTextAsync(filePath, $"{CreateEnvelopeJson(1, "heartbeat")}\n{{broken-json\n{CreateEnvelopeJson(2, "heartbeat")}\n");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            Assert.Multiple(() => {
                Assert.That(items, Has.Count.EqualTo(3));
                Assert.That(((JsonlIssueReadItem)items[1]).Kind, Is.EqualTo(JsonlDataQualityIssueKind.MalformedEvent));
                Assert.That(((JsonlEventReadItem)items[2]).Envelope.Sequence, Is.EqualTo(2));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task IncompleteFinalLineIsReportedAsCrashResidue() {
        string rootPath = CreateRootPath();
        try {
            string filePath = GetActivityFilePath(rootPath);
            await File.WriteAllTextAsync(filePath, $"{CreateEnvelopeJson(1, "heartbeat")}\n{{\"schemaVersion\":1");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            JsonlIssueReadItem issue = (JsonlIssueReadItem)items[1];
            Assert.Multiple(() => {
                Assert.That(issue.Kind, Is.EqualTo(JsonlDataQualityIssueKind.IncompleteTrailingLine));
                Assert.That(issue.LineNumber, Is.EqualTo(2));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task UnsupportedSchemaAndInvalidEnvelopeHaveDistinctSafeIssues() {
        string rootPath = CreateRootPath();
        try {
            string filePath = GetActivityFilePath(rootPath);
            string unsupported = CreateEnvelopeJson(1, "heartbeat").Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal);
            await File.WriteAllTextAsync(filePath, $"{unsupported}\n{{\"schemaVersion\":1}}\n");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            Assert.Multiple(() => {
                Assert.That(((JsonlIssueReadItem)items[0]).Kind, Is.EqualTo(JsonlDataQualityIssueKind.UnsupportedSchema));
                Assert.That(((JsonlIssueReadItem)items[1]).Kind, Is.EqualTo(JsonlDataQualityIssueKind.InvalidEnvelope));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task OversizedLineIsRejectedWithoutStoppingTheStream() {
        string rootPath = CreateRootPath();
        try {
            string filePath = GetActivityFilePath(rootPath);
            string oversized = new('x', (1024 * 1024) + 1);
            await File.WriteAllTextAsync(filePath, $"{oversized}\n{CreateEnvelopeJson(2, "heartbeat")}\n");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            Assert.Multiple(() => {
                Assert.That(((JsonlIssueReadItem)items[0]).Kind, Is.EqualTo(JsonlDataQualityIssueKind.LineTooLong));
                Assert.That(items[1], Is.TypeOf<JsonlEventReadItem>());
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task UnsafeInputFileNameIsNeverReturnedInIssueMetadata() {
        string rootPath = CreateRootPath();
        try {
            string filePath = Path.Combine(rootPath, "Customer-Secret.jsonl");
            await File.WriteAllTextAsync(filePath, "{broken");
            IReadOnlyList<JsonlReadItem> items = await ReadAllAsync(filePath);
            Assert.That(items[0].FileName, Is.EqualTo("activity-file"));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static ForegroundActivityEvent CreateForegroundEvent(long sequence) {
        EventEnvelope envelope = new(1, sequence, Guid.CreateVersion7(), DateTimeOffset.UtcNow, 0, "foreground.changed", "test");
        return new ForegroundActivityEvent(
            envelope,
            "active",
            new ApplicationEventData("visual-studio", "devenv.exe", "development-environment"),
            new ContextEventData("source-code", "normalized"),
            new ClassificationEventData("work.development", "productive", 1, "builtin.visual-studio", 1));
    }
    static string CreateEnvelopeJson(long sequence, string type, string? additionalProperty = null) {
        string suffix = additionalProperty is null ? string.Empty : $",{additionalProperty}";
        return $"{{\"schemaVersion\":1,\"sequence\":{sequence},\"eventId\":\"{Guid.CreateVersion7()}\",\"timestampUtc\":\"2026-07-19T12:00:00Z\",\"utcOffsetMinutes\":120,\"type\":\"{type}\"{suffix}}}";
    }
    static async Task<IReadOnlyList<JsonlReadItem>> ReadAllAsync(string filePath) {
        List<JsonlReadItem> items = [];
        await foreach(JsonlReadItem item in CrashTolerantJsonlReader.ReadAsync(filePath, CancellationToken.None))
            items.Add(item);
        return items;
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"jsonl-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
    static string GetActivityFilePath(string rootPath) {
        return Path.Combine(rootPath, "activity-2026-07-19.jsonl");
    }
}
