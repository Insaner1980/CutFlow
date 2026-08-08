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

    [TestMethod]
    public void PackageAssets_AreCopiedToTheLoosePackageLayout()
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "CutFlow.csproj"));
        var assets = project
            .Root!
            .Elements("ItemGroup")
            .Elements("Content")
            .Where(content => ((string?)content.Attribute("Include"))?.StartsWith("Assets\\", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.IsNotEmpty(assets);
        foreach (var asset in assets)
        {
            Assert.AreEqual(
                "PreserveNewest",
                (string?)asset.Attribute("CopyToOutputDirectory"),
                (string?)asset.Attribute("Include"));
        }
    }

    [TestMethod]
    public void Project_DoesNotConfigureSignedDistributionPackaging()
    {
        var project = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "CutFlow.csproj"));
        var properties = project
            .Root!
            .Elements("PropertyGroup")
            .SelectMany(group => group.Elements())
            .ToDictionary(property => property.Name.LocalName, property => property.Value);

        Assert.AreEqual("true", properties["EnableMsixTooling"]);
        Assert.IsFalse(properties.TryGetValue("GenerateAppxPackageOnBuild", out var generatePackage) &&
            string.Equals(generatePackage, "true", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(properties.TryGetValue("AppxPackageSigningEnabled", out var signingEnabled) &&
            string.Equals(signingEnabled, "true", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(properties.ContainsKey("PackageCertificateKeyFile"));
        Assert.IsFalse(properties.ContainsKey("PackageCertificateThumbprint"));
    }

    [TestMethod]
    public void DevelopmentRegistration_RejectsPackagesOutsideRepositoryBuildOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "scripts",
            "Register-CutFlowDevelopment.ps1"));
        var readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
        var conflictGuard = script.IndexOf("if ($conflictingPackages.Count -gt 0)", StringComparison.Ordinal);
        var registration = script.IndexOf("Add-AppxPackage -Register $manifestPath", StringComparison.Ordinal);

        Assert.IsTrue(conflictGuard >= 0);
        Assert.IsTrue(registration > conflictGuard);
        StringAssert.Contains(script, "InstallLocation");
        StringAssert.Contains(script, "Refusing to register the CutFlow development layout");
        StringAssert.Contains(script, "StringComparison]::OrdinalIgnoreCase");
        StringAssert.Contains(readme, ".\\scripts\\Register-CutFlowDevelopment.ps1 -Configuration Release");
        StringAssert.Contains(readme, "not a signed installer or Microsoft Store package");
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
