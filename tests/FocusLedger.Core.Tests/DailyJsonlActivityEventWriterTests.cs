using System.Text.Json;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;

namespace FocusLedger.Core.Tests;

public sealed class DailyJsonlActivityEventWriterTests {
    static readonly DateTimeOffset FirstDayTime = new(2026, 7, 19, 20, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset SecondDayTime = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    static readonly string[] StartedSnapshotForeground = ["day.started", "state.snapshot", "foreground.changed"];
    static readonly string[] CompletedFirstDay = ["day.started", "state.snapshot", "foreground.changed", "day.ended"];
    [Test]
    public async Task NewDailyFileBeginsWithDayStartedAndStateSnapshot() {
        string rootPath = CreateRootPath();
        try {
            await using(DailyJsonlActivityEventWriter writer = CreateWriter(rootPath)) {
                await writer.AppendAsync(CreateDayBoundary(1, "day.started", FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateSnapshot(2, FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateForeground(3, FirstDayTime), CancellationToken.None);
            }
            string[] types = await ReadEventTypesAsync(GetFilePath(rootPath, new DateOnly(2026, 7, 19)));
            Assert.That(types, Is.EqualTo(StartedSnapshotForeground));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task RolloverClosesOldFileAndCreatesIndependentNewFile() {
        string rootPath = CreateRootPath();
        try {
            await using(DailyJsonlActivityEventWriter writer = CreateWriter(rootPath)) {
                await writer.AppendAsync(CreateDayBoundary(1, "day.started", FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateSnapshot(2, FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateForeground(3, FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateDayBoundary(4, "day.ended", SecondDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateDayBoundary(5, "day.started", SecondDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateSnapshot(6, SecondDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateForeground(7, SecondDayTime), CancellationToken.None);
                Assert.That(writer.GetMetrics().RolloverCount, Is.EqualTo(1));
            }
            string[] firstDayTypes = await ReadEventTypesAsync(GetFilePath(rootPath, new DateOnly(2026, 7, 19)));
            string[] secondDayTypes = await ReadEventTypesAsync(GetFilePath(rootPath, new DateOnly(2026, 7, 20)));
            Assert.Multiple(() => {
                Assert.That(firstDayTypes, Is.EqualTo(CompletedFirstDay));
                Assert.That(secondDayTypes, Is.EqualTo(StartedSnapshotForeground));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task DateChangeWithoutOrderedBoundaryEventsIsRejected() {
        string rootPath = CreateRootPath();
        try {
            await using(DailyJsonlActivityEventWriter writer = CreateWriter(rootPath)) {
                await writer.AppendAsync(CreateDayBoundary(1, "day.started", FirstDayTime), CancellationToken.None);
                await writer.AppendAsync(CreateSnapshot(2, FirstDayTime), CancellationToken.None);
                Assert.That(async () => await writer.AppendAsync(CreateForeground(3, SecondDayTime), CancellationToken.None),
                    Throws.TypeOf<InvalidOperationException>());
            }
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task RestartAppendsExistingDailyFileWithoutSecondStartMarker() {
        string rootPath = CreateRootPath();
        try {
            await using(DailyJsonlActivityEventWriter firstWriter = CreateWriter(rootPath)) {
                await firstWriter.AppendAsync(CreateDayBoundary(1, "day.started", FirstDayTime), CancellationToken.None);
                await firstWriter.AppendAsync(CreateSnapshot(2, FirstDayTime), CancellationToken.None);
            }
            await using(DailyJsonlActivityEventWriter secondWriter = CreateWriter(rootPath)) {
                await secondWriter.AppendAsync(CreateForeground(3, FirstDayTime), CancellationToken.None);
            }
            string[] types = await ReadEventTypesAsync(GetFilePath(rootPath, new DateOnly(2026, 7, 19)));
            Assert.That(types, Is.EqualTo(StartedSnapshotForeground));
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public async Task PersistedOffsetSelectsTheLocalCalendarDate() {
        string rootPath = CreateRootPath();
        try {
            DateTimeOffset utcTime = new(2026, 7, 19, 23, 30, 0, TimeSpan.Zero);
            await using(DailyJsonlActivityEventWriter writer = CreateWriter(rootPath)) {
                await writer.AppendAsync(CreateDayBoundary(1, "day.started", utcTime, 120), CancellationToken.None);
                await writer.AppendAsync(CreateSnapshot(2, utcTime, 120), CancellationToken.None);
            }
            Assert.That(File.Exists(GetFilePath(rootPath, new DateOnly(2026, 7, 20))), Is.True);
        }
        finally { Directory.Delete(rootPath, true); }
    }
    static DailyJsonlActivityEventWriter CreateWriter(string rootPath) {
        return new DailyJsonlActivityEventWriter(rootPath, TimeSpan.FromHours(1), TimeProvider.System);
    }
    static DayBoundaryActivityEvent CreateDayBoundary(long sequence, string type, DateTimeOffset timestamp, int offsetMinutes = 0) {
        return new DayBoundaryActivityEvent(new EventEnvelope(1, sequence, Guid.CreateVersion7(), timestamp, offsetMinutes, type, "rollover"));
    }
    static StateSnapshotActivityEvent CreateSnapshot(long sequence, DateTimeOffset timestamp, int offsetMinutes = 0) {
        EventEnvelope envelope = new(1, sequence, Guid.CreateVersion7(), timestamp, offsetMinutes, "state.snapshot", "rollover");
        ForegroundSnapshotEventData foreground = new(
            new ApplicationEventData("visual-studio", "devenv.exe", "development-environment"),
            new ContextEventData("source-code", "normalized"),
            new ClassificationEventData("work.development", "productive", 1, "builtin.visual-studio", 1));
        return new StateSnapshotActivityEvent(envelope, "running", "active", new MeetingSnapshotEventData("none"), foreground);
    }
    static ForegroundActivityEvent CreateForeground(long sequence, DateTimeOffset timestamp) {
        EventEnvelope envelope = new(1, sequence, Guid.CreateVersion7(), timestamp, 0, "foreground.changed", "test");
        return new ForegroundActivityEvent(
            envelope,
            "active",
            new ApplicationEventData("visual-studio", "devenv.exe", "development-environment"),
            new ContextEventData("source-code", "normalized"),
            new ClassificationEventData("work.development", "productive", 1, "builtin.visual-studio", 1));
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"daily-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
    static string GetFilePath(string rootPath, DateOnly date) {
        return Path.Combine(
            rootPath,
            date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"activity-{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.jsonl");
    }
    static async Task<string[]> ReadEventTypesAsync(string filePath) {
        string[] lines = await File.ReadAllLinesAsync(filePath);
        return lines.Select(line => {
            using JsonDocument document = JsonDocument.Parse(line);
            return document.RootElement.GetProperty("type").GetString()!;
        }).ToArray();
    }
}
