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
    private const string RelativeCachePathPrefix = "cache/thumbnails/";
    private const string RelativeCachePathSuffix = ".jpg";
    private const string CacheSizeLimitMessage = "The generated thumbnail exceeded the cache size limit.";

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
        ProjectService.RejectReparsePoints(_projectRootPath);
    }

    public async Task<string?> GetOrCreateThumbnailAsync(
        StorageFile file,
        ProjectAsset asset,
        int requestedSize = DefaultRequestedSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(asset);
        var normalizedRequestedSize = NormalizeRequestedSize(requestedSize);

        if (asset.Kind == ProjectAssetKind.Audio || asset.IsMissing)
        {
            return null;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var currentRequest = await CaptureCurrentRequestAsync(
                file,
                asset,
                normalizedRequestedSize,
                cancellationToken);
            if (currentRequest is not { } request)
            {
                return null;
            }

            var relativePath = CreateRelativeCachePath(request.CacheKey);
            var cachePath = ResolveProjectCachePath(_projectRootPath, relativePath);
            var cacheResult = await TryUseCachedThumbnailAsync(
                file,
                asset,
                request,
                relativePath,
                cachePath,
                cancellationToken);
            if (cacheResult.IsComplete)
            {
                return cacheResult.Path;
            }

            if (cacheResult.ShouldRetry)
            {
                continue;
            }

            var generationResult = await GenerateThumbnailAsync(
                file,
                asset,
                request,
                relativePath,
                cachePath,
                cancellationToken);
            if (generationResult.IsComplete)
            {
                return generationResult.Path;
            }

            if (!generationResult.ShouldRetry)
            {
                return null;
            }
        }

        return null;
    }

    private async Task<ThumbnailAttemptResult> TryUseCachedThumbnailAsync(
        StorageFile file,
        ProjectAsset asset,
        ThumbnailRequest request,
        string relativePath,
        string cachePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            return ThumbnailAttemptResult.Pending;
        }

        if (!await IsUsableCachedThumbnailAsync(cachePath, cancellationToken))
        {
            TryDeleteProjectOwnedFile(cachePath);
            return ThumbnailAttemptResult.Pending;
        }

        if (!await SourceStillMatchesRequestAsync(file, asset, request, cancellationToken))
        {
            return ThumbnailAttemptResult.Retry;
        }

        var path = TryCommitCacheHit(asset, request, relativePath, cancellationToken) ? cachePath : null;
        return ThumbnailAttemptResult.Complete(path);
    }

    private async Task<ThumbnailAttemptResult> GenerateThumbnailAsync(
        StorageFile file,
        ProjectAsset asset,
        ThumbnailRequest request,
        string relativePath,
        string cachePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mode = request.Kind == ProjectAssetKind.Image ? ThumbnailMode.PicturesView : ThumbnailMode.VideosView;
        using var thumbnail = await file.GetThumbnailAsync(
                mode,
                checked((uint)request.RequestedSize),
                ThumbnailOptions.ResizeThumbnail)
            .AsTask(cancellationToken);
        if (thumbnail is null || thumbnail.Type != ThumbnailType.Image)
        {
            return ThumbnailAttemptResult.Complete(null);
        }

        using var encodedThumbnail = await EncodeJpegAsync(thumbnail, request.RequestedSize, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        var temporaryPath = cachePath + $".{Guid.NewGuid():N}.tmp";
        ProjectService.RejectReparsePoints(temporaryPath);
        try
        {
            await WriteTemporaryThumbnailAsync(encodedThumbnail, temporaryPath, cancellationToken);
            if (!await SourceStillMatchesRequestAsync(file, asset, request, cancellationToken))
            {
                return ThumbnailAttemptResult.Retry;
            }

            var path = TryCommitGeneratedThumbnail(
                asset,
                request,
                temporaryPath,
                relativePath,
                cancellationToken)
                    ? cachePath
                    : null;
            return ThumbnailAttemptResult.Complete(path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                EnsureProjectOwnedThumbnailPath(temporaryPath);
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task WriteTemporaryThumbnailAsync(
        InMemoryRandomAccessStream encodedThumbnail,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            16 * 1024,
            FileOptions.Asynchronous);
        using var source = encodedThumbnail.AsStreamForRead();
        await CopyBoundedAsync(
            source,
            destination,
            MaximumCachedThumbnailBytes,
            cancellationToken);
        await destination.FlushAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
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
        var normalizedRequestedSize = NormalizeRequestedSize(requestedSize);
        var normalized = MediaImportService.NormalizePath(sourcePath).ToUpperInvariant();
        var input = $"{normalized}\n{fileSize}\n{lastWriteUtc.ToUniversalTime().Ticks}\n{normalizedRequestedSize}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    internal static ThumbnailRequest CaptureRequest(ProjectAsset asset, int requestedSize)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var normalizedRequestedSize = NormalizeRequestedSize(requestedSize);

        lock (asset)
        {
            return CaptureRequestLocked(asset, normalizedRequestedSize);
        }
    }

    private async Task<ThumbnailRequest?> CaptureCurrentRequestAsync(
        StorageFile file,
        ProjectAsset asset,
        int requestedSize,
        CancellationToken cancellationToken)
    {
        var basic = await file.GetBasicPropertiesAsync().AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var fileSize = basic.Size;
        var lastWriteUtc = basic.DateModified.ToUniversalTime();

        string? obsoleteCachePath = null;
        ThumbnailRequest? request;
        lock (asset)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (asset.Kind == ProjectAssetKind.Audio ||
                asset.IsMissing ||
                string.IsNullOrWhiteSpace(asset.SourcePath) ||
                !string.Equals(
                    MediaImportService.NormalizePath(asset.SourcePath),
                    MediaImportService.NormalizePath(file.Path),
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (asset.FileSize != fileSize ||
                asset.LastWriteUtc.ToUniversalTime() != lastWriteUtc)
            {
                obsoleteCachePath = asset.ThumbnailCachePath;
                asset.FileSize = fileSize;
                asset.LastWriteUtc = lastWriteUtc;
                asset.ThumbnailCachePath = string.Empty;
            }

            request = CaptureRequestLocked(asset, requestedSize);
        }

        TryDeleteSupersededThumbnail(obsoleteCachePath);
        return request;
    }

    private async Task<bool> SourceStillMatchesRequestAsync(
        StorageFile file,
        ProjectAsset asset,
        ThumbnailRequest request,
        CancellationToken cancellationToken) =>
        await CaptureCurrentRequestAsync(file, asset, request.RequestedSize, cancellationToken) is { } current &&
        current == request;

    private static ThumbnailRequest CaptureRequestLocked(ProjectAsset asset, int requestedSize)
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

    internal static int NormalizeRequestedSize(int requestedSize) =>
        requestedSize <= 0
            ? DefaultRequestedSize
            : Math.Min(requestedSize, MaximumRequestedSize);

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

    internal static bool IsCachePathForRequest(ThumbnailRequest request, string relativePath) =>
        string.Equals(relativePath, CreateRelativeCachePath(request.CacheKey), StringComparison.Ordinal);

    internal bool TryCommitGeneratedThumbnail(
        ProjectAsset asset,
        ThumbnailRequest request,
        string temporaryPath,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!string.Equals(
                relativePath,
                CreateRelativeCachePath(request.CacheKey),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The thumbnail cache path does not match its request.");
        }

        var cachePath = ResolveProjectCachePath(_projectRootPath, relativePath);
        EnsureProjectOwnedThumbnailPath(temporaryPath);

        lock (asset)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentRequestLocked(asset, request) || !SourceStillMatchesRequest(request))
            {
                TryDeleteProjectOwnedFile(temporaryPath);
                return false;
            }

            File.Move(temporaryPath, cachePath, overwrite: true);
            cancellationToken.ThrowIfCancellationRequested();
            asset.ThumbnailCachePath = relativePath;
            return true;
        }
    }

    public static string CreateRelativeCachePath(string cacheKey)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        if (!IsLowercaseSha256(cacheKey))
        {
            throw new ArgumentException("A lowercase SHA-256 cache key is required.", nameof(cacheKey));
        }

        return $"{RelativeCachePathPrefix}{cacheKey}{RelativeCachePathSuffix}";
    }

    internal static bool IsCanonicalRelativeCachePath(string relativePath)
    {
        if (relativePath.Length != RelativeCachePathPrefix.Length + 64 + RelativeCachePathSuffix.Length ||
            !relativePath.StartsWith(RelativeCachePathPrefix, StringComparison.Ordinal) ||
            !relativePath.EndsWith(RelativeCachePathSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        return IsLowercaseSha256(relativePath.AsSpan(RelativeCachePathPrefix.Length, 64));
    }

    public static string ResolveProjectCachePath(string projectRootPath, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("Thumbnail cache paths must be project-relative.");
        }

        if (!IsCanonicalRelativeCachePath(relativePath))
        {
            throw new InvalidDataException("The thumbnail cache path is not in the canonical format.");
        }

        var root = Path.GetFullPath(projectRootPath);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        var thumbnailsRoot = Path.GetFullPath(Path.Combine(root, "cache", "thumbnails"));
        if (!resolved.StartsWith(thumbnailsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The thumbnail cache path is outside the project thumbnail cache.");
        }

        ProjectService.RejectReparsePoints(resolved);
        return resolved;
    }

    private static bool IsLowercaseSha256(ReadOnlySpan<char> value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    internal static async Task<bool> IsUsableCachedThumbnailAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            if (fileStream.Length is <= 0 or > MaximumCachedThumbnailBytes)
            {
                return false;
            }

            using IRandomAccessStream randomAccess = fileStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, randomAccess).AsTask(cancellationToken);
            var stride = checked((long)decoder.PixelWidth * 4);
            var decodedBytes = checked(stride * decoder.PixelHeight);
            if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || decodedBytes > MaximumCachedThumbnailBytes)
            {
                return false;
            }

            var pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    new BitmapTransform(),
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken);
            _ = pixels.DetachPixelData();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            return false;
        }
    }

    public static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        if (source.CanSeek)
        {
            var remaining = checked(source.Length - source.Position);
            if (remaining < 0 || remaining > maximumBytes)
            {
                throw new InvalidDataException(CacheSizeLimitMessage);
            }
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
                throw new InvalidDataException(CacheSizeLimitMessage);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<InMemoryRandomAccessStream> EncodeJpegAsync(
        StorageItemThumbnail source,
        int requestedSize,
        CancellationToken cancellationToken)
    {
        var normalizedRequestedSize = NormalizeRequestedSize(requestedSize);
        if (source.Size is 0 or > MaximumCachedThumbnailBytes)
        {
            throw new InvalidDataException(CacheSizeLimitMessage);
        }

        source.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(source).AsTask(cancellationToken);
        var decodedWidth = decoder.OrientedPixelWidth;
        var decodedHeight = decoder.OrientedPixelHeight;
        var maximumDimension = checked((uint)normalizedRequestedSize);
        if (decodedWidth == 0 ||
            decodedHeight == 0 ||
            decodedWidth > maximumDimension ||
            decodedHeight > maximumDimension)
        {
            throw new InvalidDataException("The generated thumbnail dimensions exceeded the requested size.");
        }

        var stride = checked((long)decodedWidth * 4);
        var decodedBytes = checked(stride * decodedHeight);
        if (decodedBytes > MaximumCachedThumbnailBytes)
        {
            throw new InvalidDataException(CacheSizeLimitMessage);
        }

        var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb)
            .AsTask(cancellationToken);

        var destination = new InMemoryRandomAccessStream();
        try
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destination).AsTask(cancellationToken);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                decodedWidth,
                decodedHeight,
                decoder.DpiX > 0 ? decoder.DpiX : 96,
                decoder.DpiY > 0 ? decoder.DpiY : 96,
                pixelData.DetachPixelData());
            await encoder.FlushAsync().AsTask(cancellationToken);
            if (destination.Size > MaximumCachedThumbnailBytes)
            {
                throw new InvalidDataException(CacheSizeLimitMessage);
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

    private static bool SourceStillMatchesRequest(ThumbnailRequest request)
    {
        try
        {
            var file = new FileInfo(request.SourcePath);
            return file.Exists &&
                   checked((ulong)file.Length) == request.FileSize &&
                   new DateTimeOffset(file.LastWriteTimeUtc).ToUniversalTime().Ticks == request.LastWriteUtcTicks;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException or PathTooLongException or
                                              OverflowException or System.Security.SecurityException)
        {
            return false;
        }
    }

    internal static bool TryCommitCacheHit(
        ProjectAsset asset,
        ThumbnailRequest request,
        string relativePath,
        CancellationToken cancellationToken)
    {
        lock (asset)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentRequestLocked(asset, request) || !SourceStillMatchesRequest(request))
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

        ProjectService.RejectReparsePoints(resolved);
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
            // Cache deletion is best effort and must not block the project edit.
        }
    }

    private void TryDeleteSupersededThumbnail(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !IsCanonicalRelativeCachePath(relativePath))
        {
            return;
        }

        TryDeleteProjectOwnedFile(ResolveProjectCachePath(_projectRootPath, relativePath));
    }

    private readonly record struct ThumbnailAttemptResult(bool IsComplete, bool ShouldRetry, string? Path)
    {
        public static ThumbnailAttemptResult Pending => new(false, false, null);
        public static ThumbnailAttemptResult Retry => new(false, true, null);
        public static ThumbnailAttemptResult Complete(string? path) => new(true, false, path);
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
