using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class PaletteTokenTests
{
    [TestMethod]
    public void MissingBadge_UsesPaletteForegroundOnWarningSurface()
    {
        var mediaPanel = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "MediaPanel.xaml"));
        var label = mediaPanel
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TextBlock" &&
                (string?)element.Attribute("Text") == "Missing");
        var badge = label.Parent;

        Assert.IsNotNull(badge);
        Assert.AreEqual("{StaticResource WarningBrush}", (string?)badge.Attribute("Background"));
        Assert.AreEqual("{StaticResource WindowBackgroundBrush}", (string?)label.Attribute("Foreground"));
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
