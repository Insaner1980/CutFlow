using CutFlow.Utilities;

namespace CutFlow.Tests;

[TestClass]
public sealed class SourceFileRevealTests
{
    [TestMethod]
    public void ExistingPathWithSpacesUnicodeAndShellCharacters_UsesOneLiteralExplorerArgument()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CutFlow reveal äö {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var sourcePath = Path.Combine(directory, "clip & (test), [å] #1.mp4");
            File.WriteAllText(sourcePath, "media");

            Assert.IsTrue(SourceFileReveal.TryCreateStartInfo(sourcePath, out var startInfo));
            Assert.IsNotNull(startInfo);
            Assert.AreEqual(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                startInfo.FileName);
            Assert.IsFalse(startInfo.UseShellExecute);
            Assert.HasCount(1, startInfo.ArgumentList);
            Assert.AreEqual($"/select,{Path.GetFullPath(sourcePath)}", startInfo.ArgumentList[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void UncPath_IsPassedAsOneLiteralExplorerArgument()
    {
        const string sourcePath = @"\\server name\share ä\clip & (test), [å] #1.mp4";

        Assert.IsTrue(SourceFileReveal.TryCreateStartInfo(sourcePath, _ => true, out var startInfo));
        Assert.IsNotNull(startInfo);
        Assert.HasCount(1, startInfo.ArgumentList);
        Assert.AreEqual($"/select,{sourcePath}", startInfo.ArgumentList[0]);
    }

    [TestMethod]
    public void MissingOrInaccessiblePath_IsRejectedAtInvocationTime()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"disappeared-{Guid.NewGuid():N}.mp4");

        File.WriteAllText(sourcePath, "media");
        File.Delete(sourcePath);

        Assert.IsFalse(SourceFileReveal.TryCreateStartInfo(sourcePath, out var missingStartInfo));
        Assert.IsNull(missingStartInfo);
        Assert.IsFalse(SourceFileReveal.TryCreateStartInfo(sourcePath, _ => false, out var inaccessibleStartInfo));
        Assert.IsNull(inaccessibleStartInfo);
    }

    [TestMethod]
    public void RelativeAndDevicePaths_AreRejected()
    {
        Assert.IsFalse(SourceFileReveal.TryCreateStartInfo(@"media\clip.mp4", _ => true, out _));
        Assert.IsFalse(SourceFileReveal.TryCreateStartInfo(@"\\?\C:\media\clip.mp4", _ => true, out _));
        Assert.IsFalse(SourceFileReveal.TryCreateStartInfo(@"\\.\C:\media\clip.mp4", _ => true, out _));
    }
}
