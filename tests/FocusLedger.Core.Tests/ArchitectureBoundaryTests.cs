namespace FocusLedger.Core.Tests;

public sealed class ArchitectureBoundaryTests {
    [Test]
    public void CoreAssemblyDoesNotReferenceWindowsDesktopAssemblies() {
        string[] referencedAssemblies = typeof(CoreAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();
        // Verify the platform-neutral assembly boundary against prohibited desktop dependencies.
        Assert.That(referencedAssemblies, Does.Not.Contain("System.Windows.Forms"));
        Assert.That(referencedAssemblies, Does.Not.Contain("UIAutomationClient"));
    }
}
