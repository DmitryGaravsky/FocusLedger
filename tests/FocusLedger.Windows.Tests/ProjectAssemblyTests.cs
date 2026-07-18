namespace FocusLedger.Windows.Tests;

public sealed class ProjectAssemblyTests {
    [Test]
    public void WindowsAssembly_HasExpectedIdentity() {
        Assert.That(typeof(WindowsAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("FocusLedger.Windows"));
    }
}
