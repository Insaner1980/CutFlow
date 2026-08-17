using System.Xml.Linq;
using CutFlow.Controls;
using CutFlow.Models;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class UnsupportedToolTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    [DataRow(EditorTool.Stickers)]
    [DataRow(EditorTool.Effects)]
    [DataRow(EditorTool.Transitions)]
    [DataRow(EditorTool.Captions)]
    [DataRow(EditorTool.Filters)]
    [DataRow(EditorTool.Adjustment)]
    public void UnsupportedEditorTools_CannotImportOrActOnProjectAssets(EditorTool tool)
    {
        Assert.IsFalse(MediaPanel.TryGetMediaImportScope(tool, out _));
        Assert.IsFalse(MediaPanel.CanUseAssetKind(tool, ProjectAssetKind.Video));
        Assert.IsFalse(MediaPanel.CanUseAssetKind(tool, ProjectAssetKind.Image));
        Assert.IsFalse(MediaPanel.CanUseAssetKind(tool, ProjectAssetKind.Audio));
    }

    [TestMethod]
    public void UnsupportedEditorToolPanel_ContainsOnlyInformationalContent()
    {
        var mediaPanel = XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", "CutFlow", "Controls", "MediaPanel.xaml"));
        var unsupportedContent = mediaPanel
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "UnsupportedContent");
        var interactiveElementNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "AutoSuggestBox",
            "Button",
            "GridView",
            "HyperlinkButton",
            "ListView",
            "MenuFlyoutItem",
            "TextBox",
            "ToggleButton"
        };

        Assert.DoesNotContain(
            element => interactiveElementNames.Contains(element.Name.LocalName), unsupportedContent.Descendants(),
            "Unsupported categories must remain informational empty states without focusable actions.");
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
