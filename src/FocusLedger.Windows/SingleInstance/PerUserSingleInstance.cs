using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace FocusLedger.Windows.SingleInstance;

// Owns the process-wide mutex that permits one FocusLedger process for each Windows user.
public sealed class PerUserSingleInstance : IDisposable {
    readonly string mutexName;
    readonly ManualResetEventSlim acquisitionCompleted = new(false);
    readonly ManualResetEventSlim releaseRequested = new(false);
    readonly Thread ownershipThread;
    Exception? acquisitionException;
    bool isPrimary;
    bool disposed;
    PerUserSingleInstance(string mutexName) {
        this.mutexName = mutexName;
        ownershipThread = new(RunOwnershipThread) {
            IsBackground = false,
            Name = "FocusLedger.SingleInstance"
        };
        ownershipThread.Start();
        acquisitionCompleted.Wait();
        if(acquisitionException is not null) {
            ownershipThread.Join();
            acquisitionCompleted.Dispose();
            releaseRequested.Dispose();
            throw new InvalidOperationException("The per-user single-instance lock could not be created.", acquisitionException);
        }
    }
    public void Dispose() {
        if(disposed)
            return;
        releaseRequested.Set();
        ownershipThread.Join();
        acquisitionCompleted.Dispose();
        releaseRequested.Dispose();
        disposed = true;
    }
    public bool IsPrimary {
        get { return isPrimary; }
    }
    // Resolves the current user transiently and stores only its one-way-derived lock name in the kernel namespace.
    public static PerUserSingleInstance Acquire() {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string? securityIdentifier = identity.User?.Value;
        if(string.IsNullOrWhiteSpace(securityIdentifier))
            throw new InvalidOperationException("The current Windows user identity is unavailable.");
        return Acquire(securityIdentifier);
    }
    internal static PerUserSingleInstance Acquire(string securityIdentifier) {
        return new PerUserSingleInstance(CreateMutexName(securityIdentifier));
    }
    internal static string CreateMutexName(string securityIdentifier) {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityIdentifier);
        byte[] identityBytes = Encoding.UTF8.GetBytes(securityIdentifier);
        byte[] identityHash = SHA256.HashData(identityBytes);
        return $"Global\\FocusLedger.SingleInstance.v1.{Convert.ToHexString(identityHash)}";
    }
    void RunOwnershipThread() {
        try {
            using Mutex mutex = new(false, mutexName);
            try { isPrimary = mutex.WaitOne(TimeSpan.Zero); }
            catch(AbandonedMutexException) { isPrimary = true; }
            acquisitionCompleted.Set();
            if(!isPrimary)
                return;
            releaseRequested.Wait();
            mutex.ReleaseMutex();
        }
        catch(Exception exception) {
            acquisitionException = exception;
            acquisitionCompleted.Set();
        }
    }
}
