using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using CutFlow.Models;
using CutFlow.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CutFlow.Services;

public sealed class TextOverlayRenderer
{
    private const int CacheFormatVersion = 2;
    private const int MaximumCachedFiles = 256;
    private const int PngSignatureLength = 8;
    private const int PngChunkOverheadLength = 12;
    private const int IhdrDataLength = 13;
    private const int MinimumPngFileSize = PngSignatureLength + (3 * PngChunkOverheadLength) + IhdrDataLength + 1;
    private const int MaximumPngFileSize = 64 * 1024 * 1024;
    private static readonly uint[] Crc32Table = CreateCrc32Table();
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _renderGate = new(1, 1);

    public TextOverlayRenderer(string projectRootPath, FrameworkElement? renderHost = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        _cacheDirectory = Path.Combine(Path.GetFullPath(projectRootPath), "cache", "text-overlays");
        RenderHost = renderHost;
    }

    public FrameworkElement? RenderHost { get; set; }

    public string GetCachePath(ProjectDocument project, TextTimelineItem item) =>
        Path.Combine(_cacheDirectory, GetCacheFileName(project, item));

    public async Task<string> RenderAsync(
        ProjectDocument project,
        TextTimelineItem item,
        FrameworkElement renderHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(renderHost);
        cancellationToken.ThrowIfCancellationRequested();
        if (renderHost.Visibility == Visibility.Collapsed || !renderHost.IsLoaded || renderHost is not Panel hostPanel)
        {
            throw new InvalidOperationException("The text render host must be loaded, visible to layout, and able to host a temporary visual.");
        }

        return await GetOrRenderAsync(
            project,
            item,
            (temporaryPath, token) => RenderPngAsync(project, item, hostPanel, temporaryPath, token),
            cancellationToken);
    }

    internal async Task<string> GetOrRenderAsync(
        ProjectDocument project,
        TextTimelineItem item,
        Func<string, CancellationToken, Task> renderAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(renderAsync);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_cacheDirectory);
        var path = GetCachePath(project, item);
        if (await IsCacheFileValidAsync(path, project.Settings.Width, project.Settings.Height, cancellationToken))
        {
            return path;
        }

        await _renderGate.WaitAsync(cancellationToken);
        try
        {
            if (await IsCacheFileValidAsync(path, project.Settings.Width, project.Settings.Height, cancellationToken))
            {
                return path;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await renderAsync(temporaryPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!await IsCacheFileValidAsync(
                        temporaryPath,
                        project.Settings.Width,
                        project.Settings.Height,
                        cancellationToken))
                {
                    throw new InvalidDataException("The rendered text overlay PNG has invalid pixel dimensions.");
                }

                try
                {
                    PublishCacheFile(temporaryPath, path, cancellationToken);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    if (!await IsCacheFileValidAsync(
                            path,
                            project.Settings.Width,
                            project.Settings.Height,
                            cancellationToken))
                    {
                        throw;
                    }
                }

                TrimCache(path);
                return path;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            _renderGate.Release();
        }
    }

    private static async Task<bool> IsCacheFileValidAsync(
        string path,
        int expectedWidth,
        int expectedHeight,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var fileLength = fileStream.Length;
            if (fileLength is < MinimumPngFileSize or > MaximumPngFileSize)
            {
                return false;
            }

            var fileBytes = GC.AllocateUninitializedArray<byte>((int)fileLength);
            await fileStream.ReadExactlyAsync(fileBytes, cancellationToken);
            if (!HasValidPngStructure(fileBytes, cancellationToken))
            {
                return false;
            }

            fileStream.Position = 0;
            using IRandomAccessStream randomAccess = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, randomAccess);
            cancellationToken.ThrowIfCancellationRequested();
            if (decoder.PixelWidth != expectedWidth || decoder.PixelHeight != expectedHeight)
            {
                return false;
            }

