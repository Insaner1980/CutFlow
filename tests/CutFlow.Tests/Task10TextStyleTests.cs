using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI.Text;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task10TextStyleTests
{
    [TestMethod]
    public async Task SchemaOneTextStyles_MissingFieldsUseDefaultsAndCustomFieldsRoundTrip()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var defaultTextId = Guid.NewGuid();
        var customTextId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Text styles",
              "createdAt": "2026-08-07T12:00:00+00:00",
              "modifiedAt": "2026-08-07T12:00:00+00:00",
              "textItems": [
                { "id": "{{defaultTextId}}", "durationMilliseconds": 3000, "text": "Default" },
                { "id": "{{customTextId}}", "durationMilliseconds": 3000, "text": "Custom", "isItalic": true, "backgroundEnabled": true, "backgroundColor": "#CC102030", "opacity": 0.35 }
              ]
            }
            """);
        var service = new ProjectService(directory.Path);

        var loaded = await service.LoadAsync(id);
        var defaults = loaded.TextItems.Single(item => item.Id == defaultTextId);
        var custom = loaded.TextItems.Single(item => item.Id == customTextId);

        Assert.IsFalse(defaults.IsItalic);
        Assert.IsFalse(defaults.BackgroundEnabled);
        Assert.AreEqual(TextTimelineItem.DefaultOpacity, defaults.Opacity);
        Assert.IsTrue(custom.IsItalic);
        Assert.IsTrue(custom.BackgroundEnabled);
        Assert.AreEqual(0.35, custom.Opacity, 0.0001);

        await service.SaveAsync(loaded);
        var roundTrip = (await service.LoadAsync(id)).TextItems.Single(item => item.Id == customTextId);
        Assert.IsTrue(roundTrip.IsItalic);
        Assert.IsTrue(roundTrip.BackgroundEnabled);
        Assert.AreEqual("#CC102030", roundTrip.BackgroundColor);
        Assert.AreEqual(0.35, roundTrip.Opacity, 0.0001);
    }

    [TestMethod]
    public async Task SchemaOneTextStyles_FontFamiliesNormalizeToSupportedCanonicalChoices()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Font families",
              "textItems": [
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000 },
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000, "fontFamily": null },
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000, "fontFamily": "   " },
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000, "fontFamily": "Local Custom Font" },
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000, "fontFamily": "aRiAl" },
                { "id": "{{Guid.NewGuid()}}", "durationMilliseconds": 3000, "fontFamily": "  Georgia  " }
              ]
            }
            """);

        var loaded = await new ProjectService(directory.Path).LoadAsync(id);

        CollectionAssert.AreEqual(
            new[] { "Segoe UI", "Segoe UI", "Segoe UI", "Segoe UI", "Arial", "Georgia" },
            loaded.TextItems.Select(item => item.FontFamily).ToArray());
        Assert.IsTrue(loaded.TextItems.All(item => TextStyle.SupportedFontFamilies.Contains(item.FontFamily)));
    }

    [TestMethod]
    [DataRow(-2d, 0d)]
    [DataRow(2d, 1d)]
    public async Task SchemaOneTextStyles_OutOfRangeOpacityIsClamped(double opacity, double expected)
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = ProjectDocument.CreateNew("Opacity", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Opacity", Opacity = opacity });

        await service.SaveAsync(project);

        Assert.AreEqual(expected, (await service.LoadAsync(project.Id)).TextItems.Single().Opacity);
    }

    [TestMethod]
    public void TextStyleSetters_AreUndoableAndRedoable()
    {
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Style" };
        var viewModel = CreateViewModel(item);

        Assert.IsTrue(viewModel.SetTextItalic(item.Id, true));
        Assert.IsTrue(viewModel.SetTextBackgroundEnabled(item.Id, true));
        Assert.IsTrue(viewModel.SetTextOpacity(item.Id, 0.4));
        Assert.IsTrue(viewModel.Project.TextItems.Single().IsItalic);
        Assert.IsTrue(viewModel.Project.TextItems.Single().BackgroundEnabled);
        Assert.AreEqual(0.4, viewModel.Project.TextItems.Single().Opacity, 0.0001);

        viewModel.Undo();
        Assert.AreEqual(TextTimelineItem.DefaultOpacity, viewModel.Project.TextItems.Single().Opacity);
        viewModel.Undo();
        Assert.IsFalse(viewModel.Project.TextItems.Single().BackgroundEnabled);
        viewModel.Undo();
        Assert.IsFalse(viewModel.Project.TextItems.Single().IsItalic);

        viewModel.Redo();
        viewModel.Redo();
        viewModel.Redo();
        Assert.IsTrue(viewModel.Project.TextItems.Single().IsItalic);
        Assert.IsTrue(viewModel.Project.TextItems.Single().BackgroundEnabled);
        Assert.AreEqual(0.4, viewModel.Project.TextItems.Single().Opacity, 0.0001);
    }

    [TestMethod]
    public void SetTextBold_UsesNamedBoldWeightAndRestoresDefaultWeight()
    {
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Bold" };
        var viewModel = CreateViewModel(item);

        Assert.IsTrue(viewModel.SetTextBold(item.Id, true));
        Assert.AreEqual(TextTimelineItem.BoldFontWeight, viewModel.Project.TextItems.Single().FontWeight);
        Assert.IsFalse(viewModel.SetTextBold(item.Id, true));
        Assert.IsTrue(viewModel.SetTextBold(item.Id, false));
        Assert.AreEqual(TextTimelineItem.DefaultFontWeight, viewModel.Project.TextItems.Single().FontWeight);
    }

    [TestMethod]
    public void TextAxisSetters_ClampOneCoordinateWithoutChangingTheOther()
    {
        var item = new TextTimelineItem { Id = Guid.NewGuid(), NormalizedX = 0.25, NormalizedY = 0.75 };
        var viewModel = CreateViewModel(item);

        Assert.IsTrue(viewModel.SetTextHorizontalPosition(item.Id, 2));
        Assert.AreEqual(1d, viewModel.Project.TextItems.Single().NormalizedX);
        Assert.AreEqual(0.75, viewModel.Project.TextItems.Single().NormalizedY);
        Assert.IsTrue(viewModel.SetTextVerticalPosition(item.Id, -1));
        Assert.AreEqual(1d, viewModel.Project.TextItems.Single().NormalizedX);
        Assert.AreEqual(0d, viewModel.Project.TextItems.Single().NormalizedY);
    }

    [TestMethod]
    public void UnknownTextAlignment_IsRejectedByEditingAndRenderedAsCenter()
    {
        var item = new TextTimelineItem { Id = Guid.NewGuid() };
        var viewModel = CreateViewModel(item);

        Assert.IsFalse(viewModel.SetTextAlignment(item.Id, (TextHorizontalAlignment)99));
        Assert.AreEqual(TextHorizontalAlignment.Center, viewModel.Project.TextItems.Single().Alignment);

        item.Alignment = (TextHorizontalAlignment)99;
        Assert.AreEqual(Microsoft.UI.Xaml.TextAlignment.Center, TextStyle.ResolveAlignment(item));
    }

    [TestMethod]
    public void ResetTextStyle_IsOneUndoableEditAndPreservesContentTimingAndPosition()
    {
        var item = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Keep me",
            StartMilliseconds = 1_200,
            DurationMilliseconds = 4_500,
            FontFamily = "Georgia",
            FontSize = 110,
            FontWeight = TextTimelineItem.BoldFontWeight,
            IsItalic = true,
            TextColor = "#FF102030",
            BackgroundColor = "#CC405060",
            BackgroundEnabled = true,
            Opacity = 0.5,
            Alignment = TextHorizontalAlignment.Right,
            NormalizedX = 0.2,
            NormalizedY = 0.8
        };
        var viewModel = CreateViewModel(item);

        Assert.IsTrue(viewModel.ResetTextStyle(item.Id));

        var reset = viewModel.Project.TextItems.Single();
        Assert.AreEqual("Keep me", reset.Text);
        Assert.AreEqual(1_200L, reset.StartMilliseconds);
        Assert.AreEqual(4_500L, reset.DurationMilliseconds);
        Assert.AreEqual(0.2, reset.NormalizedX);
        Assert.AreEqual(0.8, reset.NormalizedY);
        Assert.AreEqual(TextTimelineItem.DefaultFontFamily, reset.FontFamily);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, reset.FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontWeight, reset.FontWeight);
        Assert.IsFalse(reset.IsItalic);
        Assert.AreEqual(TextTimelineItem.DefaultTextColor, reset.TextColor);
        Assert.AreEqual(TextTimelineItem.DefaultBackgroundColor, reset.BackgroundColor);
        Assert.IsFalse(reset.BackgroundEnabled);
        Assert.AreEqual(TextTimelineItem.DefaultOpacity, reset.Opacity);
        Assert.AreEqual(TextHorizontalAlignment.Center, reset.Alignment);

        viewModel.Undo();
        var restored = viewModel.Project.TextItems.Single();
        Assert.AreEqual("Georgia", restored.FontFamily);
        Assert.IsTrue(restored.IsItalic);
        Assert.IsTrue(restored.BackgroundEnabled);
        Assert.AreEqual(0.5, restored.Opacity);
    }

    [TestMethod]
    public void DuplicateSelection_CopiesItalicBackgroundToggleAndOpacity()
    {
        var source = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Duplicate",
            IsItalic = true,
            BackgroundEnabled = true,
            BackgroundColor = "#CC112233",
            Opacity = 0.45
        };
        var project = ProjectDocument.CreateNew("Duplicate", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(source);

        Assert.IsTrue(TimelineEditingService.DuplicateSelection(
            project,
            new EditorSelection(EditorSelectionKind.TextItem, source.Id),
            out var selection));

        var copy = project.TextItems.Single(item => item.Id == selection.ItemId);
        Assert.IsTrue(copy.IsItalic);
        Assert.IsTrue(copy.BackgroundEnabled);
        Assert.AreEqual("#CC112233", copy.BackgroundColor);
        Assert.AreEqual(0.45, copy.Opacity, 0.0001);
    }

    [TestMethod]
    public void RenderKeys_ChangeForEveryPixelAffectingTextStyle()
    {
        var project = ProjectDocument.CreateNew("Render keys", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Render", DurationMilliseconds = 3_000 };
        project.TextItems.Add(item);
        var selection = new EditorSelection(EditorSelectionKind.TextItem, item.Id);
        var originalHash = TextOverlayRenderer.CalculateStyleHash(project, item);
        var originalLiveKey = LiveTextRenderKey.Create(project, selection, 100, 1280, 720);

        item.IsItalic = true;
        Assert.AreNotEqual(originalHash, TextOverlayRenderer.CalculateStyleHash(project, item));
        Assert.AreNotEqual(originalLiveKey, LiveTextRenderKey.Create(project, selection, 100, 1280, 720));
        item.IsItalic = false;

        item.BackgroundEnabled = true;
        Assert.AreNotEqual(originalHash, TextOverlayRenderer.CalculateStyleHash(project, item));
        Assert.AreNotEqual(originalLiveKey, LiveTextRenderKey.Create(project, selection, 100, 1280, 720));
        item.BackgroundEnabled = false;

        item.Opacity = 0.4;
        Assert.AreNotEqual(originalHash, TextOverlayRenderer.CalculateStyleHash(project, item));
        Assert.AreNotEqual(originalLiveKey, LiveTextRenderKey.Create(project, selection, 100, 1280, 720));
    }

    [TestMethod]
    public void EquivalentFontFamilyInputs_UseSameLiveAndExportRenderKeys()
    {
        var project = ProjectDocument.CreateNew("Font render keys", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Render", DurationMilliseconds = 3_000 };
        project.TextItems.Add(item);
        var selection = new EditorSelection(EditorSelectionKind.TextItem, item.Id);
        var cases = new (string Canonical, string? Equivalent)[]
        {
            ("Segoe UI", null),
            ("Segoe UI", "   "),
            ("Segoe UI", "Local Custom Font"),
            ("Arial", "aRiAl"),
            ("Georgia", "  Georgia  ")
        };

        foreach (var (canonical, equivalent) in cases)
        {
            item.FontFamily = canonical;
            var canonicalHash = TextOverlayRenderer.CalculateStyleHash(project, item);
            var canonicalLiveKey = LiveTextRenderKey.Create(project, selection, 100, 1280, 720);

            item.FontFamily = equivalent!;

            Assert.AreEqual(canonical, TextStyle.NormalizeFontFamily(equivalent));
            Assert.AreEqual(canonicalHash, TextOverlayRenderer.CalculateStyleHash(project, item));
            Assert.AreEqual(canonicalLiveKey, LiveTextRenderKey.Create(project, selection, 100, 1280, 720));
        }
    }

    [TestMethod]
    public void TextVisualStyle_NormalizesFontAndMapsItalicBackgroundAndOpacity()
    {
        var item = new TextTimelineItem
        {
            FontFamily = "Missing Font",
            IsItalic = true,
            BackgroundEnabled = false,
            BackgroundColor = "#CC112233",
            Opacity = 2
        };

        Assert.AreEqual(TextTimelineItem.DefaultFontFamily, TextStyle.NormalizeFontFamily(item.FontFamily));
        Assert.AreEqual(FontStyle.Italic, TextStyle.ResolveFontStyle(item));
        Assert.AreEqual(TextTimelineItem.DefaultBackgroundColor, TextStyle.ResolveBackgroundColor(item));
        Assert.AreEqual(1d, TextStyle.ClampOpacity(item.Opacity));
    }

    [TestMethod]
    [DataRow(false, 0d, 0d)]
    [DataRow(true, 18d, 8d)]
    public void TextContainerPadding_UsesTheLiveAndExportContract(bool backgroundEnabled, double horizontal, double vertical)
    {
        var padding = TextStyle.ResolveContainerPadding(new TextTimelineItem
        {
            BackgroundEnabled = backgroundEnabled
        });

        Assert.AreEqual(horizontal, padding.Left);
        Assert.AreEqual(vertical, padding.Top);
        Assert.AreEqual(horizontal, padding.Right);
        Assert.AreEqual(vertical, padding.Bottom);
    }

    [TestMethod]
    public void MinimalLabelPreset_ExplicitlyEnablesItsBackground()
    {
        var item = TextPresetFactory.Create(TextPreset.MinimalLabel, 0);

        Assert.IsTrue(item.BackgroundEnabled);
        Assert.AreNotEqual(TextTimelineItem.DefaultBackgroundColor, item.BackgroundColor);
    }

    private static EditorViewModel CreateViewModel(TextTimelineItem item)
    {
        var project = ProjectDocument.CreateNew("Text", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(item);
        return new EditorViewModel(project);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
