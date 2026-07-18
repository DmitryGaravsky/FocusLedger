namespace FocusLedger.Reporting.Tests;

public sealed class ProjectAssemblyTests {
    [Test]
    public void ReportingAssemblyHasExpectedIdentity() {
        Assert.That(typeof(ReportingAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("FocusLedger.Reporting"));
    }
}
