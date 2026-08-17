using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class ArchitectureConfigurationTests
{
    [TestMethod]
    public void TestHost_RunsAsX64()
    {
        Assert.AreEqual(Architecture.X64, RuntimeInformation.ProcessArchitecture);
    }

    private static readonly string[] expected = new[] { "x64" };

    [TestMethod]
    public void SolutionAndProjects_ExposeOnlyX64()
    {
        var repositoryRoot = FindRepositoryRoot();
        var solution = XDocument.Load(Path.Combine(repositoryRoot, "CutFlow.slnx"));
        var solutionPlatforms = solution
            .Descendants("Configurations")
            .Elements("Platform")
            .Select(platform => (string?)platform.Attribute("Name"))
            .ToArray();

        Assert.AreSequenceEqual(expected, solutionPlatforms);

        foreach (var projectPath in ProjectPaths(repositoryRoot))
        {
            var properties = XDocument.Load(projectPath)
                .Root!
                .Elements("PropertyGroup")
                .SelectMany(group => group.Elements())
                .GroupBy(property => property.Name.LocalName)
                .ToDictionary(group => group.Key, group => group.Last().Value);

            Assert.AreEqual("x64", properties["Platforms"], projectPath);
            Assert.AreEqual("x64", properties["Platform"], projectPath);
            Assert.AreEqual("x64", properties["PlatformTarget"], projectPath);
            Assert.AreEqual("win-x64", properties["RuntimeIdentifiers"], projectPath);
        }
    }

    [TestMethod]
    public void Projects_RejectUnsupportedPlatformAndRuntimeIdentifier()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectPath in ProjectPaths(repositoryRoot))
        {
            var validationTarget = XDocument.Load(projectPath)
                .Root!
                .Elements("Target")
                .Single(target => (string?)target.Attribute("Name") == "ValidateX64Configuration");
            var errorConditions = validationTarget
                .Elements("Error")
                .Select(error => (string?)error.Attribute("Condition"))
                .ToArray();

            Assert.AreEqual("PrepareForBuild", (string?)validationTarget.Attribute("BeforeTargets"), projectPath);
            Assert.Contains("'$(Platform)' != 'x64'", errorConditions, projectPath);
            Assert.Contains(
                "'$(RuntimeIdentifier)' != '' and '$(RuntimeIdentifier)' != 'win-x64'", errorConditions, projectPath);
        }
    }

    private static string[] ProjectPaths(string repositoryRoot) =>
    [
        Path.Combine(repositoryRoot, "src", "CutFlow", "CutFlow.csproj"),
        Path.Combine(repositoryRoot, "tests", "CutFlow.Tests", "CutFlow.Tests.csproj")
    ];

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
