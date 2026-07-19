using System.Reflection;
using FocusLedger.Core.Abstractions;

namespace FocusLedger.Core.Tests;

public sealed class BuildMetadataTests {
    [Test]
    public void CoreAssemblyContainsSharedVersionMetadata() {
        Assembly assembly = typeof(IActivitySignalSource).Assembly;
        AssemblyProductAttribute? product = assembly.GetCustomAttribute<AssemblyProductAttribute>();
        AssemblyCompanyAttribute? company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        AssemblyCopyrightAttribute? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        AssemblyInformationalVersionAttribute? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        // Verify the shared build metadata as one coherent attribution contract.
        Assert.Multiple(() => {
            Assert.That(product?.Product, Is.EqualTo("FocusLedger"));
            Assert.That(company?.Company, Is.EqualTo("Dmitrii Garavskii"));
            Assert.That(copyright?.Copyright, Is.EqualTo("Copyright © 2026 Dmitrii Garavskii"));
            Assert.That(informationalVersion?.InformationalVersion, Does.StartWith("0.1.0"));
        });
    }
}
