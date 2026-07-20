using System.Text;
using System.Text.Json;
using FocusLedger.Core.Configuration;

namespace FocusLedger.Core.Tests;

public sealed class ConfigurationSerializerTests {
    [Test]
    public void DeserializeThrowsJsonExceptionForMalformedJson() {
        byte[] malformedJson = Encoding.UTF8.GetBytes("{not-valid-json");
        Assert.That(() => ConfigurationSerializer.Deserialize(malformedJson), Throws.TypeOf<JsonException>());
    }
    [Test]
    public void DeserializeReturnsNullForJsonNullLiteral() {
        byte[] jsonNull = Encoding.UTF8.GetBytes("null");
        Assert.That(ConfigurationSerializer.Deserialize(jsonNull), Is.Null);
    }
    [Test]
    public void SerializeRejectsNullConfiguration() {
        Assert.That(() => ConfigurationSerializer.Serialize(null!), Throws.TypeOf<ArgumentNullException>());
    }
}
