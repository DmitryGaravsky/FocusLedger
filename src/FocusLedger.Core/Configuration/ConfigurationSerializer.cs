using System.Text.Json;

namespace FocusLedger.Core.Configuration;

// Provides the source-generated schema boundary for user-editable configuration files.
public static class ConfigurationSerializer {
    public static byte[] Serialize(FocusLedgerConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        return JsonSerializer.SerializeToUtf8Bytes(configuration, ConfigurationJsonContext.Default.FocusLedgerConfiguration);
    }
    public static FocusLedgerConfiguration? Deserialize(ReadOnlySpan<byte> utf8Json) {
        return JsonSerializer.Deserialize(utf8Json, ConfigurationJsonContext.Default.FocusLedgerConfiguration);
    }
}
