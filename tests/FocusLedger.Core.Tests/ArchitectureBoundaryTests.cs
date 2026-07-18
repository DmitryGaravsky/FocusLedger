namespace FocusLedger.Core.Tests;

public sealed class ArchitectureBoundaryTests {
    [Test]
    public void CoreAssembly_DoesNotReferenceWindowsDesktopAssemblies() {
        string[] referencedAssemblies = typeof(CoreAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.That(referencedAssemblies, Does.Not.Contain("System.Windows.Forms"));
        Assert.That(referencedAssemblies, Does.Not.Contain("UIAutomationClient"));
    }
}
