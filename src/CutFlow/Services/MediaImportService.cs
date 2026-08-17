using System.Diagnostics.CodeAnalysis;
using CutFlow.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class MediaImportService
{
    public const long DefaultImageDurationMilliseconds = 5_000;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance method preserves the injected service API.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "The instance method preserves the injected service API.")]
    public async Task<IReadOnlyList<ImportResult>> ImportAsync(
        IReadOnlyList<StorageFile> files,
        ProjectDocument project,
        string managedProjectsRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedProjectsRootPath);

        var knownPaths = project.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.SourcePath))
            .Select(asset => NormalizePath(asset.SourcePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportResult>(files.Count);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ImportFileAsync(file, managedProjectsRootPath, knownPaths, cancellationToken));
        }

        return results;
    }

    private static async Task<ImportResult> ImportFileAsync(
        StorageFile? file,
        string managedProjectsRootPath,
        HashSet<string> knownPaths,
        CancellationToken cancellationToken)
    {
        var displayName = file?.Name ?? "Unknown file";
        if (file is null) return ImportResult.Failure(displayName, "The dropped item is not a file.");

        try
        {
            if (!TryResolveLocalSourcePath(file.Path, out var normalizedPath, out var pathError))
                return ImportResult.Failure(displayName, pathError!);
            if (IsPathWithinDirectory(normalizedPath, managedProjectsRootPath))
                return ImportResult.Failure(displayName, $"Choose source media outside {AppInfo.ProductName}'s managed project folders.");
            if (knownPaths.Contains(normalizedPath)) return ImportResult.Duplicate(displayName);

            var metadata = await ReadMetadataAsync(file, normalizedPath, cancellationToken);
            if (!TryResolveLocalSourcePath(file.Path, out var currentPath, out pathError) ||
                !string.Equals(currentPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                return ImportResult.Failure(displayName, pathError ?? "The file path changed during import. Choose the file again.");
            if (!MatchesSnapshot(normalizedPath, metadata))
                return ImportResult.Failure(displayName, "The file changed during import. Choose the file again.");

            knownPaths.Add(normalizedPath);
            return ImportResult.Success(CreateAsset(metadata));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedMediaFailure(exception))
        {
            return ImportResult.Failure(displayName, $"Windows could not read or decode this file. {ReadableReason(exception)}");
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance method preserves the injected service API.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "The instance method preserves the injected service API.")]
    public async Task<ImportResult> RelinkAsync(
        StorageFile replacement,
        ProjectAsset asset,
        ProjectDocument project,
        string managedProjectsRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(managedProjectsRootPath);

        try
        {
            if (!TryResolveLocalSourcePath(replacement.Path, out var normalizedPath, out var pathError))
            {
                return ImportResult.Failure(replacement.Name, pathError!);
            }

            if (IsPathWithinDirectory(normalizedPath, managedProjectsRootPath))
            {
                return ImportResult.Failure(
                    replacement.Name,
                    $"Choose source media outside {AppInfo.ProductName}'s managed project folders.");
            }

            if (ContainsSourcePath(project, normalizedPath, asset.Id))
            {
                return ImportResult.Failure(replacement.Name, "That file is already imported in this project.");
            }

            var metadata = await ReadMetadataAsync(replacement, normalizedPath, cancellationToken);
            if (!TryResolveLocalSourcePath(replacement.Path, out var currentPath, out pathError) ||
                !string.Equals(currentPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return ImportResult.Failure(replacement.Name, pathError ?? "The replacement path changed during relink. Choose the file again.");
            }

            if (!MatchesSnapshot(normalizedPath, metadata))
            {
                return ImportResult.Failure(replacement.Name, "The replacement file changed during relink. Choose the file again.");
            }

            if (!TryApplyRelink(asset, metadata, project, out var error))
            {
                return ImportResult.Failure(replacement.Name, error!);
            }

            return ImportResult.Success(asset);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedMediaFailure(exception))
        {
            return ImportResult.Failure(
                replacement.Name,
                $"Windows could not read or decode this replacement. {ReadableReason(exception)}");
        }
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance method preserves the injected service API.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "The instance method preserves the injected service API.")]
    public Task<int> RefreshMissingAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Task.Run(
            () => RefreshMissing(project, File.Exists, cancellationToken),
            cancellationToken);
    }

    internal static int RefreshMissing(
        ProjectDocument project,
        Func<string, bool> pathExists,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pathExists);
        ArgumentNullException.ThrowIfNull(project);

        var changed = 0;
        foreach (var asset in project.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = TryNormalizeCurrentSourcePath(asset.SourcePath, out var normalizedPath) &&
                SourceExists(normalizedPath, pathExists);
            cancellationToken.ThrowIfCancellationRequested();
            var missing = !exists;
            if (asset.IsMissing != missing)
            {
                asset.IsMissing = missing;
                changed++;
            }
        }

        return changed;
    }

    private static bool TryNormalizeCurrentSourcePath(string? path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            normalizedPath = NormalizePath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or
                                              System.Security.SecurityException)
        {
            return false;
        }
    }

    private static bool SourceExists(string normalizedPath, Func<string, bool> pathExists)
    {
        try
        {
            return pathExists(normalizedPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException or PathTooLongException or
                                              System.Security.SecurityException)
        {
            return false;
        }
    }

    internal static bool SourceMatchesRecordedSnapshot(ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (string.IsNullOrWhiteSpace(asset.SourcePath))
        {
            return false;
        }

        if (!TryReadSnapshot(asset.SourcePath, out var current))
        {
            return false;
        }

        if (asset.LastWriteUtc == default)
        {
            // Older project documents did not necessarily persist a source snapshot.
            return true;
        }

        return current == new SourceFileSnapshot(asset.FileSize, asset.LastWriteUtc.ToUniversalTime());
    }

    public static bool TryGetKind(string path, out ProjectAssetKind kind)
    {
        kind = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" => ProjectAssetKind.Video,
            ".png" or ".jpg" or ".jpeg" => ProjectAssetKind.Image,
            ".mp3" or ".wav" => ProjectAssetKind.Audio,
            _ => default
        };
        return Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".png" or ".jpg" or ".jpeg" or ".mp3" or ".wav";
    }

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    internal static bool TryResolveLocalSourcePath(
        string? path,
        out string normalizedPath,
        out string? error)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = $"{AppInfo.ProductName} needs a usable local file-system path. Download or copy this item to a local folder, then try again.";
            return false;
        }

        try
        {
            if (!Path.IsPathFullyQualified(path))
            {
                error = $"{AppInfo.ProductName} needs a fully qualified local file-system path. Choose the file again.";
                return false;
            }

            normalizedPath = NormalizePath(path);
            if (normalizedPath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                normalizedPath.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                normalizedPath = string.Empty;
                error = "Windows device paths are not supported. Choose the file through File Explorer or copy it to a regular drive or network folder.";
                return false;
            }

            if (!File.Exists(normalizedPath))
            {
                normalizedPath = string.Empty;
                error = "The file is no longer available at its local path. Restore or download it, then try again.";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalizedPath = string.Empty;
            error = $"{AppInfo.ProductName} cannot use this file-system path. Choose a local media file through File Explorer.";
            return false;
        }
    }

    internal static bool IsPathWithinDirectory(string path, string directoryPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedDirectory = NormalizePath(directoryPath);
        return string.Equals(normalizedPath, normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsSourcePath(ProjectDocument project, string path)
        => ContainsSourcePath(project, path, excludedAssetId: null);

    internal static bool ContainsSourcePath(ProjectDocument project, string path, Guid? excludedAssetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var normalized = NormalizePath(path);
        return project.Assets.Any(asset =>
            asset.Id != excludedAssetId &&
            !string.IsNullOrWhiteSpace(asset.SourcePath) &&
            string.Equals(NormalizePath(asset.SourcePath), normalized, StringComparison.OrdinalIgnoreCase));
    }

    internal static IReadOnlyList<ImportResult> RevalidateDuplicateResults(
        ProjectDocument project,
        IReadOnlyList<ImportResult> results)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(results);

        var knownPaths = project.Assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.SourcePath))
            .Select(asset => NormalizePath(asset.SourcePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return results
            .Select(result => result.Asset is not null && !knownPaths.Add(NormalizePath(result.Asset.SourcePath))
                ? ImportResult.Duplicate(result.FileName)
                : result)
            .ToList();
    }

    internal static IReadOnlyList<ImportResult> MergeImportResults(
        IReadOnlyList<ImportResult?> resultSlots,
        IReadOnlyList<ImportResult> preparedResults)
    {
        ArgumentNullException.ThrowIfNull(resultSlots);
        ArgumentNullException.ThrowIfNull(preparedResults);

        var preparedIndex = 0;
        var merged = new List<ImportResult>(resultSlots.Count);
        foreach (var slot in resultSlots)
        {
            if (slot is not null)
            {
                merged.Add(slot);
                continue;
            }

            if (preparedIndex >= preparedResults.Count)
            {
                throw new ArgumentException("The prepared import results do not match the batch slots.", nameof(preparedResults));
            }

            merged.Add(preparedResults[preparedIndex++]);
        }

        if (preparedIndex != preparedResults.Count)
        {
            throw new ArgumentException("The prepared import results do not match the batch slots.", nameof(preparedResults));
        }

        return merged;
    }

    public static ProjectAsset CreateAsset(MediaFileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var duration = metadata.Kind == ProjectAssetKind.Image
            ? DefaultImageDurationMilliseconds
            : metadata.DurationMilliseconds;
        if (metadata.Kind is ProjectAssetKind.Video or ProjectAssetKind.Audio &&
            duration < ProjectDocument.MinimumItemDurationMilliseconds)
        {
            throw new InvalidDataException(
                $"Media must be at least {ProjectDocument.MinimumItemDurationMilliseconds} ms long.");
        }

        return new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = metadata.Kind,
            SourcePath = NormalizePath(metadata.SourcePath),
            FileName = metadata.FileName,
            DurationMilliseconds = duration,
            Width = metadata.Width,
            Height = metadata.Height,
            FileSize = metadata.FileSize,
            LastWriteUtc = metadata.LastWriteUtc.ToUniversalTime(),
            ThumbnailCachePath = string.Empty,
            IsMissing = false
        };
    }

    public static bool TryApplyRelink(
        ProjectAsset asset,
        MediaFileMetadata replacement,
        ProjectDocument project,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(project);
        if (asset.Kind != replacement.Kind)
        {
            error = $"Choose a {asset.Kind.ToString().ToLowerInvariant()} file to replace this {asset.Kind} asset.";
            return false;
        }

        ProjectAsset mapped;
        try
        {
            mapped = CreateAsset(replacement);
        }
        catch (InvalidDataException exception)
        {
            error = exception.Message;
            return false;
        }

        var requiredSourceOut = GetRequiredSourceOutMilliseconds(project, asset.Id);
        if (mapped.DurationMilliseconds < requiredSourceOut)
        {
            error = $"'{mapped.FileName}' is too short for existing timeline references through {requiredSourceOut} ms.";
            return false;
        }

        asset.SourcePath = mapped.SourcePath;
        asset.FileName = mapped.FileName;
        asset.DurationMilliseconds = mapped.DurationMilliseconds;
        asset.Width = mapped.Width;
        asset.Height = mapped.Height;
        asset.FileSize = mapped.FileSize;
        asset.LastWriteUtc = mapped.LastWriteUtc;
        asset.ThumbnailCachePath = string.Empty;
        asset.IsMissing = false;
        error = null;
        return true;
    }

    internal static long GetRequiredSourceOutMilliseconds(ProjectDocument project, Guid assetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.VideoItems
            .Where(item => item.AssetId == assetId)
            .Select(item => item.SourceOutMilliseconds)
            .Concat(project.AudioItems
                .Where(item => item.AssetId == assetId)
                .Select(item => item.SourceOutMilliseconds))
            .DefaultIfEmpty(0)
            .Max();
    }

    public static bool IsAssetReferenced(ProjectDocument project, Guid assetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.VideoItems.Any(item => item.AssetId == assetId) ||
               project.AudioItems.Any(item => item.AssetId == assetId);
    }

    public static bool TryRemoveAssetReference(ProjectDocument project, Guid assetId, out string? error)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (IsAssetReferenced(project, assetId))
        {
            error = "This asset is used on the timeline. Remove its timeline items first.";
            return false;
        }

        var removed = project.Assets.RemoveAll(asset => asset.Id == assetId) > 0;
        error = removed ? null : "The asset is no longer in this project.";
        return removed;
    }

    private static async Task<MediaFileMetadata> ReadMetadataAsync(
        StorageFile file,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetKind(normalizedPath, out var kind))
        {
            throw new NotSupportedException("Supported formats are MP4, PNG, JPEG, MP3, and WAV.");
        }

        var beforeValidation = await ReadSnapshotAsync(file, cancellationToken);
        long durationMilliseconds;
        int width = 0;
        int height = 0;

        switch (kind)
        {
            case ProjectAssetKind.Video:
                {
                    var clip = await MediaClip.CreateFromFileAsync(file);
                    cancellationToken.ThrowIfCancellationRequested();
                    var properties = clip.GetVideoEncodingProperties();
                    if (properties.Width == 0 || properties.Height == 0)
                    {
                        throw new InvalidDataException("The file does not contain a decodable video stream.");
                    }

                    durationMilliseconds = ToMilliseconds(clip.OriginalDuration);
                    width = checked((int)properties.Width);
                    height = checked((int)properties.Height);
                    break;
                }
            case ProjectAssetKind.Audio:
                {
                    var track = await BackgroundAudioTrack.CreateFromFileAsync(file);
                    cancellationToken.ThrowIfCancellationRequested();
                    durationMilliseconds = ToMilliseconds(track.OriginalDuration);
                    break;
                }
            case ProjectAssetKind.Image:
                {
                    using var stream = await file.OpenReadAsync();
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                    width = checked((int)decoder.OrientedPixelWidth);
                    height = checked((int)decoder.OrientedPixelHeight);
                    durationMilliseconds = DefaultImageDurationMilliseconds;
                    break;
                }
            default:
                throw new NotSupportedException("Unsupported media kind.");
        }

        var afterValidation = await ReadSnapshotAsync(file, cancellationToken);
        if (beforeValidation != afterValidation)
        {
            throw new InvalidDataException("The file changed while Windows was validating it. Choose the file again.");
        }

        return new MediaFileMetadata(
            normalizedPath,
            Path.GetFileName(normalizedPath),
            kind,
            durationMilliseconds,
            width,
            height,
            afterValidation.FileSize,
            afterValidation.LastWriteUtc);
    }

    private static async Task<SourceFileSnapshot> ReadSnapshotAsync(
        StorageFile file,
        CancellationToken cancellationToken)
    {
        var basic = await file.GetBasicPropertiesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return new SourceFileSnapshot(basic.Size, basic.DateModified.ToUniversalTime());
    }

    private static bool MatchesSnapshot(string path, MediaFileMetadata metadata) =>
        TryReadSnapshot(path, out var current) &&
        current == new SourceFileSnapshot(metadata.FileSize, metadata.LastWriteUtc.ToUniversalTime());

    private static bool TryReadSnapshot(string path, out SourceFileSnapshot snapshot)
    {
        snapshot = default;
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return false;
            }

            snapshot = new SourceFileSnapshot(
                checked((ulong)file.Length),
                new DateTimeOffset(file.LastWriteTimeUtc));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              ArgumentException or NotSupportedException or PathTooLongException or
                                              OverflowException or System.Security.SecurityException)
        {
            return false;
        }
    }

    private static long ToMilliseconds(TimeSpan duration) => checked((long)Math.Round(duration.TotalMilliseconds));

    internal static bool IsExpectedMediaFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or System.Runtime.InteropServices.COMException;

    private static string ReadableReason(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Access was denied.",
        NotSupportedException => exception.Message,
        InvalidDataException => exception.Message,
        _ => "The file may be unsupported, damaged, moved, or inaccessible."
    };
}

internal readonly record struct SourceFileSnapshot(ulong FileSize, DateTimeOffset LastWriteUtc);

public sealed record MediaFileMetadata(
    string SourcePath,
    string FileName,
    ProjectAssetKind Kind,
    long DurationMilliseconds,
    int Width,
    int Height,
    ulong FileSize,
    DateTimeOffset LastWriteUtc);

public sealed record ImportResult(string FileName, ProjectAsset? Asset, string? ErrorMessage, bool IsDuplicate)
{
    public bool IsSuccess => Asset is not null;

    public static ImportResult Success(ProjectAsset asset) => new(asset.FileName, asset, null, false);

    public static ImportResult Failure(string fileName, string message) => new(fileName, null, message, false);

    public static ImportResult Duplicate(string fileName) =>
        new(fileName, null, $"'{fileName}' is already imported in this project.", true);
}
