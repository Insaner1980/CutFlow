using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class MachineIndependentBuildInputsTests
{
    private static readonly string[] FixturePaths =
    [
        @"TestMedia\valid-audio.wav",
        @"TestMedia\valid-image.jpg",
        @"TestMedia\valid-video.mp4"
    ];

    [TestMethod]
    public void NativeMediaFixtures_AreDeclaredRelativeRepositoryContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testProject = XDocument.Load(Path.Combine(
            repositoryRoot,
            "tests",
            "CutFlow.Tests",
            "CutFlow.Tests.csproj"));
        var declaredContent = testProject
            .Root!
            .Elements("ItemGroup")
            .Elements("Content")
            .ToDictionary(
                content => (string)content.Attribute("Include")!,
                content => (string?)content.Attribute("CopyToOutputDirectory"),
                StringComparer.OrdinalIgnoreCase);

        foreach (var fixturePath in FixturePaths)
        {
            Assert.IsFalse(Path.IsPathRooted(fixturePath), fixturePath);
            Assert.AreEqual("PreserveNewest", declaredContent[fixturePath], fixturePath);
            Assert.IsTrue(
                File.Exists(Path.Combine(repositoryRoot, "tests", "CutFlow.Tests", fixturePath)),
                fixturePath);
            Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, fixturePath)), fixturePath);
        }
    }

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
