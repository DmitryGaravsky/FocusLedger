using System.Collections.Immutable;

namespace FocusLedger.Core.Configuration;

// Creates the complete privacy-safe schema-1 configuration shipped inside the executable.
public static class BuiltInConfiguration {
    public static FocusLedgerConfiguration CreateDefault() {
        return new FocusLedgerConfiguration(
            1,
            new TrackingConfiguration(300, 1000, 1000, 2000, 60, true,
                new WorkingScheduleConfiguration(false, "Europe/Madrid", Values("monday", "tuesday", "wednesday", "thursday", "friday"), "09:00", "18:00")),
            new PrivacyConfiguration("balanced", false, false, false, false, 64, true, true, true, true, true),
            new StorageConfiguration("%LocalAppData%\\FocusLedger", 365, 0, true, true),
            new ReportingConfiguration(true, true, true, true, true, true, true, true, 5, 600, 60),
            new StartupConfiguration(false, "FocusLedger", "--autostart"),
            CreateCategories(),
            CreateApplications(),
            CreateTitleParsers(),
            CreateClassificationRules(),
            CreateMeetingDetectors(),
            new DiagnosticsConfiguration(true, "information", 14, true, false, false, false));
    }
    static ImmutableArray<CategoryConfiguration> CreateCategories() {
        return [
            Category("work.development", "Development", "productive", 1.0),
            Category("work.code-review", "Code Review", "productive", 1.0),
            Category("work.research", "Research", "productive", 1.0),
            Category("work.documentation", "Documentation", "productive", 1.0),
            Category("work.communication", "Work Communication", "productive", 0.9),
            Category("work.meeting", "Meeting", "productive", 1.0),
            Category("work.administration", "Work Administration", "neutral", 0.6),
            Category("work.planning", "Planning", "productive", 1.0),
            Category("work.management", "Management", "productive", 0.9),
            Category("work.mentoring", "Mentoring", "productive", 1.0),
            Category("work.security", "Security Work", "productive", 1.0),
            Category("learning.technical", "Technical Learning", "productive", 0.9),
            Category("learning.language", "Language Learning", "productive", 0.9),
            Category("personal.communication", "Personal Communication", "neutral", 0.3),
            Category("personal.administration", "Personal Administration", "neutral", 0.4),
            Category("personal.entertainment", "Personal Entertainment", "neutral", 0.0),
            Category("distraction.social", "Social Media", "unproductive", 0.0),
            Category("distraction.video", "Entertainment Video", "unproductive", 0.0),
            Category("distraction.news", "News Browsing", "unproductive", 0.0),
            Category("distraction.games", "Games", "unproductive", 0.0),
            Category("system", "System", "excluded", 0.0),
            Category("idle", "Idle", "excluded", 0.0),
            Category("locked", "Locked", "excluded", 0.0),
            Category("paused", "Paused", "excluded", 0.0),
            Category("unclassified", "Unclassified", "neutral", 0.0)
        ];
    }
    static ImmutableArray<ApplicationConfiguration> CreateApplications() {
        return [
            Application("visual-studio", Values("devenv.exe"), "development-environment", "work.development"),
            Application("visual-studio-code", Values("code.exe"), "development-environment", "work.development"),
            Application("rider", Values("rider64.exe"), "development-environment", "work.development"),
            Application("windows-terminal", Values("windowsterminal.exe"), "terminal", "work.development"),
            Application("powershell", Values("powershell.exe", "pwsh.exe"), "terminal", "work.development"),
            Application("google-chrome", Values("chrome.exe"), "browser", "unclassified", "chromium-browser"),
            Application("microsoft-edge", Values("msedge.exe"), "browser", "unclassified", "chromium-browser"),
            Application("mozilla-firefox", Values("firefox.exe"), "browser", "unclassified", "firefox-browser"),
            Application("microsoft-teams", Values("ms-teams.exe", "teams.exe"), "communication", "work.communication"),
            Application("zoom", Values("zoom.exe"), "communication", "work.communication"),
            Application("slack", Values("slack.exe"), "communication", "work.communication"),
            Application("outlook", Values("outlook.exe", "olk.exe"), "email", "work.communication"),
            Application("word", Values("winword.exe"), "office", "work.documentation"),
            Application("excel", Values("excel.exe"), "office", "work.administration"),
            Application("powerpoint", Values("powerpnt.exe"), "office", "work.documentation"),
            Application("adobe-acrobat", Values("acrobat.exe", "acrord32.exe"), "document-reader", "work.documentation"),
            Application("file-explorer", Values("explorer.exe"), "system-shell", "system"),
            Application("notepad", Values("notepad.exe"), "text-editor", "work.documentation")
        ];
    }
    static ImmutableArray<TitleParserConfiguration> CreateTitleParsers() {
        return [
            new TitleParserConfiguration("chromium-browser", "known-browser-suffix", Values(" - Google Chrome", " - Microsoft Edge"), true),
            new TitleParserConfiguration("firefox-browser", "known-browser-suffix", Values(" — Mozilla Firefox", " - Mozilla Firefox"), true)
        ];
    }
    static ImmutableArray<ClassificationRuleConfiguration> CreateClassificationRules() {
        return [
            Rule("builtin.github.pull-request", 1000, Values("browser"), "(?i)pull request|github", "work.code-review", "pull-request"),
            Rule("builtin.microsoft-learn", 950, Values("browser"), "(?i)microsoft learn|\\.net documentation", "learning.technical", "technical-documentation"),
            Rule("builtin.google-meet", 940, Values("browser"), "(?i)google meet|meet -", "work.meeting", "web-meeting"),
            Rule("builtin.youtube", 500, Values("browser"), "(?i)youtube", "distraction.video", "youtube"),
            Rule("builtin.development-environment", 100, Values("development-environment", "terminal"), null, "work.development", "source-code")
        ];
    }
    static ImmutableArray<MeetingDetectorConfiguration> CreateMeetingDetectors() {
        return [
            Meeting("microsoft-teams", "microsoft-teams", Values("ms-teams.exe", "teams.exe"), null, 0.8, 0.6),
            Meeting("zoom", "zoom", Values("zoom.exe"), null, 0.8, 0.6),
            Meeting("google-meet", "google-meet", Values("chrome.exe", "msedge.exe"), "web-meeting", 0.8, 0.6),
            Meeting("slack-huddles", "slack-huddles", Values("slack.exe"), null, 0.85, 0.65),
            Meeting("webex", "webex", Values("webex.exe", "ciscocollabhost.exe"), null, 0.8, 0.6)
        ];
    }
    static CategoryConfiguration Category(string id, string displayName, string disposition, double weight) {
        return new CategoryConfiguration(id, displayName, disposition, weight);
    }
    static ApplicationConfiguration Application(
        string id,
        ImmutableArray<string> processNames,
        string family,
        string category,
        string? parser = null) {
        return new ApplicationConfiguration(id, processNames, family, category, parser);
    }
    static ClassificationRuleConfiguration Rule(
        string id,
        int priority,
        ImmutableArray<string> families,
        string? pattern,
        string category,
        string context) {
        return new ClassificationRuleConfiguration(id, priority, true, families, pattern, category, context);
    }
    static MeetingDetectorConfiguration Meeting(
        string id,
        string provider,
        ImmutableArray<string> processNames,
        string? requiredContext,
        double startConfidence,
        double continueConfidence) {
        return new MeetingDetectorConfiguration(id, true, provider, processNames, requiredContext, startConfidence, continueConfidence, 10, 15, true, false);
    }
    static ImmutableArray<string> Values(params string[] values) {
        return [.. values];
    }
}
