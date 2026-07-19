using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace FocusLedger.Windows.Commands;

// Creates a stable pipe name without exposing the Windows SID or account name.
static class PerUserPipeName {
    public static string Create() {
        using(WindowsIdentity identity = WindowsIdentity.GetCurrent()) {
            string? securityIdentifier = identity.User?.Value;
            if(string.IsNullOrWhiteSpace(securityIdentifier))
                throw new InvalidOperationException("The current Windows user identity is unavailable.");
            return Create(securityIdentifier);
        }
    }
    internal static string Create(string securityIdentifier) {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityIdentifier);
        byte[] identityHash = SHA256.HashData(Encoding.UTF8.GetBytes(securityIdentifier));
        return $"FocusLedger.Commands.v1.{Convert.ToHexString(identityHash)}";
    }
}
