using FocusLedger.Core.Persistence;

namespace FocusLedger.Core.Tests;

public sealed class RetentionMaintenanceTests {
    static readonly DateOnly CurrentDate = new(2026, 7, 20);
    [Test]
    public void RunDeletesOnlyActivityFilesOutsideRetainedCalendarDays() {
        string rootPath = CreateRootPath();
        try {
            string expiredPath = CreateActivityFile(rootPath, new DateOnly(2025, 7, 20));
            string firstRetainedPath = CreateActivityFile(rootPath, new DateOnly(2025, 7, 21));
            string currentPath = CreateActivityFile(rootPath, CurrentDate);
            RetentionMaintenance maintenance = new(rootPath);
            RetentionMaintenanceResult result = maintenance.Run(CurrentDate, 365, 14, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(File.Exists(expiredPath), Is.False);
                Assert.That(File.Exists(firstRetainedPath), Is.True);
                Assert.That(File.Exists(currentPath), Is.True);
                Assert.That(result.DeletedActivityFiles, Is.EqualTo(1));
                Assert.That(result.Failures, Is.Zero);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public void RunDeletesExpiredDiagnosticFilesUsingIndependentPolicy() {
        string rootPath = CreateRootPath();
        try {
            string expiredPath = CreateDiagnosticFile(rootPath, new DateOnly(2026, 7, 6));
            string firstRetainedPath = CreateDiagnosticFile(rootPath, new DateOnly(2026, 7, 7));
            RetentionMaintenance maintenance = new(rootPath);
            RetentionMaintenanceResult result = maintenance.Run(CurrentDate, 365, 14, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(File.Exists(expiredPath), Is.False);
                Assert.That(File.Exists(firstRetainedPath), Is.True);
                Assert.That(result.DeletedDiagnosticFiles, Is.EqualTo(1));
                Assert.That(result.Failures, Is.Zero);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public void RunLeavesUnknownMisplacedAndReportFilesUntouched() {
        string rootPath = CreateRootPath();
        try {
            string unknownPath = CreateFile(Path.Combine(rootPath, "data", "2020-01", "notes.txt"));
            string misplacedPath = CreateFile(Path.Combine(rootPath, "data", "2026-07", "activity-2020-01-01.jsonl"));
            string reportPath = CreateFile(Path.Combine(rootPath, "reports", "activity-report-2020-01-01.html"));
            RetentionMaintenance maintenance = new(rootPath);
            RetentionMaintenanceResult result = maintenance.Run(CurrentDate, 1, 1, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(File.Exists(unknownPath), Is.True);
                Assert.That(File.Exists(misplacedPath), Is.True);
                Assert.That(File.Exists(reportPath), Is.True);
                Assert.That(result.DeletedActivityFiles, Is.Zero);
                Assert.That(result.DeletedDiagnosticFiles, Is.Zero);
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public void RunDoesNotCreateMissingRetentionDirectories() {
        string rootPath = CreateRootPath();
        try {
            RetentionMaintenance maintenance = new(rootPath);
            RetentionMaintenanceResult result = maintenance.Run(CurrentDate, 365, 14, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(Directory.Exists(Path.Combine(rootPath, "data")), Is.False);
                Assert.That(Directory.Exists(Path.Combine(rootPath, "logs")), Is.False);
                Assert.That(result, Is.EqualTo(new RetentionMaintenanceResult(0, 0, 0)));
            });
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public void RunHonorsCancellationBeforeInspectingFiles() {
        string rootPath = CreateRootPath();
        try {
            string expiredPath = CreateActivityFile(rootPath, new DateOnly(2020, 1, 1));
            using(CancellationTokenSource cancellationSource = new()) {
                cancellationSource.Cancel();
                RetentionMaintenance maintenance = new(rootPath);
                Assert.That(
                    () => maintenance.Run(CurrentDate, 365, 14, cancellationSource.Token),
                    Throws.TypeOf<OperationCanceledException>());
            }
            Assert.That(File.Exists(expiredPath), Is.True);
        }
        finally { Directory.Delete(rootPath, true); }
    }
    [Test]
    public void DeletionFailureDoesNotStopRemainingMaintenance() {
        string rootPath = CreateRootPath();
        string protectedPath = string.Empty;
        try {
            protectedPath = CreateActivityFile(rootPath, new DateOnly(2020, 1, 1));
            File.SetAttributes(protectedPath, FileAttributes.ReadOnly);
            string diagnosticPath = CreateDiagnosticFile(rootPath, new DateOnly(2020, 1, 1));
            RetentionMaintenance maintenance = new(rootPath);
            RetentionMaintenanceResult result = maintenance.Run(CurrentDate, 365, 14, CancellationToken.None);
            Assert.Multiple(() => {
                Assert.That(File.Exists(protectedPath), Is.True);
                Assert.That(File.Exists(diagnosticPath), Is.False);
                Assert.That(result.DeletedDiagnosticFiles, Is.EqualTo(1));
                Assert.That(result.Failures, Is.EqualTo(1));
            });
        }
        finally {
            if(File.Exists(protectedPath))
                File.SetAttributes(protectedPath, FileAttributes.Normal);
            Directory.Delete(rootPath, true);
        }
    }
    static string CreateRootPath() {
        string rootPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }
    static string CreateActivityFile(string rootPath, DateOnly date) {
        return CreateFile(Path.Combine(
            rootPath,
            "data",
            date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"activity-{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.jsonl"));
    }
    static string CreateDiagnosticFile(string rootPath, DateOnly date) {
        return CreateFile(Path.Combine(
            rootPath,
            "logs",
            $"diagnostic-{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.log"));
    }
    static string CreateFile(string filePath) {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "test");
        return filePath;
    }
}
