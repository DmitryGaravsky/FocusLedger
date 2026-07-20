using FocusLedger.Core.Configuration;

namespace FocusLedger.Core.Tests;

public sealed class ConfigurationValidatorTests {
    const string Canary = "Customer.Name@example.invalid";
    readonly ConfigurationValidator validator = new();
    [Test]
    public void BuiltInDefaultIsValid() {
        ConfigurationValidationResult result = validator.Validate(BuiltInConfiguration.CreateDefault());
        Assert.That(result.Errors, Is.Empty);
    }
    [Test]
    public void DuplicateIdsAndMissingReferencesAreRejected() {
        FocusLedgerConfiguration baseline = BuiltInConfiguration.CreateDefault();
        FocusLedgerConfiguration configuration = baseline with {
            Categories = baseline.Categories.Add(baseline.Categories[0]),
            Applications = baseline.Applications.SetItem(0, baseline.Applications[0] with { DefaultCategory = "missing-category", TitleParser = "missing-parser" })
        };
        ConfigurationValidationResult result = validator.Validate(configuration);
        Assert.Multiple(() => {
            Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.DuplicateId));
            Assert.That(result.Errors.Count(error => error.Code == ConfigurationValidationCode.MissingReference), Is.EqualTo(2));
        });
    }
    [Test]
    public void UnsafePrivacySettingsAreRejected() {
        FocusLedgerConfiguration baseline = BuiltInConfiguration.CreateDefault();
        FocusLedgerConfiguration configuration = baseline with {
            Privacy = baseline.Privacy with { PersistRawWindowTitles = true, PersistUrls = true },
            Diagnostics = baseline.Diagnostics with { IncludeConfigurationContent = true }
        };
        ConfigurationValidationResult result = validator.Validate(configuration);
        Assert.That(result.Errors.Count(error => error.Code == ConfigurationValidationCode.UnsafePrivacySetting), Is.EqualTo(3));
    }
    [Test]
    public void InvalidRegexRangesProcessNamesAndSafeContextsAreRejected() {
        FocusLedgerConfiguration baseline = BuiltInConfiguration.CreateDefault();
        FocusLedgerConfiguration configuration = baseline with {
            Tracking = baseline.Tracking with { IdleThresholdSeconds = 0 },
            Applications = baseline.Applications.SetItem(0, baseline.Applications[0] with { ProcessNames = ["..\\unsafe.exe"] }),
            ClassificationRules = baseline.ClassificationRules.SetItem(0, baseline.ClassificationRules[0] with { TitlePattern = "[", SafeContext = "Unsafe Context" })
        };
        ConfigurationValidationResult result = validator.Validate(configuration);
        Assert.Multiple(() => {
            Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.InvalidRegex));
            Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.InvalidRange));
            Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.InvalidProcessName));
            Assert.That(result.Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.InvalidSafeContext));
        });
    }
    [Test]
    public void EnabledInvalidScheduleIsRejected() {
        FocusLedgerConfiguration baseline = BuiltInConfiguration.CreateDefault();
        FocusLedgerConfiguration configuration = baseline with {
            Tracking = baseline.Tracking with {
                WorkingSchedule = baseline.Tracking.WorkingSchedule with {
                    Enabled = true,
                    TimeZone = "Invalid/TimeZone",
                    Days = ["monday", "monday"],
                    Start = "18:00",
                    End = "09:00"
                }
            }
        };
        ConfigurationValidationResult result = validator.Validate(configuration);
        Assert.That(result.Errors.Count(error => error.Code == ConfigurationValidationCode.InvalidSchedule), Is.EqualTo(3));
    }
    [Test]
    public void ErrorsDoNotCopyUntrustedValues() {
        FocusLedgerConfiguration baseline = BuiltInConfiguration.CreateDefault();
        FocusLedgerConfiguration configuration = baseline with {
            Applications = baseline.Applications.SetItem(0, baseline.Applications[0] with { ProcessNames = [Canary] })
        };
        ConfigurationValidationResult result = validator.Validate(configuration);
        string renderedErrors = string.Join('|', result.Errors.Select(error => $"{error.Path}:{error.Code}:{error.Message}"));
        Assert.That(renderedErrors, Does.Not.Contain(Canary));
    }
    [Test]
    public void MissingRequiredStringFromJsonProducesSafeError() {
        byte[] json = ConfigurationSerializer.Serialize(BuiltInConfiguration.CreateDefault());
        string modifiedJson = System.Text.Encoding.UTF8.GetString(json).Replace("\"timeZone\": \"Europe/Madrid\"", "\"timeZone\": null", StringComparison.Ordinal);
        FocusLedgerConfiguration configuration = ConfigurationSerializer.Deserialize(System.Text.Encoding.UTF8.GetBytes(modifiedJson))!;
        configuration = configuration with { Tracking = configuration.Tracking with { WorkingSchedule = configuration.Tracking.WorkingSchedule with { Enabled = true } } };
        Assert.That(() => validator.Validate(configuration), Throws.Nothing);
        Assert.That(validator.Validate(configuration).Errors, Has.Some.Property(nameof(ConfigurationValidationError.Code)).EqualTo(ConfigurationValidationCode.InvalidSchedule));
    }
}
