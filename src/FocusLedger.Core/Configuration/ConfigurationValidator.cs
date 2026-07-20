using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusLedger.Core.Configuration;

public enum ConfigurationValidationCode {
    UnsupportedSchema,
    DuplicateId,
    MissingReference,
    InvalidRegex,
    InvalidRange,
    UnsafePrivacySetting,
    InvalidSchedule,
    InvalidProcessName,
    InvalidSafeContext,
    MissingValue,
    TooManyErrors
}

public sealed record ConfigurationValidationError(string Path, ConfigurationValidationCode Code, string Message);

public sealed record ConfigurationValidationResult(ImmutableArray<ConfigurationValidationError> Errors) {
    public bool IsValid {
        get { return Errors.IsEmpty; }
    }
}

// Validates untrusted local configuration without copying offending values into diagnostics-safe errors.
public sealed class ConfigurationValidator {
    const int MaximumErrors = 256;
    static readonly Regex ProcessNameRegex = new("^[A-Za-z0-9._+-]+\\.exe$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    static readonly Regex SafeContextRegex = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    readonly List<ConfigurationValidationError> errors = [];
    public ConfigurationValidationResult Validate(FocusLedgerConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        lock(errors) {
            errors.Clear();
            ValidateSchema(configuration);
            ValidateTracking(configuration.Tracking);
            ValidatePrivacy(configuration.Privacy);
            ValidateStorage(configuration.Storage);
            ValidateReporting(configuration.Reporting);
            ValidateCategories(configuration.Categories);
            ValidateApplications(configuration.Applications, configuration.Categories, configuration.TitleParsers);
            ValidateTitleParsers(configuration.TitleParsers);
            ValidateRules(configuration.ClassificationRules, configuration.Categories, configuration.Applications, configuration.Privacy.MaximumSafeContextLength);
            ValidateMeetingDetectors(configuration.MeetingDetectors, configuration.ClassificationRules);
            ValidateDiagnostics(configuration.Diagnostics);
            return new ConfigurationValidationResult([.. errors]);
        }
    }
    void ValidateSchema(FocusLedgerConfiguration configuration) {
        if(configuration.SchemaVersion != 1)
            Add("$.schemaVersion", ConfigurationValidationCode.UnsupportedSchema, "The configuration schema version is not supported.");
    }
    void ValidateTracking(TrackingConfiguration tracking) {
        ValidateRange(tracking.IdleThresholdSeconds, 1, 86400, "$.tracking.idleThresholdSeconds");
        ValidateRange(tracking.ForegroundReconciliationIntervalMilliseconds, 100, 60000, "$.tracking.foregroundReconciliationIntervalMilliseconds");
        ValidateRange(tracking.IdleSamplingIntervalMilliseconds, 100, 60000, "$.tracking.idleSamplingIntervalMilliseconds");
        ValidateRange(tracking.EventFlushIntervalMilliseconds, 100, 60000, "$.tracking.eventFlushIntervalMilliseconds");
        ValidateRange(tracking.HeartbeatIntervalSeconds, 1, 3600, "$.tracking.heartbeatIntervalSeconds");
        ValidateWorkingSchedule(tracking.WorkingSchedule);
    }
    void ValidateWorkingSchedule(WorkingScheduleConfiguration schedule) {
        if(!schedule.Enabled)
            return;
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone); }
        catch(Exception exception)
            when(exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) {
            Add("$.tracking.workingSchedule.timeZone", ConfigurationValidationCode.InvalidSchedule, "The working schedule time zone is invalid.");
        }
        string[] allowedDays = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
        if(schedule.Days.IsDefaultOrEmpty || schedule.Days.Any(day => !allowedDays.Contains(day, StringComparer.Ordinal)) || schedule.Days.Distinct(StringComparer.Ordinal).Count() != schedule.Days.Length)
            Add("$.tracking.workingSchedule.days", ConfigurationValidationCode.InvalidSchedule, "The working schedule days are invalid.");
        bool startValid = TimeOnly.TryParseExact(schedule.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly start);
        bool endValid = TimeOnly.TryParseExact(schedule.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly end);
        if(!startValid || !endValid || start >= end)
            Add("$.tracking.workingSchedule", ConfigurationValidationCode.InvalidSchedule, "The working schedule interval is invalid.");
    }
    void ValidatePrivacy(PrivacyConfiguration privacy) {
        if(privacy.Mode is not "balanced" and not "strict")
            Add("$.privacy.mode", ConfigurationValidationCode.UnsafePrivacySetting, "The privacy mode is not supported.");
        if(privacy.PersistRawWindowTitles)
            Add("$.privacy.persistRawWindowTitles", ConfigurationValidationCode.UnsafePrivacySetting, "Raw window title persistence is prohibited.");
        if(privacy.PersistUrls)
            Add("$.privacy.persistUrls", ConfigurationValidationCode.UnsafePrivacySetting, "URL persistence is prohibited.");
        if(privacy.PersistExecutablePaths)
            Add("$.privacy.persistExecutablePaths", ConfigurationValidationCode.UnsafePrivacySetting, "Executable path persistence is prohibited.");
        if(privacy.PersistMeetingTitles)
            Add("$.privacy.persistMeetingTitles", ConfigurationValidationCode.UnsafePrivacySetting, "Meeting title persistence is prohibited.");
        ValidateRange(privacy.MaximumSafeContextLength, 1, 256, "$.privacy.maximumSafeContextLength");
    }
    void ValidateStorage(StorageConfiguration storage) {
        if(string.IsNullOrWhiteSpace(storage.RootDirectory))
            Add("$.storage.rootDirectory", ConfigurationValidationCode.MissingValue, "The storage root is required.");
        ValidateRange(storage.ActivityRetentionDays, 1, 36500, "$.storage.activityRetentionDays");
        ValidateRange(storage.ReportRetentionDays, 0, 36500, "$.storage.reportRetentionDays");
    }
    void ValidateReporting(ReportingConfiguration reporting) {
        ValidateRange(reporting.MinimumDisplayedIntervalSeconds, 0, 3600, "$.reporting.minimumDisplayedIntervalSeconds");
        ValidateRange(reporting.FocusSessionMinimumSeconds, 1, 86400, "$.reporting.focusSessionMinimumSeconds");
        ValidateRange(reporting.FocusSessionMaximumNeutralGapSeconds, 0, 3600, "$.reporting.focusSessionMaximumNeutralGapSeconds");
    }
    void ValidateCategories(ImmutableArray<CategoryConfiguration> categories) {
        ValidateUniqueIds(categories.Select(category => category.Id), "$.categories");
        for(int index = 0; index < categories.Length; index++) {
            CategoryConfiguration category = categories[index];
            ValidateRequired(category.Id, $"$.categories[{index}].id");
            ValidateRequired(category.DisplayName, $"$.categories[{index}].displayName");
            if(category.Disposition is not "productive" and not "neutral" and not "unproductive" and not "excluded")
                Add($"$.categories[{index}].disposition", ConfigurationValidationCode.InvalidRange, "The category disposition is invalid.");
            ValidateRange(category.Weight, 0, 1, $"$.categories[{index}].weight");
        }
    }
    void ValidateApplications(
        ImmutableArray<ApplicationConfiguration> applications,
        ImmutableArray<CategoryConfiguration> categories,
        ImmutableArray<TitleParserConfiguration> parsers) {
        ValidateUniqueIds(applications.Select(application => application.Id), "$.applications");
        HashSet<string> categoryIds = categories.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> parserIds = parsers.Select(parser => parser.Id).ToHashSet(StringComparer.Ordinal);
        for(int index = 0; index < applications.Length; index++) {
            ApplicationConfiguration application = applications[index];
            ValidateRequired(application.Id, $"$.applications[{index}].id");
            ValidateRequired(application.Family, $"$.applications[{index}].family");
            if(!categoryIds.Contains(application.DefaultCategory))
                Add($"$.applications[{index}].defaultCategory", ConfigurationValidationCode.MissingReference, "The referenced category does not exist.");
            if(application.TitleParser is not null && !parserIds.Contains(application.TitleParser))
                Add($"$.applications[{index}].titleParser", ConfigurationValidationCode.MissingReference, "The referenced title parser does not exist.");
            ValidateProcessNames(application.ProcessNames, $"$.applications[{index}].processNames");
        }
    }
    void ValidateTitleParsers(ImmutableArray<TitleParserConfiguration> parsers) {
        ValidateUniqueIds(parsers.Select(parser => parser.Id), "$.titleParsers");
        for(int index = 0; index < parsers.Length; index++) {
            ValidateRequired(parsers[index].Id, $"$.titleParsers[{index}].id");
            ValidateRequired(parsers[index].Type, $"$.titleParsers[{index}].type");
            if(parsers[index].Suffixes.IsDefaultOrEmpty)
                Add($"$.titleParsers[{index}].suffixes", ConfigurationValidationCode.MissingValue, "At least one parser suffix is required.");
        }
    }
    void ValidateRules(
        ImmutableArray<ClassificationRuleConfiguration> rules,
        ImmutableArray<CategoryConfiguration> categories,
        ImmutableArray<ApplicationConfiguration> applications,
        int maximumSafeContextLength) {
        ValidateUniqueIds(rules.Select(rule => rule.Id), "$.classificationRules");
        HashSet<string> categoryIds = categories.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> families = applications.Select(application => application.Family).ToHashSet(StringComparer.Ordinal);
        for(int index = 0; index < rules.Length; index++) {
            ClassificationRuleConfiguration rule = rules[index];
            if(!categoryIds.Contains(rule.Category))
                Add($"$.classificationRules[{index}].category", ConfigurationValidationCode.MissingReference, "The referenced category does not exist.");
            if(rule.ApplicationFamilies.Any(family => !families.Contains(family)))
                Add($"$.classificationRules[{index}].applicationFamilies", ConfigurationValidationCode.MissingReference, "A referenced application family does not exist.");
            ValidateRegex(rule.TitlePattern, $"$.classificationRules[{index}].titlePattern");
            if(!IsSafeContext(rule.SafeContext, maximumSafeContextLength))
                Add($"$.classificationRules[{index}].safeContext", ConfigurationValidationCode.InvalidSafeContext, "The safe context label is invalid.");
        }
    }
    void ValidateMeetingDetectors(
        ImmutableArray<MeetingDetectorConfiguration> detectors,
        ImmutableArray<ClassificationRuleConfiguration> rules) {
        ValidateUniqueIds(detectors.Select(detector => detector.Id), "$.meetingDetectors");
        HashSet<string> safeContexts = rules.Select(rule => rule.SafeContext).ToHashSet(StringComparer.Ordinal);
        for(int index = 0; index < detectors.Length; index++) {
            MeetingDetectorConfiguration detector = detectors[index];
            ValidateProcessNames(detector.ProcessNames, $"$.meetingDetectors[{index}].processNames");
            ValidateRange(detector.StartConfidence, 0, 1, $"$.meetingDetectors[{index}].startConfidence");
            ValidateRange(detector.ContinueConfidence, 0, 1, $"$.meetingDetectors[{index}].continueConfidence");
            if(detector.ContinueConfidence > detector.StartConfidence)
                Add($"$.meetingDetectors[{index}]", ConfigurationValidationCode.InvalidRange, "The meeting confidence thresholds are invalid.");
            ValidateRange(detector.StartDebounceSeconds, 0, 3600, $"$.meetingDetectors[{index}].startDebounceSeconds");
            ValidateRange(detector.EndDebounceSeconds, 0, 3600, $"$.meetingDetectors[{index}].endDebounceSeconds");
            if(detector.PersistTitle)
                Add($"$.meetingDetectors[{index}].persistTitle", ConfigurationValidationCode.UnsafePrivacySetting, "Meeting title persistence is prohibited.");
            if(detector.RequiredSafeContext is not null && !safeContexts.Contains(detector.RequiredSafeContext))
                Add($"$.meetingDetectors[{index}].requiredSafeContext", ConfigurationValidationCode.MissingReference, "The referenced safe context does not exist.");
        }
    }
    void ValidateDiagnostics(DiagnosticsConfiguration diagnostics) {
        ValidateRange(diagnostics.RetentionDays, 1, 36500, "$.diagnostics.retentionDays");
        if(diagnostics.IncludeRawWindowTitles)
            Add("$.diagnostics.includeRawWindowTitles", ConfigurationValidationCode.UnsafePrivacySetting, "Raw window titles are prohibited in diagnostics.");
        if(diagnostics.IncludeExecutablePaths)
            Add("$.diagnostics.includeExecutablePaths", ConfigurationValidationCode.UnsafePrivacySetting, "Executable paths are prohibited in diagnostics.");
        if(diagnostics.IncludeConfigurationContent)
            Add("$.diagnostics.includeConfigurationContent", ConfigurationValidationCode.UnsafePrivacySetting, "Configuration content is prohibited in diagnostics.");
    }
    void ValidateUniqueIds(IEnumerable<string> ids, string path) {
        if(ids.GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            Add(path, ConfigurationValidationCode.DuplicateId, "The collection contains a duplicate identifier.");
    }
    void ValidateProcessNames(ImmutableArray<string> processNames, string path) {
        if(processNames.IsDefaultOrEmpty || processNames.Any(processName => string.IsNullOrWhiteSpace(processName) || !ProcessNameRegex.IsMatch(processName)))
            Add(path, ConfigurationValidationCode.InvalidProcessName, "A process name is invalid.");
    }
    void ValidateRegex(string? pattern, string path) {
        if(pattern is null)
            return;
        try { _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
        catch(ArgumentException) { Add(path, ConfigurationValidationCode.InvalidRegex, "The regular expression is invalid."); }
    }
    void ValidateRequired(string? value, string path) {
        if(string.IsNullOrWhiteSpace(value))
            Add(path, ConfigurationValidationCode.MissingValue, "A required value is missing.");
    }
    void ValidateRange(int value, int minimum, int maximum, string path) {
        if(value < minimum || value > maximum)
            Add(path, ConfigurationValidationCode.InvalidRange, "The numeric value is outside the allowed range.");
    }
    void ValidateRange(double value, double minimum, double maximum, string path) {
        if(double.IsNaN(value) || value < minimum || value > maximum)
            Add(path, ConfigurationValidationCode.InvalidRange, "The numeric value is outside the allowed range.");
    }
    void Add(string path, ConfigurationValidationCode code, string message) {
        if(errors.Count >= MaximumErrors)
            return;
        errors.Add(new ConfigurationValidationError(path, code, message));
        if(errors.Count == MaximumErrors)
            errors[^1] = new ConfigurationValidationError("$", ConfigurationValidationCode.TooManyErrors, "Additional configuration errors were omitted.");
    }
    static bool IsSafeContext(string? value, int maximumLength) {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && SafeContextRegex.IsMatch(value);
    }
}
