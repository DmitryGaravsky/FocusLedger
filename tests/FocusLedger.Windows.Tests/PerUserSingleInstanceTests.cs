namespace FocusLedger.Windows.Tests;

using FocusLedger.Windows.SingleInstance;

public sealed class PerUserSingleInstanceTests {
    [Test]
    public void SecondAcquisitionForSameIdentityIsNotPrimary() {
        string identity = $"test-user-{Guid.NewGuid():N}";
        using PerUserSingleInstance first = PerUserSingleInstance.Acquire(identity);
        bool secondIsPrimary = Task.Run(() => {
            using PerUserSingleInstance second = PerUserSingleInstance.Acquire(identity);
            return second.IsPrimary;
        }).GetAwaiter().GetResult();
        Assert.Multiple(() => {
            Assert.That(first.IsPrimary, Is.True);
            Assert.That(secondIsPrimary, Is.False);
        });
    }
    [Test]
    public void ReleasedIdentityCanBeAcquiredAgain() {
        string identity = $"test-user-{Guid.NewGuid():N}";
        using(PerUserSingleInstance first = PerUserSingleInstance.Acquire(identity)) {
            Assert.That(first.IsPrimary, Is.True);
        }
        bool nextIsPrimary = Task.Run(() => {
            using PerUserSingleInstance next = PerUserSingleInstance.Acquire(identity);
            return next.IsPrimary;
        }).GetAwaiter().GetResult();
        Assert.That(nextIsPrimary, Is.True);
    }
    [Test]
    public void DifferentIdentitiesUseIndependentLocks() {
        using PerUserSingleInstance first = PerUserSingleInstance.Acquire($"test-user-a-{Guid.NewGuid():N}");
        using PerUserSingleInstance second = PerUserSingleInstance.Acquire($"test-user-b-{Guid.NewGuid():N}");
        Assert.Multiple(() => {
            Assert.That(first.IsPrimary, Is.True);
            Assert.That(second.IsPrimary, Is.True);
        });
    }
    [Test]
    public void MutexNameIsStableAndDoesNotExposeIdentity() {
        const string Identity = "S-1-5-21-111111111-222222222-333333333-1001";
        string first = PerUserSingleInstance.CreateMutexName(Identity);
        string second = PerUserSingleInstance.CreateMutexName(Identity);
        Assert.Multiple(() => {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("Global\\FocusLedger.SingleInstance.v1."));
            Assert.That(first, Does.Not.Contain(Identity));
            Assert.That(first, Does.Match("^Global\\\\FocusLedger\\.SingleInstance\\.v1\\.[0-9A-F]{64}$"));
        });
    }
    [Test]
    public async Task PrimaryLockCanBeDisposedFromAnotherThread() {
        PerUserSingleInstance instance = PerUserSingleInstance.Acquire($"test-user-{Guid.NewGuid():N}");
        Assert.That(instance.IsPrimary, Is.True);
        await Task.Run(instance.Dispose);
    }
}
