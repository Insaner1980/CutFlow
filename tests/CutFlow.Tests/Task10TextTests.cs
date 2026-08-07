using CutFlow.Models;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Buffers.Binary;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task10TextTests
{
    [DataTestMethod]
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
        Assert.IsTrue(item.FontWeight > 0);
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
            """);
        var service = new Services.ProjectService(directory.Path);

        var loaded = await service.LoadAsync(id);
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
        await service.SaveAsync(loaded);
        var roundTrip = (await service.LoadAsync(id)).TextItems.Single();

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
            Id = Guid.NewGuid(), Text = "Styled", StartMilliseconds = 200, DurationMilliseconds = 1_500,
            FontFamily = "Arial", FontSize = 73, FontWeight = 700, TextColor = "#FFABCDEF",
            BackgroundColor = "#80443322", Alignment = TextHorizontalAlignment.Left,
            NormalizedX = 0.2, NormalizedY = 0.7
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
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Hello" };
        var original = Services.TextOverlayRenderer.CalculateStyleHash(project, item);
        item.StartMilliseconds = 8_000;
        item.DurationMilliseconds = 500;
        Assert.AreEqual(original, Services.TextOverlayRenderer.CalculateStyleHash(project, item));

        item.NormalizedX = 0.6;
        Assert.AreNotEqual(original, Services.TextOverlayRenderer.CalculateStyleHash(project, item));
        item.NormalizedX = 0.5;
        project.Settings.Width = 1280;
        Assert.AreNotEqual(original, Services.TextOverlayRenderer.CalculateStyleHash(project, item));
        StringAssert.Matches(
            Services.TextOverlayRenderer.GetCacheFileName(project, item),
            new System.Text.RegularExpressions.Regex($"^{item.Id:D}-[0-9a-f]{{16}}\\.png$"));
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
    public void RenderSize_CompensatesForOneHundredFiftyPercentRasterizationScale()
    {
        var size = Services.TextOverlayRenderer.CalculateRenderSize(1920, 1080, 1.5);

        Assert.AreEqual(1280, size.Width);
        Assert.AreEqual(720, size.Height);
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
        await WriteTransparentPngAsync(cachePath, 1, 1);
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
        await WriteTransparentPngAsync(cachePath, 2, 2);
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

    [DataTestMethod]
    [DataRow(PngCorruption.TruncatedIdat)]
    [DataRow(PngCorruption.IhdrCrc)]
    [DataRow(PngCorruption.IdatCrc)]
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
        await WriteTransparentPngAsync(cachePath, 2, 2);
        CorruptPng(cachePath, corruption);
        if (corruption is PngCorruption.IhdrCrc or PngCorruption.IdatCrc)
        {
            Assert.AreEqual(16, (await ReadPngPixelsAsync(cachePath)).Length, "WIC should still decode the CRC-corrupt fixture.");
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

        CollectionAssert.AreEqual(new[] { paths[0], paths[0] }, paths);
        Assert.AreEqual(4, (await ReadPngPixelsAsync(paths[0])).Length);
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
    public async Task CacheAfterRender_RemainsBoundedToTwoHundredFiftySixPngFiles()
    {
        using var directory = new TemporaryDirectory();
        var project = ProjectDocument.CreateNew("Bounded cache", DateTimeOffset.UnixEpoch);
        project.Settings.Width = 1;
        project.Settings.Height = 1;
        var item = new TextTimelineItem { Id = Guid.NewGuid(), Text = "Newest" };
        var renderer = new Services.TextOverlayRenderer(directory.Path);
        var cacheDirectory = Path.GetDirectoryName(renderer.GetCachePath(project, item))!;
        Directory.CreateDirectory(cacheDirectory);
        for (var index = 0; index < 256; index++)
        {
            var path = Path.Combine(cacheDirectory, $"old-{index:D3}.png");
            await File.WriteAllBytesAsync(path, [0]);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(1));
        }

        await renderer.GetOrRenderAsync(
            project,
            item,
            (path, cancellationToken) => WriteTransparentPngAsync(path, 1, 1, cancellationToken),
            CancellationToken.None);

        Assert.AreEqual(256, Directory.GetFiles(cacheDirectory, "*.png").Length);
    }

    [TestMethod]
    public async Task PublishCacheFile_WhenCanceledAfterValidation_DoesNotPublishStalePng()
    {
        using var directory = new TemporaryDirectory();
        var temporaryPath = Path.Combine(directory.Path, "overlay.tmp");
        var cachePath = Path.Combine(directory.Path, "overlay.png");
        await File.WriteAllTextAsync(temporaryPath, "rendered bytes");
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
            CancellationToken.None);

        var decoded = await ReadPngPixelsAsync(path);
        Assert.AreEqual(0, decoded[3]);
        Assert.AreEqual(128, decoded[7]);
    }

    [DataTestMethod]
    [DataRow("TextContent", VirtualKey.Enter, false, false)]
    [DataRow("TextContent", VirtualKey.Enter, true, true)]
    [DataRow("TextDuration", VirtualKey.Enter, false, true)]
    [DataRow("TextContent", VirtualKey.Escape, true, false)]
    public void InspectorCommitGesture_PreservesMultilineEnterAndUsesExplicitCommit(
        string tag, VirtualKey key, bool controlDown, bool expected)
    {
        Assert.AreEqual(expected, Controls.InspectorCommitGesture.ShouldCommit(tag, key, controlDown));
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
        Assert.IsTrue(markerIndex >= 8, "The generated PNG did not contain an IDAT chunk.");
        var dataIndex = markerIndex + marker.Length;
        Assert.IsTrue(dataIndex < bytes.Length - 4, "The generated PNG IDAT chunk had no payload.");
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
        ReadOnlySpan<byte> expectedType = corruption == PngCorruption.IhdrCrc ? "IHDR"u8 : "IDAT"u8;
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            var chunkEnd = (long)offset + 12 + dataLength;
            Assert.IsTrue(chunkEnd <= bytes.Length, "The generated PNG contained an invalid chunk boundary.");
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

    public enum PngCorruption
    {
        TruncatedIdat,
        IhdrCrc,
        IdatCrc
    }
}
