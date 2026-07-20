namespace FocusLedger.Core.Persistence;

// Defines the canonical daily activity-file naming rule shared by the writer, the composition root, and test harnesses.
public static class ActivityFileNaming {
    public static string GetFilePath(string dataRootPath, DateOnly date) {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);
        return Path.Combine(
            dataRootPath,
            date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            $"activity-{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.jsonl");
    }
}
