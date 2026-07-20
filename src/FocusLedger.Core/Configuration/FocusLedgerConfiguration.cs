using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace FocusLedger.Core.Configuration;

// Represents the complete immutable schema-1 configuration activated by runtime components.
public sealed record FocusLedgerConfiguration(
    int SchemaVersion,
    TrackingConfiguration Tracking,
    PrivacyConfiguration Privacy,
    StorageConfiguration Storage,
    ReportingConfiguration Reporting,
    StartupConfiguration Startup,
    ImmutableArray<CategoryConfiguration> Categories,
    ImmutableArray<ApplicationConfiguration> Applications,
    ImmutableArray<TitleParserConfiguration> TitleParsers,
    ImmutableArray<ClassificationRuleConfiguration> ClassificationRules,
    ImmutableArray<MeetingDetectorConfiguration> MeetingDetectors,
    DiagnosticsConfiguration Diagnostics) : IConfigurationSnapshot;

public sealed record TrackingConfiguration(
    int IdleThresholdSeconds,
    int ForegroundReconciliationIntervalMilliseconds,
    int IdleSamplingIntervalMilliseconds,
    int EventFlushIntervalMilliseconds,
    int HeartbeatIntervalSeconds,
    bool PersistManualPause,
    WorkingScheduleConfiguration WorkingSchedule);

public sealed record WorkingScheduleConfiguration(
    bool Enabled,
    string TimeZone,
    ImmutableArray<string> Days,
    string Start,
    string End);

public sealed record PrivacyConfiguration(
    string Mode,
    bool PersistRawWindowTitles,
    bool PersistUrls,
    bool PersistExecutablePaths,
    bool PersistMeetingTitles,
    int MaximumSafeContextLength,
    bool RejectEmailAddresses,
    bool RejectWindowsPaths,
    bool RejectUrls,
    bool RejectIpAddresses,
    bool RejectLongNumericIdentifiers);

public sealed record StorageConfiguration(
    string RootDirectory,
    int ActivityRetentionDays,
    int ReportRetentionDays,
    bool CreateYearMonthDirectories,
    bool AllowConcurrentReportReads);

public sealed record ReportingConfiguration(
    bool OpenAfterInteractiveGeneration,
    bool IncludeTimeline,
    bool IncludeApplications,
    bool IncludeSafeContexts,
    bool IncludeFocusMetrics,
    bool IncludeMeetingMetrics,
    bool IncludeLostTimeMetrics,
    bool IncludeDataQuality,
    int MinimumDisplayedIntervalSeconds,
    int FocusSessionMinimumSeconds,
    int FocusSessionMaximumNeutralGapSeconds);

public sealed record StartupConfiguration(bool EnableAutomatically, string RegistryValueName, string Arguments);

public sealed record CategoryConfiguration(string Id, string DisplayName, string Disposition, double Weight);

public sealed record ApplicationConfiguration(
    string Id,
    ImmutableArray<string> ProcessNames,
    string Family,
    string DefaultCategory,
    string? TitleParser = null);

public sealed record TitleParserConfiguration(
    string Id,
    string Type,
    ImmutableArray<string> Suffixes,
    bool EmitSafeLabelsOnly);

public sealed record ClassificationRuleConfiguration(
    string Id,
    int Priority,
    bool Enabled,
    ImmutableArray<string> ApplicationFamilies,
    string? TitlePattern,
    string Category,
    string SafeContext);

public sealed record MeetingDetectorConfiguration(
    string Id,
    bool Enabled,
    string Provider,
    ImmutableArray<string> ProcessNames,
    string? RequiredSafeContext,
    double StartConfidence,
    double ContinueConfidence,
    int StartDebounceSeconds,
    int EndDebounceSeconds,
    bool UseAudioEvidence,
    bool PersistTitle);

public sealed record DiagnosticsConfiguration(
    bool Enabled,
    string MinimumLevel,
    int RetentionDays,
    bool IncludeStackTraces,
    bool IncludeRawWindowTitles,
    bool IncludeExecutablePaths,
    bool IncludeConfigurationContent);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(FocusLedgerConfiguration))]
public sealed partial class ConfigurationJsonContext : JsonSerializerContext {
}
