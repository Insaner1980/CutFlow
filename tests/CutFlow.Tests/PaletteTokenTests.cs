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
        Assert.AreEqual("{StaticResource AccentTextBrush}", (string?)label.Attribute("Foreground"));
    }

    [TestMethod]
    public void Palette_UsesWindowsSystemColorsInHighContrast()
    {
        var theme = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Styles",
            "ThemeResources.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var highContrast = theme
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "ResourceDictionary" &&
                (string?)element.Attribute(xaml + "Key") == "HighContrast");
        var mappings = highContrast
            .Elements()
            .ToDictionary(
                element => (string)element.Attribute(xaml + "Key")!,
                element => (string?)element.Attribute("ResourceKey"));

        Assert.AreEqual("SystemColorWindowColor", mappings["WindowBackgroundColor"]);
        Assert.AreEqual("SystemColorWindowTextColor", mappings["TextPrimaryColor"]);
        Assert.AreEqual("SystemColorHighlightColor", mappings["AccentColor"]);
        Assert.AreEqual("SystemColorHighlightTextColor", mappings["AccentTextColor"]);

        var brushes = theme
            .Root!
            .Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToList();
        Assert.IsNotEmpty(brushes);
        Assert.IsTrue(brushes.All(brush => ((string?)brush.Attribute("Color"))?.StartsWith("{ThemeResource ", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public void DenseTextContainers_GrowInsteadOfClippingScaledText()
    {
        var root = FindRepositoryRoot();
        var home = XDocument.Load(Path.Combine(root, "src", "CutFlow", "Views", "HomeView.xaml"));
        var editor = XDocument.Load(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml"));
        var media = XDocument.Load(Path.Combine(root, "src", "CutFlow", "Controls", "MediaPanel.xaml"));

        AssertFlexibleTitleBar(home);
        AssertFlexibleTitleBar(editor);

        var projectCard = home
            .Descendants()
            .Single(element => element.Name.LocalName == "Border" && (string?)element.Attribute("Width") == "260");
        Assert.IsNull(projectCard.Attribute("Height"));
        Assert.AreEqual("224", (string?)projectCard.Attribute("MinHeight"));

        var railLabels = editor
            .Descendants()
            .Where(element =>
                element.Name.LocalName == "TextBlock" &&
                element.Ancestors().Any(ancestor =>
                    ancestor.Name.LocalName == "Button" &&
                    (string?)ancestor.Attribute("Style") == "{StaticResource RailButtonStyle}"))
            .ToList();
        Assert.HasCount(9, railLabels);
        Assert.IsTrue(railLabels.All(label => (string?)label.Attribute("TextWrapping") == "Wrap"));

        var itemsPanel = media.Descendants().Single(element => element.Name.LocalName == "ItemsWrapGrid");
        Assert.AreEqual("124", (string?)itemsPanel.Attribute("ItemWidth"));
        Assert.IsNull(itemsPanel.Attribute("ItemHeight"));
        var assetCard = media
            .Descendants()
            .Single(element => element.Name.LocalName == "Border" && (string?)element.Attribute("CanDrag") == "True");
        Assert.AreEqual("154", (string?)assetCard.Attribute("MinHeight"));
    }

    private static void AssertFlexibleTitleBar(XDocument document)
    {
        var firstRow = document
            .Descendants()
            .First(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .First(element => element.Name.LocalName == "RowDefinition");

        Assert.AreEqual("Auto", (string?)firstRow.Attribute("Height"));
        Assert.AreEqual("48", (string?)firstRow.Attribute("MinHeight"));
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
