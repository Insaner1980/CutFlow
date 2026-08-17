using CutFlow.Models;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Buffers.Binary;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed partial class Task10TextTests
{
    [TestMethod]
    [DataRow(TextPreset.Default, "Text", 64d, 0.5d, 0.5d)]
    [DataRow(TextPreset.Title, "Title", 96d, 0.5d, 0.22d)]
    [DataRow(TextPreset.Subtitle, "Subtitle", 52d, 0.5d, 0.78d)]
    [DataRow(TextPreset.MinimalLabel, "Label", 38d, 0.5d, 0.88d)]
    public void AddText_FourPresetsCreateConcreteSelectedThreeSecondItems(
        TextPreset preset, string expectedText, double expectedSize, double expectedX, double expectedY)
    {
        var project = ProjectDocument.CreateNew("Text", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), Text = "Existing", DurationMilliseconds = 2_000 });
        var viewModel = new EditorViewModel(project);
        viewModel.Seek(1_250);

        var item = viewModel.AddText(preset);

        Assert.AreEqual(1_250L, item.StartMilliseconds);
        Assert.AreEqual(3_000L, item.DurationMilliseconds);
        Assert.AreEqual(expectedText, item.Text);
        Assert.AreEqual(expectedSize, item.FontSize);
        Assert.AreEqual(expectedX, item.NormalizedX, 0.0001);
        Assert.AreEqual(expectedY, item.NormalizedY, 0.0001);
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.FontFamily));
        Assert.IsGreaterThan(0, item.FontWeight);
        Assert.IsTrue(item.TextColor.StartsWith('#'));
        Assert.AreEqual(EditorSelectionKind.TextItem, viewModel.Selection.Kind);
        Assert.AreEqual(item.Id, viewModel.Selection.ItemId);
    }

    [TestMethod]
    public async Task SchemaOneTextStyles_DefaultRoundTripAndUnsafeValuesNormalize()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            { "schemaVersion": 1, "id": "{{id}}", "name": "Old text", "createdAt": "2026-08-04T12:00:00+00:00", "modifiedAt": "2026-08-04T12:00:00+00:00", "textItems": [{ "id": "{{textId}}", "startMilliseconds": 0, "durationMilliseconds": 3000, "text": "Old", "fontFamily": "", "fontSize": 0, "fontWeight": 0, "textColor": "bad", "backgroundColor": "bad", "normalizedX": 4, "normalizedY": -2 }] }
            """, TestContext.CancellationToken);
        var service = new Services.ProjectService(directory.Path);

        var loaded = await service.LoadAsync(id, TestContext.CancellationToken);
        var item = loaded.TextItems.Single();

        Assert.AreEqual(TextTimelineItem.DefaultFontFamily, item.FontFamily);
        Assert.AreEqual(TextTimelineItem.DefaultFontSize, item.FontSize);
        Assert.AreEqual(TextTimelineItem.DefaultFontWeight, item.FontWeight);
        Assert.AreEqual(TextTimelineItem.DefaultTextColor, item.TextColor);
        Assert.AreEqual(TextTimelineItem.DefaultBackgroundColor, item.BackgroundColor);
        Assert.AreEqual(1d, item.NormalizedX);
        Assert.AreEqual(0d, item.NormalizedY);

        item.FontFamily = "Arial";
        item.FontSize = 71;
        item.FontWeight = 700;
        item.TextColor = "#FF112233";
        item.BackgroundColor = "#80102030";
        item.Alignment = TextHorizontalAlignment.Right;
        item.NormalizedX = 0.25;
        item.NormalizedY = 0.75;
        await service.SaveAsync(loaded, TestContext.CancellationToken);
        var roundTrip = (await service.LoadAsync(id, TestContext.CancellationToken)).TextItems.Single();

        Assert.AreEqual("Arial", roundTrip.FontFamily);
        Assert.AreEqual(71d, roundTrip.FontSize);
        Assert.AreEqual(700, roundTrip.FontWeight);
        Assert.AreEqual("#FF112233", roundTrip.TextColor);
        Assert.AreEqual("#80102030", roundTrip.BackgroundColor);
        Assert.AreEqual(TextHorizontalAlignment.Right, roundTrip.Alignment);
        Assert.AreEqual(0.25, roundTrip.NormalizedX);
        Assert.AreEqual(0.75, roundTrip.NormalizedY);
    }

    [TestMethod]
    public async Task LoadAsync_NormalizesTextCoordinateJsonNumberEdges()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "textItems": [
                { "id": "{{Guid.NewGuid()}}", "normalizedX": -0, "normalizedY": -1 },
                { "id": "{{Guid.NewGuid()}}", "normalizedX": 2, "normalizedY": 1e309 },
                { "id": "{{Guid.NewGuid()}}", "normalizedX": -1e309, "normalizedY": 1e-999 }
              ]
            }
            """, TestContext.CancellationToken);

        var items = (await new Services.ProjectService(directory.Path).LoadAsync(id, TestContext.CancellationToken)).TextItems;

        Assert.AreEqual(0L, BitConverter.DoubleToInt64Bits(items[0].NormalizedX));
        Assert.AreEqual(0d, items[0].NormalizedY);
        Assert.AreEqual(1d, items[1].NormalizedX);
        Assert.AreEqual(0.5d, items[1].NormalizedY);
        Assert.AreEqual(0.5d, items[2].NormalizedX);
        Assert.AreEqual(0d, items[2].NormalizedY);
    }

    [TestMethod]
    public async Task LoadAsync_NormalizesValidArgbColorCasingConsistently()
    {
        using var directory = new TemporaryDirectory();
        var id = Guid.NewGuid();
        var textId = Guid.NewGuid();
        var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "Projects", id.ToString("D")));
        await File.WriteAllTextAsync(Path.Combine(projectDirectory.FullName, "project.json"), $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "name": "Color casing",
              "settings": { "backgroundColor": "#FfaBcDeF" },
              "textItems": [
                {
                  "id": "{{textId}}",
                  "durationMilliseconds": 3000,
                  "textColor": "#7faabbcc",
                  "backgroundColor": "#00ddeeff"
                }
              ]
            }
            """, TestContext.CancellationToken);

        var loaded = await new Services.ProjectService(directory.Path).LoadAsync(id, TestContext.CancellationToken);
        var item = loaded.TextItems.Single();

        Assert.AreEqual("#FFABCDEF", loaded.Settings.BackgroundColor);
        Assert.AreEqual("#7FAABBCC", item.TextColor);
        Assert.AreEqual("#00DDEEFF", item.BackgroundColor);
    }

    [TestMethod]
    public void SetTextPosition_ClampsCommitsOneUndoAndNoOpCreatesNoHistory()
    {
        var project = ProjectDocument.CreateNew("Drag", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), NormalizedX = 0.5, NormalizedY = 0.5 };
        project.TextItems.Add(item);
        var viewModel = new EditorViewModel(project);

        Assert.IsFalse(viewModel.SetTextPosition(item.Id, 0.5, 0.5));
        Assert.IsFalse(viewModel.CanUndo);
        Assert.IsTrue(viewModel.SetTextPosition(item.Id, 2, -1));
        Assert.AreEqual(1d, viewModel.Project.TextItems.Single().NormalizedX);
        Assert.AreEqual(0d, viewModel.Project.TextItems.Single().NormalizedY);
        Assert.IsTrue(viewModel.CanUndo);
        viewModel.Undo();
        Assert.AreEqual(0.5, viewModel.Project.TextItems.Single().NormalizedX);
        Assert.AreEqual(0.5, viewModel.Project.TextItems.Single().NormalizedY);
    }

    [TestMethod]
    public void DuplicateSelection_CopiesEveryTextStyleField()
    {
        var project = ProjectDocument.CreateNew("Duplicate", DateTimeOffset.UnixEpoch);
        var source = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Styled",
            StartMilliseconds = 200,
            DurationMilliseconds = 1_500,
            FontFamily = "Arial",
            FontSize = 73,
            FontWeight = 700,
            TextColor = "#FFABCDEF",
            BackgroundColor = "#80443322",
            Alignment = TextHorizontalAlignment.Left,
            NormalizedX = 0.2,
            NormalizedY = 0.7
        };
        project.TextItems.Add(source);

        Assert.IsTrue(Services.TimelineEditingService.DuplicateSelection(
            project, new EditorSelection(EditorSelectionKind.TextItem, source.Id), out var selection));

        var copy = project.TextItems.Single(item => item.Id == selection.ItemId);
        Assert.AreEqual(source.FontFamily, copy.FontFamily);
        Assert.AreEqual(source.FontSize, copy.FontSize);
        Assert.AreEqual(source.FontWeight, copy.FontWeight);
        Assert.AreEqual(source.TextColor, copy.TextColor);
        Assert.AreEqual(source.BackgroundColor, copy.BackgroundColor);
        Assert.AreEqual(source.Alignment, copy.Alignment);
        Assert.AreEqual(source.NormalizedX, copy.NormalizedX);
        Assert.AreEqual(source.NormalizedY, copy.NormalizedY);
    }

    [TestMethod]
    public void StyleHashChangesForPixelsAndDimensionsButNotTiming()
    {
        var project = ProjectDocument.CreateNew("Hash", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "Hello",
            BackgroundEnabled = true
        };
        var original = Services.TextOverlayRenderer.CalculateStyleHash(project, item);
        Assert.AreEqual(
            "4ff414ed4469676c",
            original,
            "Update the expected hash only when the raster contract or cache format intentionally changes.");
        item.StartMilliseconds = 8_000;
        item.DurationMilliseconds = 500;
        Assert.AreEqual(original, Services.TextOverlayRenderer.CalculateStyleHash(project, item));

        var pixelChanges = new Action<ProjectDocument, TextTimelineItem>[]
        {
            (_, value) => value.Text = "Changed",
            (_, value) => value.FontFamily = "Arial",
            (_, value) => value.FontSize = 72,
            (_, value) => value.FontWeight = 700,
            (_, value) => value.IsItalic = true,
            (_, value) => value.TextColor = "#FFFF0000",
            (_, value) => value.BackgroundColor = "#FF0000FF",
            (_, value) => value.BackgroundEnabled = false,
            (_, value) => value.Opacity = 0.5,
            (_, value) => value.Alignment = TextHorizontalAlignment.Left,
            (_, value) => value.NormalizedX = 0.6,
            (_, value) => value.NormalizedY = 0.6,
            (value, _) => value.Settings.Width = 1280,
            (value, _) => value.Settings.Height = 720
        };
        foreach (var change in pixelChanges)
        {
            var changedProject = ProjectDocumentCloner.Clone(project);
            var changedItem = changedProject.TextItems.SingleOrDefault(candidate => candidate.Id == item.Id) ?? new TextTimelineItem
            {
                Id = item.Id,
                Text = item.Text,
                BackgroundEnabled = item.BackgroundEnabled
            };
            change(changedProject, changedItem);
            Assert.AreNotEqual(original, Services.TextOverlayRenderer.CalculateStyleHash(changedProject, changedItem));
        }

        item.NormalizedX = 2;
        var clampedPositionHash = Services.TextOverlayRenderer.CalculateStyleHash(project, item);
        item.NormalizedX = 1;
        Assert.AreEqual(clampedPositionHash, Services.TextOverlayRenderer.CalculateStyleHash(project, item));
        Assert.MatchesRegex(
            new System.Text.RegularExpressions.Regex($"^{item.Id:D}-[0-9a-f]{{16}}\\.png$"),
            Services.TextOverlayRenderer.GetCacheFileName(project, item));
    }

    [TestMethod]
    public void RendererCachePath_IsProjectLocalTextOverlayPath()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Cache", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Cached" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);

        var path = renderer.GetCachePath(project, item);

        Assert.AreEqual(
            Path.Combine(directory.Path, "cache", "text-overlays", Services.TextOverlayRenderer.GetCacheFileName(project, item)),
            path);
    }

    [TestMethod]
    public async Task TextOverlayCache_LinkedDirectoryRejectsWithoutOverwritingTarget()
    {
        using var directory = new TemporaryDirectory();
        var projectRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "project"));
        var cacheRoot = Directory.CreateDirectory(Path.Combine(projectRoot.FullName, "cache"));
        var externalDirectory = Directory.CreateDirectory(Path.Combine(directory.Path, "external"));
        Directory.CreateSymbolicLink(
            Path.Combine(cacheRoot.FullName, "text-overlays"),
            externalDirectory.FullName);
        var project = ProjectDocument.CreateNew("Linked text cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Linked" };
        var externalPath = Path.Combine(
            externalDirectory.FullName,
            Services.TextOverlayRenderer.GetCacheFileName(project, item));
        await File.WriteAllTextAsync(externalPath, "preserve external overlay", TestContext.CancellationToken);
        var renderCalled = false;
        var renderer = new Services.TextOverlayRenderer(projectRoot.FullName);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => renderer.GetOrRenderAsync(
            project,
            item,
            async (path, cancellationToken) =>
            {
                renderCalled = true;
                await WriteTransparentPngAsync(path, 1, 1, cancellationToken);
            },
            CancellationToken.None));

        Assert.IsFalse(renderCalled);
        Assert.AreEqual("preserve external overlay", await File.ReadAllTextAsync(externalPath, TestContext.CancellationToken));
    }

    [TestMethod]
    public void RenderSize_CompensatesForOneHundredFiftyPercentRasterizationScale()
    {
        var size = Services.TextOverlayRenderer.CalculateRenderSize(1920, 1080, 1.5);

        Assert.AreEqual(1280, size.Width);
        Assert.AreEqual(720, size.Height);
    }

    [TestMethod]
    [DataRow(1920, 1080)]
    [DataRow(1080, 1920)]
    [DataRow(1080, 1080)]
    public async Task EncodePngAsync_ResizesRasterizationRoundingToExactProjectDimensions(
        int outputWidth,
        int outputHeight)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "scaled.png");
        var renderSize = Services.TextOverlayRenderer.CalculateRenderSize(outputWidth, outputHeight, 3.5);
        var rasterWidth = (int)Math.Ceiling(renderSize.Width * 3.5);
        var rasterHeight = (int)Math.Ceiling(renderSize.Height * 3.5);

        Assert.IsTrue(rasterWidth != outputWidth || rasterHeight != outputHeight);

        await Services.TextOverlayRenderer.EncodePngAsync(
            path,
            new byte[checked(rasterWidth * rasterHeight * 4)],
            rasterWidth,
            rasterHeight,
            outputWidth,
            outputHeight,
            CancellationToken.None);

        var dimensions = await ReadPngDimensionsAsync(path);
        Assert.AreEqual((uint)outputWidth, dimensions.Width);
        Assert.AreEqual((uint)outputHeight, dimensions.Height);
    }

    [TestMethod]
    public async Task CacheInvalidDimensions_RegeneratesExactProjectSize()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Invalid cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 1, 1, TestContext.CancellationToken);
        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            async (temporaryPath, cancellationToken) =>
            {
                renderCount++;
                await WriteTransparentPngAsync(temporaryPath, 2, 2, cancellationToken);
            },
            CancellationToken.None);

        var dimensions = await ReadPngDimensionsAsync(result);
        Assert.AreEqual(1, renderCount);
        Assert.AreEqual(2u, dimensions.Width);
        Assert.AreEqual(2u, dimensions.Height);
    }

    [TestMethod]
    public async Task CacheExactDimensions_ReusesPngWithoutRendering()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Valid cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 2, 2, TestContext.CancellationToken);
        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            (_, _) =>
            {
                renderCount++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(cachePath, result);
        Assert.AreEqual(0, renderCount);
    }

    [TestMethod]
    [DataRow(PngCorruption.TruncatedIdat)]
    [DataRow(PngCorruption.IhdrCrc)]
    [DataRow(PngCorruption.IdatCrc)]
    [DataRow(PngCorruption.IendCrc)]
    [DataRow(PngCorruption.OversizedIhdrLength)]
    public async Task CacheMalformedPng_RegeneratesInsteadOfReusing(PngCorruption corruption)
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Corrupt cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 2, 2, TestContext.CancellationToken);
        CorruptPng(cachePath, corruption);
        if (corruption is PngCorruption.IhdrCrc or PngCorruption.IdatCrc or PngCorruption.IendCrc)
        {
            Assert.HasCount(16, await ReadPngPixelsAsync(cachePath), "WIC should still decode the CRC-corrupt fixture.");
        }

        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            async (temporaryPath, cancellationToken) =>
            {
                renderCount++;
                await WriteTransparentPngAsync(temporaryPath, 2, 2, cancellationToken);
            },
            CancellationToken.None);

        var dimensions = await ReadPngDimensionsAsync(result);
        Assert.AreEqual(1, renderCount);
        Assert.AreEqual(2u, dimensions.Width);
        Assert.AreEqual(2u, dimensions.Height);
    }

    [TestMethod]
    [DataRow(PngStructureViolation.UnknownCriticalChunk)]
    [DataRow(PngStructureViolation.PlteAfterIdat)]
    [DataRow(PngStructureViolation.NonConsecutiveIdat)]
    [DataRow(PngStructureViolation.DuplicatePlte)]
    public async Task CacheCriticalStructureViolation_RegeneratesInsteadOfReusing(
        PngStructureViolation violation)
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Invalid PNG structure", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 2, 2, TestContext.CancellationToken);
        ApplyPngStructureViolation(cachePath, violation);
        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            async (temporaryPath, cancellationToken) =>
            {
                renderCount++;
                await WriteTransparentPngAsync(temporaryPath, 2, 2, cancellationToken);
            },
            CancellationToken.None);

        Assert.AreEqual(1, renderCount);
        Assert.AreEqual((2u, 2u), await ReadPngDimensionsAsync(result));
    }

    [TestMethod]
    public async Task CacheUnknownAncillaryChunk_RemainsReusable()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Ancillary PNG chunk", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 2, 2, TestContext.CancellationToken);
        InsertPngChunk(cachePath, "vpAg"u8, [], "IDAT"u8);
        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            (_, _) =>
            {
                renderCount++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(0, renderCount);
        Assert.AreEqual(cachePath, result);
    }

    [TestMethod]
    [DataRow(9, 1)]
    [DataRow(12, 2)]
    public async Task CacheUnsupportedColorOrInterlace_RegeneratesInsteadOfFailing(
        int ihdrDataOffset,
        int unsupportedValue)
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Unsupported PNG encoding", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 2;
        project.Settings.Height = 2;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Pixels" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cachePath = renderer.GetCachePath(project, item);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await WriteTransparentPngAsync(cachePath, 2, 2, TestContext.CancellationToken);
        SetIhdrByte(cachePath, ihdrDataOffset, checked((byte)unsupportedValue));
        var renderCount = 0;

        var result = await renderer.GetOrRenderAsync(
            project,
            item,
            async (temporaryPath, cancellationToken) =>
            {
                renderCount++;
                await WriteTransparentPngAsync(temporaryPath, 2, 2, cancellationToken);
            },
            CancellationToken.None);

        Assert.AreEqual(1, renderCount);
        Assert.AreEqual((2u, 2u), await ReadPngDimensionsAsync(result));
    }

    [TestMethod]
    public void DecodedPixelBufferSize_IsCheckedAndBounded()
    {
        Assert.IsTrue(Services.TextOverlayRenderer.TryGetDecodedPixelByteCount(1920, 1920, out var byteCount));
        Assert.AreEqual(14_745_600, byteCount);
        Assert.IsFalse(Services.TextOverlayRenderer.TryGetDecodedPixelByteCount(4097, 4096, out _));
        Assert.IsFalse(Services.TextOverlayRenderer.TryGetDecodedPixelByteCount(uint.MaxValue, uint.MaxValue, out _));
    }

    [TestMethod]
    public async Task RendererInstances_PublishingSameKey_BothReuseValidWinner()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Concurrent publish", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Shared" };
        var firstRenderer = new Services.TextOverlayRenderer(directory.Path);
        var secondRenderer = new Services.TextOverlayRenderer(directory.Path);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = firstRenderer.GetOrRenderAsync(
            project,
            item,
            async (path, cancellationToken) =>
            {
                await WriteTransparentPngAsync(path, 1, 1, cancellationToken);
                firstStarted.SetResult(true);
                await releaseFirst.Task.WaitAsync(cancellationToken);
            },
            CancellationToken.None);

        await firstStarted.Task;
        var second = secondRenderer.GetOrRenderAsync(
            project,
            item,
            (path, cancellationToken) => WriteTransparentPngAsync(path, 1, 1, cancellationToken),
            CancellationToken.None);

        string[] paths;
        try
        {
            var winnerPath = await second;
            using var winnerLock = new FileStream(winnerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            releaseFirst.TrySetResult(true);
            paths = await Task.WhenAll(first, second);
        }
        finally
        {
            releaseFirst.TrySetResult(true);
        }

        Assert.AreSequenceEqual(new[] { paths[0], paths[0] }, paths);
        Assert.HasCount(4, await ReadPngPixelsAsync(paths[0]));
    }

    [TestMethod]
    public async Task RendererInstance_SerializesDifferentCacheKeysForSharedHost()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Concurrent render", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var first = new TextTimelineItem { Id = Guid.NewGuid(), Text = "First" };
        var second = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Second" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var active = 0;
        var maximumActive = 0;
        var sync = new object();
        async Task Render(string path, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                active++;
                maximumActive = Math.Max(maximumActive, active);
            }

            try
            {
                await Task.Delay(75, cancellationToken);
                await WriteTransparentPngAsync(path, 1, 1, cancellationToken);
            }
            finally
            {
                lock (sync) active--;
            }
        }

        await Task.WhenAll(
            renderer.GetOrRenderAsync(project, first, Render, CancellationToken.None),
            renderer.GetOrRenderAsync(project, second, Render, CancellationToken.None));

        Assert.AreEqual(1, maximumActive);
    }

    [TestMethod]
    public async Task RendererInstance_CancellationWhileQueuedNeverStartsObsoleteRender()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Canceled render", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var first = new TextTimelineItem { Id = Guid.NewGuid(), Text = "First" };
        var second = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Second" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderCount = 0;
        async Task Render(string path, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref renderCount);
            if (call == 1)
            {
                firstStarted.SetResult(true);
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            await WriteTransparentPngAsync(path, 1, 1, cancellationToken);
        }

        var firstRender = renderer.GetOrRenderAsync(project, first, Render, CancellationToken.None);
        await firstStarted.Task;
        using var cancellation = new CancellationTokenSource();
        var obsoleteRender = renderer.GetOrRenderAsync(project, second, Render, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => obsoleteRender);
        Assert.AreEqual(1, renderCount);
        releaseFirst.SetResult(true);
        await firstRender;
    }

    [TestMethod]
    public async Task CacheAfterRender_RetainsCurrentAndNewestPngFilesDeterministically()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Bounded cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Newest" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cacheDirectory = Path.GetDirectoryName(renderer.GetCachePath(project, item))!;
        Directory.CreateDirectory(cacheDirectory);
        var sharedTimestamp = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 256; index++)
        {
            var path = Path.Combine(cacheDirectory, $"old-{index:D3}.png");
            await File.WriteAllBytesAsync(path, [0], TestContext.CancellationToken);
            File.SetLastWriteTimeUtc(path, sharedTimestamp);
        }
        var temporaryPath = Path.Combine(cacheDirectory, "render-in-progress.png.tmp");
        var nonPngPath = Path.Combine(cacheDirectory, "notes.txt");
        await File.WriteAllTextAsync(temporaryPath, "temporary", TestContext.CancellationToken);
        await File.WriteAllTextAsync(nonPngPath, "unrelated", TestContext.CancellationToken);

        var currentPath = await renderer.GetOrRenderAsync(
            project,
            item,
            (path, cancellationToken) => WriteTransparentPngAsync(path, 1, 1, cancellationToken),
            CancellationToken.None);

        Assert.HasCount(256, Directory.GetFiles(cacheDirectory, "*.png"));
        Assert.IsTrue(File.Exists(currentPath));
        Assert.IsTrue(File.Exists(Path.Combine(cacheDirectory, "old-000.png")));
        Assert.IsFalse(File.Exists(Path.Combine(cacheDirectory, "old-255.png")));
        Assert.IsTrue(File.Exists(temporaryPath));
        Assert.IsTrue(File.Exists(nonPngPath));
    }

    [TestMethod]
    public async Task CacheAfterRender_WhenOldestPngIsLocked_DeletesNextOldest()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Locked cache entry", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Current" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cacheDirectory = Path.GetDirectoryName(renderer.GetCachePath(project, item))!;
        Directory.CreateDirectory(cacheDirectory);
        var oldestPath = Path.Combine(cacheDirectory, "old-000.png");
        var nextOldestPath = Path.Combine(cacheDirectory, "old-001.png");
        var firstTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 256; index++)
        {
            var path = Path.Combine(cacheDirectory, $"old-{index:D3}.png");
            await File.WriteAllBytesAsync(path, [0], TestContext.CancellationToken);
            File.SetLastWriteTimeUtc(path, firstTimestamp.AddMinutes(index));
        }

        string currentPath;
        using (new FileStream(oldestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            currentPath = await renderer.GetOrRenderAsync(
                project,
                item,
                (path, cancellationToken) => WriteTransparentPngAsync(path, 1, 1, cancellationToken),
                CancellationToken.None);

            Assert.HasCount(256, Directory.GetFiles(cacheDirectory, "*.png"));
            Assert.IsTrue(File.Exists(oldestPath));
            Assert.IsFalse(File.Exists(nextOldestPath));
            Assert.IsTrue(File.Exists(currentPath));
        }
    }

    [TestMethod]
    public async Task PublishCacheFile_WhenCanceledAfterValidation_DoesNotPublishStalePng()
    {
        using var directory = new TemporaryDirectory();
        var temporaryPath = Path.Combine(directory.Path, "overlay.tmp");
        var cachePath = Path.Combine(directory.Path, "overlay.png");
        await File.WriteAllTextAsync(temporaryPath, "rendered bytes", TestContext.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            Services.TextOverlayRenderer.PublishCacheFile(temporaryPath, cachePath, cancellation.Token));

        Assert.IsTrue(File.Exists(temporaryPath));
        Assert.IsFalse(File.Exists(cachePath));
    }

    [TestMethod]
    public async Task EncodePngAsync_PreservesTransparentAlphaChannel()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "transparent.png");
        byte[] pixels = [0, 0, 0, 0, 20, 40, 60, 128];

        await Services.TextOverlayRenderer.EncodePngAsync(
            path,
            pixels,
            width: 2,
            height: 1,
            outputWidth: 2,
            outputHeight: 1,
            CancellationToken.None);

        var decoded = await ReadPngPixelsAsync(path);
        Assert.AreEqual(0, decoded[3]);
        Assert.AreEqual(128, decoded[7]);
    }

    [TestMethod]
    [DataRow("TextContent", VirtualKey.Enter, false, false)]
    [DataRow("TextContent", VirtualKey.Enter, true, true)]
    [DataRow("TextDuration", VirtualKey.Enter, false, true)]
    [DataRow("TextContent", VirtualKey.Escape, true, false)]
    public void InspectorCommitGesture_PreservesMultilineEnterAndUsesExplicitCommit(
        string tag, VirtualKey key, bool controlDown, bool expected)
    {
        Assert.AreEqual(expected, Controls.InspectorCommitGesture.ShouldCommit(tag, key, controlDown));
    }

    [TestMethod]
    public void InspectorDuplicateCommit_SecondNoOpDoesNotAddHistory()
    {
        var project = ProjectDocument.CreateNew("Inspector", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid() };
        project.TextItems.Add(item);
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsTrue(viewModel.SetTextContent(item.Id, "Changed"));
        Assert.IsFalse(viewModel.SetTextContent(item.Id, "Changed"));
        Assert.AreEqual(1L, viewModel.Revision);
        Assert.AreEqual(1, committed);
    }

    [TestMethod]
    public void InspectorEditBoundary_RefreshesCanonicalStateAfterRequest()
    {
        var project = ProjectDocument.CreateNew("Inspector", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            DurationMilliseconds = 2_000
        };
        var item = new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceOutMilliseconds = 2_000,
            DurationMilliseconds = 2_000
        };
        project.Assets.Add(asset);
        project.VideoItems.Add(item);
        var viewModel = new EditorViewModel(project);
        var displayedSourceOut = 5_000L;
        var committed = true;

        Controls.InspectorEditBoundary.Commit(
            () => committed = viewModel.TrimVideoEnd(item.Id, displayedSourceOut),
            () => displayedSourceOut = viewModel.Project.VideoItems.Single().SourceOutMilliseconds);

        Assert.IsFalse(committed);
        Assert.AreEqual(2_000L, displayedSourceOut);
        Assert.IsFalse(viewModel.CanUndo);
    }

    private sealed partial class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private static async Task WriteTransparentPngAsync(
        string path,
        uint width,
        uint height,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using IRandomAccessStream randomAccess = stream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccess);
        cancellationToken.ThrowIfCancellationRequested();
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            width,
            height,
            96,
            96,
            new byte[checked((int)(width * height * 4))]);
        await encoder.FlushAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<(uint Width, uint Height)> ReadPngDimensionsAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using IRandomAccessStream randomAccess = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, randomAccess);
        return (decoder.PixelWidth, decoder.PixelHeight);
    }

    private static async Task<byte[]> ReadPngPixelsAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using IRandomAccessStream randomAccess = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, randomAccess);
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        return pixels.DetachPixelData();
    }

    private static void TruncateFirstIdatPayload(string path)
    {
        var bytes = File.ReadAllBytes(path);
        ReadOnlySpan<byte> marker = "IDAT"u8;
        var markerIndex = bytes.AsSpan().IndexOf(marker);
        Assert.IsGreaterThanOrEqualTo(8, markerIndex, "The generated PNG did not contain an IDAT chunk.");
        var dataIndex = markerIndex + marker.Length;
        Assert.IsLessThan(bytes.Length - 4, dataIndex, "The generated PNG IDAT chunk had no payload.");
        File.WriteAllBytes(path, bytes[..(dataIndex + 1)]);
    }

    private static void CorruptPng(string path, PngCorruption corruption)
    {
        if (corruption == PngCorruption.TruncatedIdat)
        {
            TruncateFirstIdatPayload(path);
            return;
        }

        var bytes = File.ReadAllBytes(path);
        if (corruption == PngCorruption.OversizedIhdrLength)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, sizeof(uint)), uint.MaxValue);
            File.WriteAllBytes(path, bytes);
            return;
        }

        ReadOnlySpan<byte> expectedType = corruption switch
        {
            PngCorruption.IhdrCrc => "IHDR"u8,
            PngCorruption.IdatCrc => "IDAT"u8,
            PngCorruption.IendCrc => "IEND"u8,
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            var chunkEnd = (long)offset + 12 + dataLength;
            Assert.IsLessThanOrEqualTo(bytes.Length, chunkEnd, "The generated PNG contained an invalid chunk boundary.");
            if (bytes.AsSpan(offset + 4, 4).SequenceEqual(expectedType))
            {
                bytes[offset + 8 + (int)dataLength] ^= 0x01;
                File.WriteAllBytes(path, bytes);
                return;
            }

            offset = (int)chunkEnd;
        }

        Assert.Fail($"The generated PNG did not contain the expected {corruption} chunk.");
    }

    private static void ApplyPngStructureViolation(string path, PngStructureViolation violation)
    {
        switch (violation)
        {
            case PngStructureViolation.UnknownCriticalChunk:
                InsertPngChunk(path, "ABCD"u8, [], "IDAT"u8);
                return;
            case PngStructureViolation.PlteAfterIdat:
                InsertPngChunk(path, "PLTE"u8, [0, 0, 0], "IEND"u8);
                return;
            case PngStructureViolation.DuplicatePlte:
                InsertPngChunk(path, "PLTE"u8, [0, 0, 0], "IDAT"u8);
                InsertPngChunk(path, "PLTE"u8, [0, 0, 0], "IDAT"u8);
                return;
            case PngStructureViolation.NonConsecutiveIdat:
                SplitIdatWithAncillaryChunk(path);
                return;
            default:
                Assert.Fail($"Unsupported PNG structure violation: {violation}.");
                return;
        }
    }

    private static void SetIhdrByte(string path, int dataOffset, byte value)
    {
        var bytes = File.ReadAllBytes(path);
        var ihdrOffset = FindPngChunkOffset(bytes, "IHDR"u8);
        var dataLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(ihdrOffset, 4)));
        Assert.AreEqual(13, dataLength);
        bytes[ihdrOffset + 8 + dataOffset] = value;
        BinaryPrimitives.WriteUInt32BigEndian(
            bytes.AsSpan(ihdrOffset + 8 + dataLength, 4),
            CalculatePngCrc("IHDR"u8, bytes.AsSpan(ihdrOffset + 8, dataLength)));
        File.WriteAllBytes(path, bytes);
    }

    private static void InsertPngChunk(
        string path,
        ReadOnlySpan<byte> chunkType,
        ReadOnlySpan<byte> chunkData,
        ReadOnlySpan<byte> beforeChunkType)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = FindPngChunkOffset(bytes, beforeChunkType);
        var chunk = CreatePngChunk(chunkType, chunkData);
        File.WriteAllBytes(path, ReplaceBytes(bytes, offset, 0, chunk));
    }

    private static void SplitIdatWithAncillaryChunk(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = FindPngChunkOffset(bytes, "IDAT"u8);
        var dataLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
        Assert.IsGreaterThan(1, dataLength, "The generated PNG IDAT chunk could not be split.");
        var split = dataLength / 2;
        var data = bytes.AsSpan(offset + 8, dataLength);
        var firstIdat = CreatePngChunk("IDAT"u8, data[..split]);
        var ancillary = CreatePngChunk("vpAg"u8, []);
        var secondIdat = CreatePngChunk("IDAT"u8, data[split..]);
        var replacement = new byte[firstIdat.Length + ancillary.Length + secondIdat.Length];
        firstIdat.CopyTo(replacement, 0);
        ancillary.CopyTo(replacement, firstIdat.Length);
        secondIdat.CopyTo(replacement, firstIdat.Length + ancillary.Length);
        File.WriteAllBytes(path, ReplaceBytes(bytes, offset, dataLength + 12, replacement));
    }

    private static int FindPngChunkOffset(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> expectedType)
    {
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            var chunkEnd = (long)offset + 12 + dataLength;
            Assert.IsLessThanOrEqualTo(bytes.Length, chunkEnd, "The generated PNG contained an invalid chunk boundary.");
            if (bytes.Slice(offset + 4, 4).SequenceEqual(expectedType))
            {
                return offset;
            }

            offset = (int)chunkEnd;
        }

        Assert.Fail($"The generated PNG did not contain chunk {Encoding.ASCII.GetString(expectedType)}.");
        return -1;
    }

    private static byte[] CreatePngChunk(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> chunkData)
    {
        Assert.HasCount(4, chunkType);
        var chunk = new byte[checked(chunkData.Length + 12)];
        BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)chunkData.Length);
        chunkType.CopyTo(chunk.AsSpan(4));
        chunkData.CopyTo(chunk.AsSpan(8));
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + chunkData.Length), CalculatePngCrc(chunkType, chunkData));
        return chunk;
    }

    private static uint CalculatePngCrc(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> chunkData)
    {
        var crc = uint.MaxValue;
        foreach (var value in chunkType)
        {
            crc = UpdatePngCrc(crc, value);
        }

        foreach (var value in chunkData)
        {
            crc = UpdatePngCrc(crc, value);
        }

        return ~crc;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) == 0 ? crc >> 1 : 0xEDB88320u ^ (crc >> 1);
        }

        return crc;
    }

    private static byte[] ReplaceBytes(byte[] source, int offset, int count, byte[] replacement)
    {
        var result = new byte[checked(source.Length - count + replacement.Length)];
        source.AsSpan(0, offset).CopyTo(result);
        replacement.CopyTo(result, offset);
        source.AsSpan(offset + count).CopyTo(result.AsSpan(offset + replacement.Length));
        return result;
    }

    public enum PngCorruption
    {
        TruncatedIdat,
        IhdrCrc,
        IdatCrc,
        IendCrc,
        OversizedIhdrLength
    }

    public enum PngStructureViolation
    {
        UnknownCriticalChunk,
        PlteAfterIdat,
        NonConsecutiveIdat,
        DuplicatePlte
    }

    public TestContext TestContext { get; set; }
}
