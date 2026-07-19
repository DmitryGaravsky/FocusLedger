using FocusLedger.Core.Abstractions;

namespace FocusLedger.Core.Tests;

public sealed class ArchitectureBoundaryTests {
    [Test]
    public void CoreAssemblyDoesNotReferenceWindowsDesktopAssemblies() {
        string[] referencedAssemblies = typeof(IActivitySignalSource).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();
        // Verify the platform-neutral assembly boundary against prohibited desktop dependencies.
        Assert.That(referencedAssemblies, Does.Not.Contain("System.Windows.Forms"));
        Assert.That(referencedAssemblies, Does.Not.Contain("UIAutomationClient"));
    }
}
