using CutFlow.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class MediaImportService
{
    public const long DefaultImageDurationMilliseconds = 5_000;

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
            var displayName = file?.Name ?? "Unknown file";
            if (file is null)
            {
                results.Add(ImportResult.Failure(displayName, "The dropped item is not a file."));
                continue;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    results.Add(ImportResult.Failure(displayName, $"{AppInfo.ProductName} cannot access this file by a local path."));
                    continue;
                }

                var normalizedPath = NormalizePath(file.Path);
                if (IsPathWithinDirectory(normalizedPath, managedProjectsRootPath))
                {
                    results.Add(ImportResult.Failure(
                        displayName,
                        $"Choose source media outside {AppInfo.ProductName}'s managed project folders."));
                    continue;
                }

                if (knownPaths.Contains(normalizedPath))
                {
                    results.Add(ImportResult.Duplicate(displayName));
                    continue;
                }

                var metadata = await ReadMetadataAsync(file, cancellationToken);
                results.Add(ImportResult.Success(CreateAsset(metadata)));
                knownPaths.Add(normalizedPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedMediaFailure(exception))
            {
                results.Add(ImportResult.Failure(
                    displayName,
                    $"Windows could not read or decode this file. {ReadableReason(exception)}"));
            }
        }

        return results;
    }

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
            var normalizedPath = NormalizePath(replacement.Path);
            if (IsPathWithinDirectory(normalizedPath, managedProjectsRootPath))
            {
                return ImportResult.Failure(
                    replacement.Name,
                    $"Choose source media outside {AppInfo.ProductName}'s managed project folders.");
            }

            if (project.Assets.Any(candidate =>
                    candidate.Id != asset.Id &&
                    !string.IsNullOrWhiteSpace(candidate.SourcePath) &&
                    string.Equals(NormalizePath(candidate.SourcePath), normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                return ImportResult.Failure(replacement.Name, "That file is already imported in this project.");
            }

            var metadata = await ReadMetadataAsync(replacement, cancellationToken);
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

    public Task<int> RefreshMissingAsync(ProjectDocument project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Task.Run(
            () => RefreshMissing(project, path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }),
            cancellationToken);
    }

    internal static int RefreshMissing(ProjectDocument project, Func<string, bool> pathExists)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pathExists);

        var changed = 0;
        foreach (var asset in project.Assets)
        {
            var missing = !pathExists(asset.SourcePath);
            if (asset.IsMissing != missing)
            {
                asset.IsMissing = missing;
                changed++;
            }
        }

        return changed;
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

    internal static bool IsPathWithinDirectory(string path, string directoryPath)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedDirectory = NormalizePath(directoryPath);
        return string.Equals(normalizedPath, normalizedDirectory, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsSourcePath(ProjectDocument project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        var normalized = NormalizePath(path);
        return project.Assets.Any(asset =>
            !string.IsNullOrWhiteSpace(asset.SourcePath) &&
            string.Equals(NormalizePath(asset.SourcePath), normalized, StringComparison.OrdinalIgnoreCase));
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

        var requiredSourceOut = asset.Kind == ProjectAssetKind.Audio
            ? project.AudioItems
                .Where(item => item.AssetId == asset.Id)
                .Select(item => item.SourceOutMilliseconds)
                .DefaultIfEmpty(0)
                .Max()
            : project.VideoItems
                .Where(item => item.AssetId == asset.Id)
                .Select(item => item.SourceOutMilliseconds)
                .DefaultIfEmpty(0)
                .Max();
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

    private static async Task<MediaFileMetadata> ReadMetadataAsync(StorageFile file, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetKind(file.Path, out var kind))
        {
            throw new NotSupportedException("Supported formats are MP4, PNG, JPEG, MP3, and WAV.");
        }

        var basic = await file.GetBasicPropertiesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        long durationMilliseconds;
        int width = 0;
        int height = 0;

        switch (kind)
        {
            case ProjectAssetKind.Video:
            {
                var clip = await MediaClip.CreateFromFileAsync(file);
                cancellationToken.ThrowIfCancellationRequested();
                var properties = await file.Properties.GetVideoPropertiesAsync();
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
                width = checked((int)decoder.PixelWidth);
                height = checked((int)decoder.PixelHeight);
                durationMilliseconds = DefaultImageDurationMilliseconds;
                break;
            }
            default:
                throw new NotSupportedException("Unsupported media kind.");
        }

        return new MediaFileMetadata(
            NormalizePath(file.Path),
            file.Name,
            kind,
            durationMilliseconds,
            width,
            height,
            basic.Size,
            basic.DateModified.ToUniversalTime());
    }

    private static long ToMilliseconds(TimeSpan duration) => checked((long)Math.Round(duration.TotalMilliseconds));

    private static bool IsExpectedMediaFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or System.Runtime.InteropServices.COMException;

    private static string ReadableReason(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Access was denied.",
        NotSupportedException => exception.Message,
        InvalidDataException => exception.Message,
        _ => "The file may be unsupported, damaged, moved, or inaccessible."
    };
}

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
