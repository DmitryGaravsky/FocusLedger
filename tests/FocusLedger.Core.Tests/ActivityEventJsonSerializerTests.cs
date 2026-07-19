using System.Text.Json;
using FocusLedger.Core.Events;

namespace FocusLedger.Core.Tests;

public sealed class ActivityEventJsonSerializerTests {
    static string FixturePath() {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Compatibility", "schema-1-foreground-event.json");
    }
    [Test]
    public void Schema1ForegroundFixtureRoundTripsCanonicalContract() {
        byte[] fixture = File.ReadAllBytes(FixturePath());
        ForegroundActivityEvent? activityEvent = ActivityEventJsonSerializer.DeserializeForeground(fixture);
        Assert.That(activityEvent, Is.Not.Null);
        byte[] serialized = ActivityEventJsonSerializer.Serialize(activityEvent!);
        using JsonDocument expectedDocument = JsonDocument.Parse(fixture);
        using JsonDocument actualDocument = JsonDocument.Parse(serialized);
        AssertCanonicalForegroundEvent(expectedDocument.RootElement, actualDocument.RootElement);
    }
    [Test]
    public void ReaderIgnoresUnknownAdditivePropertiesAtEveryPayloadLevel() {
        string fixture = File.ReadAllText(FixturePath());
        using JsonDocument document = JsonDocument.Parse(fixture);
        using MemoryStream stream = new();
        using(Utf8JsonWriter writer = new(stream)) {
            writer.WriteStartObject();
            foreach(JsonProperty property in document.RootElement.EnumerateObject()) {
                if(property.NameEquals("app")) {
                    writer.WritePropertyName(property.Name);
                    writer.WriteStartObject();
                    foreach(JsonProperty appProperty in property.Value.EnumerateObject())
                        appProperty.WriteTo(writer);
                    writer.WriteString("futureAppProperty", "ignored-value");
                    writer.WriteEndObject();
                    continue;
                }
                property.WriteTo(writer);
            }
            writer.WriteBoolean("futureEnvelopeProperty", true);
            writer.WriteEndObject();
        }
        ForegroundActivityEvent? activityEvent = ActivityEventJsonSerializer.DeserializeForeground(stream.ToArray());
        Assert.Multiple(() => {
            Assert.That(activityEvent, Is.Not.Null);
            Assert.That(activityEvent!.App.Id, Is.EqualTo("visual-studio"));
            Assert.That(activityEvent.Type, Is.EqualTo("foreground.changed"));
        });
    }
    [Test]
    public void OptionalCommonFieldsAreOmittedWhenAbsent() {
        EventEnvelope envelope = new(1, 1, Guid.CreateVersion7(), DateTimeOffset.UtcNow, 0, "foreground.changed");
        ForegroundActivityEvent activityEvent = new(
            envelope,
            "active",
            new ApplicationEventData("visual-studio", "devenv.exe", "development-environment"),
            null,
            new ClassificationEventData("work.development", "productive", 1, "builtin.visual-studio", 1));
        using JsonDocument document = JsonDocument.Parse(ActivityEventJsonSerializer.Serialize(activityEvent));
        Assert.Multiple(() => {
            Assert.That(document.RootElement.TryGetProperty("source", out _), Is.False);
            Assert.That(document.RootElement.TryGetProperty("correlationId", out _), Is.False);
            Assert.That(document.RootElement.TryGetProperty("context", out _), Is.False);
            Assert.That(document.RootElement.TryGetProperty("envelope", out _), Is.False);
        });
    }
    static void AssertCanonicalForegroundEvent(JsonElement expected, JsonElement actual) {
        Assert.That(actual.EnumerateObject().Select(static property => property.Name),
            Is.EqualTo(expected.EnumerateObject().Select(static property => property.Name)));
        Assert.Multiple(() => {
            Assert.That(actual.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(expected.GetProperty("schemaVersion").GetInt32()));
            Assert.That(actual.GetProperty("sequence").GetInt64(), Is.EqualTo(expected.GetProperty("sequence").GetInt64()));
            Assert.That(actual.GetProperty("eventId").GetGuid(), Is.EqualTo(expected.GetProperty("eventId").GetGuid()));
            Assert.That(actual.GetProperty("timestampUtc").GetDateTimeOffset(), Is.EqualTo(expected.GetProperty("timestampUtc").GetDateTimeOffset()));
            Assert.That(actual.GetProperty("utcOffsetMinutes").GetInt32(), Is.EqualTo(expected.GetProperty("utcOffsetMinutes").GetInt32()));
            Assert.That(actual.GetProperty("type").GetString(), Is.EqualTo(expected.GetProperty("type").GetString()));
            Assert.That(actual.GetProperty("source").GetString(), Is.EqualTo(expected.GetProperty("source").GetString()));
            Assert.That(actual.GetProperty("correlationId").GetGuid(), Is.EqualTo(expected.GetProperty("correlationId").GetGuid()));
            Assert.That(actual.GetProperty("presence").GetString(), Is.EqualTo(expected.GetProperty("presence").GetString()));
            Assert.That(JsonElement.DeepEquals(actual.GetProperty("app"), expected.GetProperty("app")), Is.True);
            Assert.That(JsonElement.DeepEquals(actual.GetProperty("context"), expected.GetProperty("context")), Is.True);
            Assert.That(JsonElement.DeepEquals(actual.GetProperty("classification"), expected.GetProperty("classification")), Is.True);
        });
    }
}
