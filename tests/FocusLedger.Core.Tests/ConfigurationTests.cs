using System.Text.Json;
using FocusLedger.Core.Configuration;

namespace FocusLedger.Core.Tests;

public sealed class ConfigurationTests {
    static readonly string[] ExpectedTopLevelProperties = [
        "schemaVersion", "tracking", "privacy", "storage", "reporting", "startup", "categories", "applications",
        "titleParsers", "classificationRules", "meetingDetectors", "diagnostics"
    ];
    [Test]
    public void BuiltInDefaultMatchesNormativePrivacyAndOperationalValues() {
        FocusLedgerConfiguration configuration = BuiltInConfiguration.CreateDefault();
        Assert.Multiple(() => {
            Assert.That(configuration.SchemaVersion, Is.EqualTo(1));
            Assert.That(configuration.Privacy.Mode, Is.EqualTo("balanced"));
            Assert.That(configuration.Privacy.PersistRawWindowTitles, Is.False);
            Assert.That(configuration.Privacy.PersistUrls, Is.False);
            Assert.That(configuration.Privacy.PersistExecutablePaths, Is.False);
            Assert.That(configuration.Privacy.PersistMeetingTitles, Is.False);
            Assert.That(configuration.Diagnostics.IncludeRawWindowTitles, Is.False);
            Assert.That(configuration.Diagnostics.IncludeExecutablePaths, Is.False);
            Assert.That(configuration.Tracking.IdleThresholdSeconds, Is.EqualTo(300));
            Assert.That(configuration.Storage.ActivityRetentionDays, Is.EqualTo(365));
            Assert.That(configuration.Categories.Length, Is.EqualTo(25));
            Assert.That(configuration.Applications.Length, Is.EqualTo(18));
            Assert.That(configuration.TitleParsers.Length, Is.EqualTo(2));
            Assert.That(configuration.ClassificationRules.Length, Is.EqualTo(5));
            Assert.That(configuration.MeetingDetectors.Length, Is.EqualTo(5));
        });
    }
    [Test]
    public void BuiltInDefaultRoundTripsThroughSourceGeneratedSchema() {
        FocusLedgerConfiguration expected = BuiltInConfiguration.CreateDefault();
        byte[] json = ConfigurationSerializer.Serialize(expected);
        FocusLedgerConfiguration? actual = ConfigurationSerializer.Deserialize(json);
        Assert.That(actual, Is.Not.Null);
        byte[] roundTrippedJson = ConfigurationSerializer.Serialize(actual!);
        using(JsonDocument expectedDocument = JsonDocument.Parse(json)) {
            using(JsonDocument actualDocument = JsonDocument.Parse(roundTrippedJson)) {
                Assert.That(JsonElement.DeepEquals(actualDocument.RootElement, expectedDocument.RootElement), Is.True);
            }
        }
    }
    [Test]
    public void SerializedDefaultUsesCanonicalTopLevelNamesAndOmitsOptionalNulls() {
        byte[] json = ConfigurationSerializer.Serialize(BuiltInConfiguration.CreateDefault());
        using(JsonDocument document = JsonDocument.Parse(json)) {
            Assert.That(document.RootElement.EnumerateObject().Select(static property => property.Name), Is.EqualTo(ExpectedTopLevelProperties));
            JsonElement visualStudio = document.RootElement.GetProperty("applications")[0];
            Assert.That(visualStudio.TryGetProperty("titleParser", out _), Is.False);
            JsonElement teams = document.RootElement.GetProperty("meetingDetectors")[0];
            Assert.That(teams.TryGetProperty("requiredSafeContext", out _), Is.False);
        }
    }
    [Test]
    public void Schema1NamingFixtureDeserializesWithoutMigration() {
        string fixturePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Compatibility", "schema-1-configuration-names.json");
        FocusLedgerConfiguration? configuration = ConfigurationSerializer.Deserialize(File.ReadAllBytes(fixturePath));
        Assert.Multiple(() => {
            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration!.SchemaVersion, Is.EqualTo(1));
            Assert.That(configuration.Categories.Single().Id, Is.EqualTo("work.development"));
            Assert.That(configuration.Applications.Single().TitleParser, Is.EqualTo("ide-window"));
            Assert.That(configuration.MeetingDetectors.Single().RequiredSafeContext, Is.EqualTo("web-meeting"));
        });
    }
}
