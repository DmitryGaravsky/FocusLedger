using System.Text;
using System.Text.Json;
using FocusLedger.Core.Events;
using FocusLedger.Core.Persistence;

namespace FocusLedger.Core.Tests;

public sealed class JsonlActivityEventWriterTests {
    [Test]
    public async Task WriterAppendsCompleteUtf8LinesWithoutBom() {
        string filePath = CreateFilePath();
        try {
            await using(JsonlActivityEventWriter writer = CreateWriter(filePath)) {
                await writer.AppendAsync(CreateEvent(1, "foreground.changed"), CancellationToken.None);
                await writer.AppendAsync(CreateEvent(2, "foreground.changed"), CancellationToken.None);
                await writer.FlushAsync(CancellationToken.None);
            }
            byte[] content = await File.ReadAllBytesAsync(filePath);
            Assert.Multiple(() => {
                Assert.That(content.AsSpan().StartsWith(Encoding.UTF8.Preamble), Is.False);
                Assert.That(content[^1], Is.EqualTo((byte)'\n'));
            });
            string[] lines = Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Has.Length.EqualTo(2));
            using JsonDocument first = JsonDocument.Parse(lines[0]);
            using JsonDocument second = JsonDocument.Parse(lines[1]);
            Assert.Multiple(() => {
                Assert.That(first.RootElement.GetProperty("sequence").GetInt64(), Is.EqualTo(1));
                Assert.That(second.RootElement.GetProperty("sequence").GetInt64(), Is.EqualTo(2));
            });
        }
        finally { File.Delete(filePath); }
    }
    [Test]
    public async Task ActiveWriterAllowsConcurrentReadButRejectsAnotherWriter() {
        string filePath = CreateFilePath();
        try {
            await using(JsonlActivityEventWriter writer = CreateWriter(filePath)) {
                await writer.AppendAsync(CreateEvent(1, "foreground.changed"), CancellationToken.None);
                await writer.FlushAsync(CancellationToken.None);
                using(FileStream reader = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    Assert.That(reader.Length, Is.GreaterThan(0));
                }
                Assert.That(() => CreateWriter(filePath), Throws.TypeOf<IOException>());
            }
        }
        finally { File.Delete(filePath); }
    }
    [Test]
    public async Task CriticalTransitionFlushesImmediately() {
        string filePath = CreateFilePath();
        try {
            await using(JsonlActivityEventWriter writer = CreateWriter(filePath)) {
                await writer.AppendAsync(CreateEvent(1, "system.suspending"), CancellationToken.None);
                Assert.That(writer.GetMetrics().FlushCount, Is.EqualTo(1));
            }
        }
        finally { File.Delete(filePath); }
    }
    [Test]
    public async Task PeriodicTimerFlushesBufferedEvents() {
        string filePath = CreateFilePath();
        try {
            await using(JsonlActivityEventWriter writer = new(filePath, TimeSpan.FromMilliseconds(20), TimeProvider.System)) {
                await writer.AppendAsync(CreateEvent(1, "foreground.changed"), CancellationToken.None);
                await WaitForFlushAsync(writer);
                Assert.That(writer.GetMetrics().FlushCount, Is.GreaterThanOrEqualTo(1));
            }
        }
        finally { File.Delete(filePath); }
    }
    [Test]
    public async Task ExplicitFlushAndDisposalAreDeterministic() {
        string filePath = CreateFilePath();
        try {
            JsonlActivityEventWriter writer = CreateWriter(filePath);
            await writer.AppendAsync(CreateEvent(1, "foreground.changed"), CancellationToken.None);
            await writer.FlushAsync(CancellationToken.None);
            await writer.DisposeAsync();
            await writer.DisposeAsync();
            Assert.That(async () => await writer.AppendAsync(CreateEvent(2, "foreground.changed"), CancellationToken.None), Throws.TypeOf<ObjectDisposedException>());
        }
        finally { File.Delete(filePath); }
    }
    static JsonlActivityEventWriter CreateWriter(string filePath) {
        return new JsonlActivityEventWriter(filePath, TimeSpan.FromHours(1), TimeProvider.System);
    }
    static ForegroundActivityEvent CreateEvent(long sequence, string eventType) {
        EventEnvelope envelope = new(1, sequence, Guid.CreateVersion7(), DateTimeOffset.UtcNow, 0, eventType, "test");
        return new ForegroundActivityEvent(
            envelope,
            "active",
            new ApplicationEventData("visual-studio", "devenv.exe", "development-environment"),
            new ContextEventData("source-code", "normalized"),
            new ClassificationEventData("work.development", "productive", 1, "builtin.visual-studio", 1));
    }
    static string CreateFilePath() {
        return Path.Combine(TestContext.CurrentContext.WorkDirectory, $"activity-{Guid.NewGuid():N}.jsonl");
    }
    static async Task WaitForFlushAsync(JsonlActivityEventWriter writer) {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        while(writer.GetMetrics().FlushCount == 0)
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
    }
}
