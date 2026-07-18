using System.Text.Json;

namespace FocusLedger.Core.Tests;

public sealed class Schema1NamingBaselineTests {
    static string FixturePath(string fileName) {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "Compatibility", fileName);
    }
    static JsonDocument ReadFixture(string fileName) {
        return JsonDocument.Parse(File.ReadAllText(FixturePath(fileName)));
    }
    static void AssertPropertyNames(JsonElement element, params string[] expectedNames) {
        string[] actualNames = element.EnumerateObject().Select(static property => property.Name).ToArray();
        Assert.That(actualNames, Is.EquivalentTo(expectedNames));
    }
    [Test]
    public void ForegroundEventFixtureUsesCanonicalSchema1Names() {
        using JsonDocument document = ReadFixture("schema-1-foreground-event.json");
        JsonElement root = document.RootElement;
        // Freeze the envelope and foreground payload names independently from future serializer implementation.
        AssertPropertyNames(root, "schemaVersion", "sequence", "eventId", "timestampUtc", "utcOffsetMinutes", "type", "source", "correlationId", "presence", "app", "context", "classification");
        AssertPropertyNames(root.GetProperty("app"), "id", "processName", "family");
        AssertPropertyNames(root.GetProperty("context"), "label", "privacy");
        AssertPropertyNames(root.GetProperty("classification"), "category", "disposition", "weight", "ruleId", "confidence");
        Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("foreground.changed"));
    }
    [Test]
    public void EventTypeFixtureMatchesCanonicalSchema1Catalog() {
        string[] expectedTypes = [
            "tracker.started", "tracker.stopping", "tracker.stopped", "tracker.recovered_after_unclean_shutdown",
            "day.started", "day.ended", "state.snapshot", "heartbeat", "tracking.paused", "tracking.resumed",
            "foreground.changed", "foreground.context_changed", "foreground.unavailable", "idle.started", "idle.ended",
            "session.locked", "session.unlocked", "session.connected", "session.disconnected", "session.logon", "session.logoff",
            "system.suspending", "system.resumed", "system.time_changed", "system.timezone_changed", "meeting.started",
            "meeting.context_changed", "meeting.ended", "configuration.reloaded", "configuration.reload_failed", "collector.error", "writer.error"
        ];
        using JsonDocument document = ReadFixture("schema-1-event-types.json");
        string[] actualTypes = document.RootElement.EnumerateArray().Select(static element => element.GetString()!).ToArray();
        // Preserve both spelling and catalog completeness while keeping ordering non-semantic.
        Assert.That(actualTypes, Is.EquivalentTo(expectedTypes));
        Assert.That(actualTypes, Is.Unique);
    }
    [Test]
    public void ConfigurationFixtureUsesCanonicalSchema1Names() {
        using JsonDocument document = ReadFixture("schema-1-configuration-names.json");
        JsonElement root = document.RootElement;
        // Freeze every schema-1 configuration section and its documented field names.
        AssertPropertyNames(root, "schemaVersion", "tracking", "privacy", "storage", "reporting", "startup", "categories", "applications", "titleParsers", "classificationRules", "meetingDetectors", "diagnostics");
        AssertPropertyNames(root.GetProperty("tracking"), "idleThresholdSeconds", "foregroundReconciliationIntervalMilliseconds", "idleSamplingIntervalMilliseconds", "eventFlushIntervalMilliseconds", "heartbeatIntervalSeconds", "persistManualPause", "workingSchedule");
        AssertPropertyNames(root.GetProperty("tracking").GetProperty("workingSchedule"), "enabled", "timeZone", "days", "start", "end");
        AssertPropertyNames(root.GetProperty("privacy"), "mode", "persistRawWindowTitles", "persistUrls", "persistExecutablePaths", "persistMeetingTitles", "maximumSafeContextLength", "rejectEmailAddresses", "rejectWindowsPaths", "rejectUrls", "rejectIpAddresses", "rejectLongNumericIdentifiers");
        AssertPropertyNames(root.GetProperty("storage"), "rootDirectory", "activityRetentionDays", "reportRetentionDays", "createYearMonthDirectories", "allowConcurrentReportReads");
        AssertPropertyNames(root.GetProperty("reporting"), "openAfterInteractiveGeneration", "includeTimeline", "includeApplications", "includeSafeContexts", "includeFocusMetrics", "includeMeetingMetrics", "includeLostTimeMetrics", "includeDataQuality", "minimumDisplayedIntervalSeconds", "focusSessionMinimumSeconds", "focusSessionMaximumNeutralGapSeconds");
        AssertPropertyNames(root.GetProperty("startup"), "enableAutomatically", "registryValueName", "arguments");
        AssertPropertyNames(root.GetProperty("categories")[0], "id", "displayName", "disposition", "weight");
        AssertPropertyNames(root.GetProperty("applications")[0], "id", "processNames", "family", "defaultCategory", "titleParser");
        AssertPropertyNames(root.GetProperty("titleParsers")[0], "id", "type", "suffixes", "emitSafeLabelsOnly");
        AssertPropertyNames(root.GetProperty("classificationRules")[0], "id", "priority", "enabled", "applicationFamilies", "titlePattern", "category", "safeContext");
        AssertPropertyNames(root.GetProperty("meetingDetectors")[0], "id", "enabled", "provider", "processNames", "requiredSafeContext", "startConfidence", "continueConfidence", "startDebounceSeconds", "endDebounceSeconds", "useAudioEvidence", "persistTitle");
        AssertPropertyNames(root.GetProperty("diagnostics"), "enabled", "minimumLevel", "retentionDays", "includeStackTraces", "includeRawWindowTitles", "includeExecutablePaths", "includeConfigurationContent");
        Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
    }
}
