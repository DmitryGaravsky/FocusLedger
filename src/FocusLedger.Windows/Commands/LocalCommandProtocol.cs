using System.Text.Json.Serialization;

namespace FocusLedger.Windows.Commands;

// Defines the bounded same-user command surface supported by the current application milestone.
public enum LocalCommandKind {
    Status,
    Pause,
    Resume,
    EnableStartup,
    DisableStartup,
    Quit
}

public sealed record LocalCommandResult(bool Accepted, string Status, Func<ValueTask>? AfterAcknowledgement = null);

sealed record LocalCommandRequest(int SchemaVersion, string Command);

sealed record LocalCommandResponse(int SchemaVersion, bool Accepted, string Status);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LocalCommandRequest))]
[JsonSerializable(typeof(LocalCommandResponse))]
sealed partial class LocalCommandJsonContext : JsonSerializerContext {
}

// Validates the currently supported command-line surface before process state is opened.
public static class LocalCommandLine {
    public static bool TryParse(string[] args, out LocalCommandKind? command) {
        ArgumentNullException.ThrowIfNull(args);
        command = null;
        if(args.Length == 0 || args is ["--autostart"])
            return true;
        if(args.Length != 1)
            return false;
        command = args[0] switch {
            "--status" => LocalCommandKind.Status,
            "--pause" => LocalCommandKind.Pause,
            "--resume" => LocalCommandKind.Resume,
            "--enable-startup" => LocalCommandKind.EnableStartup,
            "--disable-startup" => LocalCommandKind.DisableStartup,
            "--quit" => LocalCommandKind.Quit,
            _ => null
        };
        return command is not null;
    }
}
