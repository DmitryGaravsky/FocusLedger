namespace FocusLedger.Windows.Tests;

public sealed class ProjectAssemblyTests {
    [Test]
    public void WindowsAssemblyHasExpectedIdentity() {
        Assert.That(typeof(NativeMethods).Assembly.GetName().Name, Is.EqualTo("FocusLedger.Windows"));
    }
}
