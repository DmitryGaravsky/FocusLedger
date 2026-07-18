namespace FocusLedger.Windows.Tests;

public sealed class ProjectAssemblyTests {
    [Test]
    public void WindowsAssemblyHasExpectedIdentity() {
        Assert.That(typeof(WindowsAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("FocusLedger.Windows"));
    }
}
