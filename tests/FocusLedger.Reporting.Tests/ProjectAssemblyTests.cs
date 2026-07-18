namespace FocusLedger.Reporting.Tests;

public sealed class ProjectAssemblyTests {
    [Test]
    public void ReportingAssembly_HasExpectedIdentity() {
        Assert.That(typeof(ReportingAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("FocusLedger.Reporting"));
    }
}
