using System.Security.Cryptography;
using System.Text;
using CutFlow.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace CutFlow.Services;

public sealed class ThumbnailService
{
    public const long MaximumCachedThumbnailBytes = 8 * 1024 * 1024;
    public const int DefaultRequestedSize = 256;
    public const int MaximumRequestedSize = 1_024;
    public const string FallbackTileKey = "media-fallback";
    public const string AudioTileKey = "audio-fallback";

    private readonly string _projectRootPath;

    public ThumbnailService(string projectRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        _projectRootPath = Path.GetFullPath(projectRootPath);
    }

    public async Task<string?> GetOrCreateThumbnailAsync(
        StorageFile file,
        ProjectAsset asset,
        int requestedSize = DefaultRequestedSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(asset);
        if (requestedSize is <= 0 or > MaximumRequestedSize)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSize));
        }

        if (asset.Kind == ProjectAssetKind.Audio || asset.IsMissing)
        {
            return null;
        }

        var request = CaptureRequest(asset, requestedSize);
        if (!IsStoragePathForRequest(request, file.Path))
        {
            return null;
        }

        var relativePath = CreateRelativeCachePath(request.CacheKey);
        var cachePath = ResolveProjectCachePath(_projectRootPath, relativePath);
        if (File.Exists(cachePath))
        {
            return TryCommitCacheHit(asset, request, relativePath) ? cachePath : null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var mode = request.Kind == ProjectAssetKind.Image ? ThumbnailMode.PicturesView : ThumbnailMode.VideosView;
        using var thumbnail = await file.GetThumbnailAsync(mode, checked((uint)requestedSize), ThumbnailOptions.ResizeThumbnail);
        cancellationToken.ThrowIfCancellationRequested();
        if (thumbnail is null || thumbnail.Type != ThumbnailType.Image)
        {
            return null;
        }

        using var encodedThumbnail = await EncodeJpegAsync(thumbnail, cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var temporaryPath = cachePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous))
            {
                await CopyBoundedAsync(encodedThumbnail.AsStreamForRead(), destination, MaximumCachedThumbnailBytes, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return TryCommitGeneratedThumbnail(asset, request, temporaryPath, relativePath) ? cachePath : null;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public bool TryDeleteCachedThumbnail(ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (string.IsNullOrWhiteSpace(asset.ThumbnailCachePath))
        {
            return false;
        }

        var path = ResolveProjectCachePath(_projectRootPath, asset.ThumbnailCachePath);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public static string CreateCacheKey(string sourcePath, ulong fileSize, DateTimeOffset lastWriteUtc, int requestedSize)
    {
        if (requestedSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSize));
        }

        var normalized = MediaImportService.NormalizePath(sourcePath).ToUpperInvariant();
        var input = $"{normalized}\n{fileSize}\n{lastWriteUtc.ToUniversalTime().Ticks}\n{requestedSize}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    internal static ThumbnailRequest CaptureRequest(ProjectAsset asset, int requestedSize)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (requestedSize is <= 0 or > MaximumRequestedSize)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedSize));
        }

        lock (asset)
        {
            var sourcePath = MediaImportService.NormalizePath(asset.SourcePath);
            return new ThumbnailRequest(
                asset.Id,
                asset.Kind,
                sourcePath,
                asset.FileSize,
                asset.LastWriteUtc.ToUniversalTime().Ticks,
                requestedSize,
                CreateCacheKey(sourcePath, asset.FileSize, asset.LastWriteUtc, requestedSize));
        }
    }

    internal static bool IsCurrentRequest(ProjectAsset asset, ThumbnailRequest request)
    {
        ArgumentNullException.ThrowIfNull(asset);
        lock (asset)
        {
            return IsCurrentRequestLocked(asset, request);
        }
    }

    internal static bool IsStoragePathForRequest(ThumbnailRequest request, string storagePath) =>
        !string.IsNullOrWhiteSpace(storagePath) &&
        string.Equals(
            request.SourcePath,
            MediaImportService.NormalizePath(storagePath),
            StringComparison.OrdinalIgnoreCase);

    internal bool TryCommitGeneratedThumbnail(
        ProjectAsset asset,
        ThumbnailRequest request,
        string temporaryPath,
        string relativePath)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!string.Equals(
                relativePath,
                CreateRelativeCachePath(request.CacheKey),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The thumbnail cache path does not match its request.");
        }

        var cachePath = ResolveProjectCachePath(_projectRootPath, relativePath);
        EnsureProjectOwnedThumbnailPath(temporaryPath);

        lock (asset)
        {
            if (!IsCurrentRequestLocked(asset, request))
            {
                TryDeleteProjectOwnedFile(temporaryPath);
                return false;
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
            asset.ThumbnailCachePath = relativePath;
            return true;
        }
    }

    public static string CreateRelativeCachePath(string cacheKey)
    {
        if (cacheKey.Length != 64 || cacheKey.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 cache key is required.", nameof(cacheKey));
        }

        return Path.Combine("cache", "thumbnails", $"{cacheKey.ToLowerInvariant()}.jpg");
    }

    public static string ResolveProjectCachePath(string projectRootPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Thumbnail cache paths must be project-relative.");
        }

        var root = Path.GetFullPath(projectRootPath);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        var thumbnailsRoot = Path.GetFullPath(Path.Combine(root, "cache", "thumbnails"));
        if (!resolved.StartsWith(thumbnailsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The thumbnail cache path is outside the project thumbnail cache.");
        }

        return resolved;
    }

    public static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        var buffer = new byte[16 * 1024];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("The generated thumbnail exceeded the cache size limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<InMemoryRandomAccessStream> EncodeJpegAsync(
        IRandomAccessStream source,
        CancellationToken cancellationToken)
    {
        source.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(source);
        cancellationToken.ThrowIfCancellationRequested();
        var decodedBytes = checked((long)decoder.PixelWidth * decoder.PixelHeight * 4);
        if (decodedBytes > MaximumCachedThumbnailBytes)
        {
            throw new InvalidDataException("The generated thumbnail exceeded the cache size limit.");
        }

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        cancellationToken.ThrowIfCancellationRequested();

        var destination = new InMemoryRandomAccessStream();
        try
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destination);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                decoder.OrientedPixelWidth,
                decoder.OrientedPixelHeight,
                decoder.DpiX > 0 ? decoder.DpiX : 96,
                decoder.DpiY > 0 ? decoder.DpiY : 96,
                pixelData.DetachPixelData());
            await encoder.FlushAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (destination.Size > MaximumCachedThumbnailBytes)
            {
                throw new InvalidDataException("The generated thumbnail exceeded the cache size limit.");
            }

            destination.Seek(0);
            return destination;
        }
        catch
        {
            destination.Dispose();
            throw;
        }
    }

    private static bool IsCurrentRequestLocked(ProjectAsset asset, ThumbnailRequest request) =>
        asset.Id == request.AssetId &&
        asset.Kind == request.Kind &&
        !string.IsNullOrWhiteSpace(asset.SourcePath) &&
        string.Equals(
            MediaImportService.NormalizePath(asset.SourcePath),
            request.SourcePath,
            StringComparison.OrdinalIgnoreCase) &&
        asset.FileSize == request.FileSize &&
        asset.LastWriteUtc.ToUniversalTime().Ticks == request.LastWriteUtcTicks;

    private static bool TryCommitCacheHit(ProjectAsset asset, ThumbnailRequest request, string relativePath)
    {
        lock (asset)
        {
            if (!IsCurrentRequestLocked(asset, request))
            {
                return false;
            }

            asset.ThumbnailCachePath = relativePath;
            return true;
        }
    }

    private void EnsureProjectOwnedThumbnailPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        var thumbnailsRoot = Path.GetFullPath(Path.Combine(_projectRootPath, "cache", "thumbnails"));
        if (!resolved.StartsWith(thumbnailsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The temporary thumbnail path is outside the project thumbnail cache.");
        }
    }

    private void TryDeleteProjectOwnedFile(string path)
    {
        EnsureProjectOwnedThumbnailPath(path);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

internal readonly record struct ThumbnailRequest(
    Guid AssetId,
    ProjectAssetKind Kind,
    string SourcePath,
    ulong FileSize,
    long LastWriteUtcTicks,
    int RequestedSize,
    string CacheKey);
