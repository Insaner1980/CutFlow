using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class PackageActivationConfigurationTests
{
    private static readonly XNamespace Foundation =
        "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace RestrictedCapabilities =
        "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    [TestMethod]
    public void PackageManifest_UsesBuildResolvedFullTrustExecutable()
    {
        var application = LoadPackageManifest()
            .Root!
            .Element(Foundation + "Applications")!
            .Elements(Foundation + "Application")
            .Single();

        Assert.AreEqual("App", (string?)application.Attribute("Id"));
        Assert.AreEqual("$targetnametoken$.exe", (string?)application.Attribute("Executable"));
        Assert.AreEqual("Windows.FullTrustApplication", (string?)application.Attribute("EntryPoint"));
    }

    [TestMethod]
    public void PackageManifest_DeclaresOnlyRunFullTrustCapability()
    {
        var capabilities = LoadPackageManifest()
            .Root!
            .Element(Foundation + "Capabilities")!
            .Elements()
            .ToArray();

        Assert.HasCount(1, capabilities);
        Assert.AreEqual(RestrictedCapabilities + "Capability", capabilities[0].Name);
        Assert.AreEqual("runFullTrust", (string?)capabilities[0].Attribute("Name"));
    }

    private static XDocument LoadPackageManifest() =>
        XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Package.appxmanifest"));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the CutFlow repository root.");
    }
}
