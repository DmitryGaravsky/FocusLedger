namespace FocusLedger.Core.Persistence;

using System.Globalization;
using System.Security;

// Removes only expired files whose names and locations match FocusLedger-owned retention partitions.
public sealed class RetentionMaintenance {
    readonly string dataRootPath;
    readonly string logsRootPath;
    public RetentionMaintenance(string storageRootPath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRootPath);
        string rootPath = Path.GetFullPath(storageRootPath);
        dataRootPath = Path.Combine(rootPath, "data");
        logsRootPath = Path.Combine(rootPath, "logs");
    }
    public RetentionMaintenanceResult Run(
        DateOnly currentDate,
        int activityRetentionDays,
        int diagnosticRetentionDays,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThan(activityRetentionDays, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(diagnosticRetentionDays, 1);
        DateOnly activityFirstRetainedDate = currentDate.AddDays(1 - activityRetentionDays);
        DateOnly diagnosticFirstRetainedDate = currentDate.AddDays(1 - diagnosticRetentionDays);
        int deletedActivityFiles = 0;
        int deletedDiagnosticFiles = 0;
        int failures = 0;
        ProcessActivityFiles(activityFirstRetainedDate, cancellationToken, ref deletedActivityFiles, ref failures);
        ProcessDiagnosticFiles(diagnosticFirstRetainedDate, cancellationToken, ref deletedDiagnosticFiles, ref failures);
        return new RetentionMaintenanceResult(deletedActivityFiles, deletedDiagnosticFiles, failures);
    }
    void ProcessActivityFiles(
        DateOnly firstRetainedDate,
        CancellationToken cancellationToken,
        ref int deletedFiles,
        ref int failures) {
        if(!CanInspectRoot(dataRootPath, ref failures))
            return;
        foreach(string directoryPath in EnumerateDirectories(dataRootPath, ref failures)) {
            cancellationToken.ThrowIfCancellationRequested();
            if(IsReparsePoint(directoryPath, ref failures) || !TryParseMonthDirectory(directoryPath, out int year, out int month))
                continue;
            foreach(string filePath in EnumerateFiles(directoryPath, ref failures)) {
                cancellationToken.ThrowIfCancellationRequested();
                if(!TryParseActivityFile(filePath, out DateOnly fileDate) || fileDate.Year != year || fileDate.Month != month)
                    continue;
                TryDeleteExpiredFile(filePath, fileDate, firstRetainedDate, ref deletedFiles, ref failures);
            }
        }
    }
    void ProcessDiagnosticFiles(
        DateOnly firstRetainedDate,
        CancellationToken cancellationToken,
        ref int deletedFiles,
        ref int failures) {
        if(!CanInspectRoot(logsRootPath, ref failures))
            return;
        foreach(string filePath in EnumerateFiles(logsRootPath, ref failures)) {
            cancellationToken.ThrowIfCancellationRequested();
            if(!TryParseDiagnosticFile(filePath, out DateOnly fileDate))
                continue;
            TryDeleteExpiredFile(filePath, fileDate, firstRetainedDate, ref deletedFiles, ref failures);
        }
    }
    static bool CanInspectRoot(string path, ref int failures) {
        return Directory.Exists(path) && !IsReparsePoint(path, ref failures);
    }
    static string[] EnumerateDirectories(string path, ref int failures) {
        if(!Directory.Exists(path))
            return [];
        try { return Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly); }
        catch(Exception exception)
            when(IsExpectedFileSystemFailure(exception)) {
            failures++;
            return [];
        }
    }
    static string[] EnumerateFiles(string path, ref int failures) {
        if(!Directory.Exists(path))
            return [];
        try { return Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly); }
        catch(Exception exception)
            when(IsExpectedFileSystemFailure(exception)) {
            failures++;
            return [];
        }
    }
    static bool IsReparsePoint(string path, ref int failures) {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch(Exception exception)
            when(IsExpectedFileSystemFailure(exception)) {
            failures++;
            return true;
        }
    }
    static bool TryParseMonthDirectory(string directoryPath, out int year, out int month) {
        string name = Path.GetFileName(directoryPath);
        year = 0;
        month = 0;
        return name.Length == 7
            && name[4] == '-'
            && int.TryParse(name.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && int.TryParse(name.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out month)
            && year is >= 1 and <= 9999
            && month is >= 1 and <= 12;
    }
    static bool TryParseActivityFile(string filePath, out DateOnly date) {
        return TryParseDatedFile(filePath, "activity-", ".jsonl", out date);
    }
    static bool TryParseDiagnosticFile(string filePath, out DateOnly date) {
        return TryParseDatedFile(filePath, "diagnostic-", ".log", out date);
    }
    static bool TryParseDatedFile(string filePath, string prefix, string extension, out DateOnly date) {
        string fileName = Path.GetFileName(filePath);
        if(!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(extension, StringComparison.Ordinal)) {
            date = default;
            return false;
        }
        ReadOnlySpan<char> dateText = fileName.AsSpan(prefix.Length, fileName.Length - prefix.Length - extension.Length);
        return DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
    static void TryDeleteExpiredFile(
        string filePath,
        DateOnly fileDate,
        DateOnly firstRetainedDate,
        ref int deletedFiles,
        ref int failures) {
        if(fileDate >= firstRetainedDate)
            return;
        try {
            File.Delete(filePath);
            deletedFiles++;
        }
        catch(Exception exception)
            when(IsExpectedFileSystemFailure(exception)) {
            failures++;
        }
    }
    static bool IsExpectedFileSystemFailure(Exception exception) {
        return exception is IOException or UnauthorizedAccessException or SecurityException;
    }
}

public sealed record RetentionMaintenanceResult(int DeletedActivityFiles, int DeletedDiagnosticFiles, int Failures);
