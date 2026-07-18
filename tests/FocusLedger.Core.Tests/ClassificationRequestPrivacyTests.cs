using System.Text.Json;
using FocusLedger.Core.Classification;

namespace FocusLedger.Core.Tests;

public sealed class ClassificationRequestPrivacyTests {
    const string SensitiveTitle = "Secret Customer Meeting with John Smith";
    static ClassificationRequest CreateRequest() {
        ApplicationIdentity application = new("microsoft-teams", "ms-teams.exe", "communication");
        return new ClassificationRequest(application, SensitiveTitle);
    }
    [Test]
    public void ToStringRedactsSensitiveTitle() {
        ClassificationRequest request = CreateRequest();
        // Verify that routine diagnostic rendering cannot expose the transient title.
        Assert.That(request.ToString(), Does.Not.Contain(SensitiveTitle));
    }
    [Test]
    public void JsonSerializationOmitsSensitiveTitle() {
        ClassificationRequest request = CreateRequest();
        // Serialize the full request to exercise the explicit JSON privacy boundary.
        string json = JsonSerializer.Serialize(request);
        // Verify that neither the value nor its sensitive property name enters serialized output.
        Assert.That(json, Does.Not.Contain(SensitiveTitle));
        Assert.That(json, Does.Not.Contain(nameof(ClassificationRequest.RawWindowTitle)));
    }
}