            var pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            cancellationToken.ThrowIfCancellationRequested();
            _ = pixels.DetachPixelData();
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or COMException)
        {
            return false;
        }
    }

    private static bool HasValidPngStructure(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!bytes[..PngSignatureLength].SequenceEqual(signature))
        {
            return false;
        }

        var offset = PngSignatureLength;
        var isFirstChunk = true;
        var hasIhdr = false;
        var hasImageData = false;
        while (offset <= bytes.Length - PngChunkOverheadLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, sizeof(uint)));
            if (dataLength > int.MaxValue || dataLength > bytes.Length - offset - PngChunkOverheadLength)
            {
                return false;
            }

            var chunkDataLength = (int)dataLength;
            var chunkType = bytes.Slice(offset + sizeof(uint), sizeof(uint));
            var chunkData = bytes.Slice(offset + (2 * sizeof(uint)), chunkDataLength);
            var crcOffset = offset + (2 * sizeof(uint)) + chunkDataLength;
            var storedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(crcOffset, sizeof(uint)));
            if (storedCrc != CalculateCrc32(chunkType, chunkData, cancellationToken))
            {
                return false;
            }

            if (isFirstChunk)
            {
                if (!chunkType.SequenceEqual("IHDR"u8) || chunkDataLength != IhdrDataLength)
                {
                    return false;
                }

                hasIhdr = true;
            }
            else if (chunkType.SequenceEqual("IHDR"u8))
            {
                return false;
            }

            if (chunkType.SequenceEqual("IDAT"u8) && chunkDataLength > 0)
            {
                hasImageData = true;
            }

            var chunkEnd = crcOffset + sizeof(uint);
            if (chunkType.SequenceEqual("IEND"u8))
            {
                return chunkDataLength == 0 && hasIhdr && hasImageData && chunkEnd == bytes.Length;
            }

            offset = chunkEnd;
            isFirstChunk = false;
        }

        return false;
    }

    private static uint CalculateCrc32(
        ReadOnlySpan<byte> chunkType,
        ReadOnlySpan<byte> chunkData,
        CancellationToken cancellationToken)
    {
        var crc = UpdateCrc32(uint.MaxValue, chunkType, cancellationToken);
        return ~UpdateCrc32(crc, chunkData, cancellationToken);
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        for (var index = 0; index < bytes.Length; index++)
        {
            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            crc = Crc32Table[(byte)(crc ^ bytes[index])] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (var index = 0; index < table.Length; index++)
        {
            var crc = (uint)index;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : 0xEDB88320u ^ (crc >> 1);
            }

            table[index] = crc;
        }

        return table;
    }

    private static async Task RenderPngAsync(
        ProjectDocument project,
        TextTimelineItem item,
        Panel hostPanel,
        string path,
        CancellationToken cancellationToken)
    {
        var visual = CreateVisual(project, item);
        hostPanel.Children.Add(visual);
        try
        {
            visual.Measure(new Windows.Foundation.Size(project.Settings.Width, project.Settings.Height));
            visual.Arrange(new Windows.Foundation.Rect(0, 0, project.Settings.Width, project.Settings.Height));
            var bitmap = new RenderTargetBitmap();
            var renderSize = CalculateRenderSize(
                project.Settings.Width,
                project.Settings.Height,
                visual.XamlRoot?.RasterizationScale ?? 1);
            await bitmap.RenderAsync(visual, renderSize.Width, renderSize.Height);
            cancellationToken.ThrowIfCancellationRequested();
            if (bitmap.PixelWidth != project.Settings.Width || bitmap.PixelHeight != project.Settings.Height)
            {
                throw new InvalidDataException(
                    $"The rendered overlay was {bitmap.PixelWidth}x{bitmap.PixelHeight}; expected {project.Settings.Width}x{project.Settings.Height} pixels.");
            }

            var buffer = await bitmap.GetPixelsAsync();
            cancellationToken.ThrowIfCancellationRequested();
            await EncodePngAsync(path, buffer.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight, cancellationToken);
        }
        finally
        {
            hostPanel.Children.Remove(visual);
        }
    }

    public static string CalculateStyleHash(ProjectDocument project, TextTimelineItem item)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(item);
        var value = string.Join('|',
            CacheFormatVersion.ToString(CultureInfo.InvariantCulture),
            item.Text,
            item.FontFamily,
            item.FontSize.ToString("R", CultureInfo.InvariantCulture),
            item.FontWeight.ToString(CultureInfo.InvariantCulture),
            item.IsItalic.ToString(CultureInfo.InvariantCulture),
            item.TextColor,
            item.BackgroundColor,
            item.BackgroundEnabled.ToString(CultureInfo.InvariantCulture),
            TextStyle.ClampOpacity(item.Opacity).ToString("R", CultureInfo.InvariantCulture),
            item.Alignment.ToString(),
            item.NormalizedX.ToString("R", CultureInfo.InvariantCulture),
            item.NormalizedY.ToString("R", CultureInfo.InvariantCulture),
            project.Settings.Width.ToString(CultureInfo.InvariantCulture),
            project.Settings.Height.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
    }

    public static string GetCacheFileName(ProjectDocument project, TextTimelineItem item) =>
        $"{item.Id:D}-{CalculateStyleHash(project, item)}.png";

    internal static (int Width, int Height) CalculateRenderSize(
        int pixelWidth,
        int pixelHeight,
        double rasterizationScale)
    {
        if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        if (!double.IsFinite(rasterizationScale) || rasterizationScale <= 0) rasterizationScale = 1;
        return (
            Math.Max(1, (int)Math.Round(pixelWidth / rasterizationScale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(pixelHeight / rasterizationScale, MidpointRounding.AwayFromZero)));
    }

    internal static void PublishCacheFile(
        string temporaryPath,
        string cachePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(temporaryPath, cachePath, overwrite: true);
    }

    private static Canvas CreateVisual(ProjectDocument project, TextTimelineItem item)
    {
        var width = project.Settings.Width;
        var height = project.Settings.Height;
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Opacity = 1,
            IsHitTestVisible = false
        };
        var text = new TextBlock();
        TextStyle.ApplyTextBlockStyle(text, item, width * 0.9);
        var container = new Border
        {
            Child = text
        };
        TextStyle.ApplyContainerStyle(container, item, new Thickness(0));
        container.Measure(new Windows.Foundation.Size(width * 0.9, height));
        Canvas.SetLeft(container, TextStyle.ClampNormalized(item.NormalizedX) * width - container.DesiredSize.Width / 2);
        Canvas.SetTop(container, TextStyle.ClampNormalized(item.NormalizedY) * height - container.DesiredSize.Height / 2);
        canvas.Children.Add(container);
        return canvas;
    }

    internal static async Task EncodePngAsync(
        string path,
        byte[] pixels,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);
        using IRandomAccessStream randomAccess = fileStream.AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccess);
        cancellationToken.ThrowIfCancellationRequested();
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync();
        cancellationToken.ThrowIfCancellationRequested();
        await fileStream.FlushAsync(cancellationToken);
    }

    private void TrimCache(string currentPath)
    {
        foreach (var file in new DirectoryInfo(_cacheDirectory)
                     .EnumerateFiles("*.png")
                     .Where(file => !string.Equals(file.FullName, currentPath, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Skip(MaximumCachedFiles - 1))
        {
            try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
