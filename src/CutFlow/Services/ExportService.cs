using CutFlow.Models;
using CutFlow.Utilities;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace CutFlow.Services;

internal delegate Task<TranscodeFailureReason> ExportRenderAsync(
    MediaComposition composition,
    StorageFile stagingFile,
    MediaEncodingProfile profile,
    IProgress<double> progress,
    CancellationToken cancellationToken);

public sealed class ExportService
{
    internal const int MaximumStagingBaseNameLength = 96;
    internal const int MaximumStagingFileNameLength = MaximumStagingBaseNameLength + 45;

    private readonly CompositionService _compositionService;
    private readonly TextOverlayRenderer? _textOverlayRenderer;
    private readonly ExportRenderAsync _renderAsync;
    private readonly Func<StorageFile, Task> _cleanupAsync;
    private readonly SimpleLogService? _logService;

    public ExportService(
        CompositionService? compositionService = null,
        TextOverlayRenderer? textOverlayRenderer = null,
        SimpleLogService? logService = null)
        : this(
            compositionService ?? new CompositionService(),
            textOverlayRenderer,
            RenderNativeAsync,
            logService: logService)
    {
    }

    internal ExportService(
        CompositionService compositionService,
        TextOverlayRenderer? textOverlayRenderer,
        ExportRenderAsync renderAsync,
        Func<StorageFile, Task>? cleanupAsync = null,
        SimpleLogService? logService = null)
    {
        _compositionService = compositionService ?? throw new ArgumentNullException(nameof(compositionService));
        _textOverlayRenderer = textOverlayRenderer;
        _renderAsync = renderAsync ?? throw new ArgumentNullException(nameof(renderAsync));
        _cleanupAsync = cleanupAsync ?? DeleteStagingAsync;
        _logService = logService;
    }

    public async Task<ExportResult> ExportAsync(
        ProjectDocument project,
        StorageFile destination,
        ExportOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return await ExportToPathAsync(project, destination.Path, options, progress, cancellationToken);
    }

    public async Task<ExportResult> ExportToPathAsync(
        ProjectDocument project,
        string destinationPath,
        ExportOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken) =>
        await ExportToPathAsync(
            project,
            project,
            destinationPath,
            options,
            progress,
            cancellationToken);

    internal async Task<ExportResult> ExportToPathAsync(
        ProjectDocument project,
        ProjectDocument sourceGuardProject,
        string destinationPath,
        ExportOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceGuardProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        project = CreateProjectSnapshot(project);
        destinationPath = Path.GetFullPath(destinationPath);

        if (IsSourceMediaPath(project, sourceGuardProject, destinationPath))
        {
            return SourceMediaConflict(destinationPath);
        }

        var validation = await ExportPreflight.ValidateAsync(
            project,
            refreshMissingFlags: true,
            cancellationToken);
        if (!validation.CanExport)
        {
            return new ExportResult(ExportResultStatus.ValidationFailed, destinationPath, validation.ErrorMessage);
        }

        if (project.Settings.TextTrackVisible &&
            project.TextItems.Any(item => item.DurationMilliseconds > 0) &&
            _textOverlayRenderer?.RenderHost is null)
        {
            return new ExportResult(
                ExportResultStatus.Failed,
                destinationPath,
                "Text overlays cannot be exported because the text renderer is unavailable.");
        }

        progress.Report(0);
        var profile = ExportEncodingProfile.Create(project.Settings.AspectRatio, options);
        var build = await _compositionService.BuildAsync(
            project,
            _textOverlayRenderer,
            checked((int)profile.Video.Width),
            checked((int)profile.Video.Height),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!build.HasContent || build.Errors.Count > 0)
        {
            var message = build.Errors.Count > 0
                ? string.Join(" ", build.Errors)
                : "The project did not produce an exportable composition.";
            return new ExportResult(ExportResultStatus.Failed, destinationPath, message);
        }

        if (IsSourceMediaPath(project, sourceGuardProject, destinationPath))
        {
            return SourceMediaConflict(destinationPath);
        }

        var directoryPath = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The export destination folder is unavailable.");
        var folder = await StorageFolder.GetFolderFromPathAsync(directoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        StorageFile? staging = null;
        try
        {
            string stagingFileName;
            do
            {
                stagingFileName = CreateStagingFileName(destinationPath, Guid.NewGuid());
            }
            while (IsSourceMediaPath(
                project,
                sourceGuardProject,
                Path.Combine(directoryPath, stagingFileName)));

            staging = await folder.CreateFileAsync(stagingFileName, CreationCollisionOption.FailIfExists);
            cancellationToken.ThrowIfCancellationRequested();
            var finalValidation = await ExportPreflight.ValidateAsync(
                project,
                refreshMissingFlags: true,
                cancellationToken);
            if (!finalValidation.CanExport)
            {
                return new ExportResult(
                    ExportResultStatus.ValidationFailed,
                    destinationPath,
                    finalValidation.ErrorMessage);
            }

            var failure = await _renderAsync(build.Composition, staging, profile, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (failure != TranscodeFailureReason.None)
            {
                return new ExportResult(
                    ExportResultStatus.Failed,
                    destinationPath,
                    ExportFailureMapper.GetMessage(failure) ?? "Export failed.");
            }

            if (IsSourceMediaPath(project, sourceGuardProject, destinationPath))
            {
                return SourceMediaConflict(destinationPath);
            }

            await CommitAsync(staging.Path, destinationPath);
            staging = null;
            progress.Report(100);
            return new ExportResult(ExportResultStatus.Success, destinationPath, string.Empty);
        }
        finally
        {
            if (staging is not null)
            {
                try
                {
                    await _cleanupAsync(staging);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Secondary cleanup must not replace the primary export failure or cancellation.
                    if (_logService is not null)
                    {
                        _ = _logService.TryWriteAsync(
                            $"Export staging cleanup failed: {exception.GetType().Name}");
                    }
                }
            }
        }
    }

    private static async Task DeleteStagingAsync(StorageFile staging) =>
        await staging.DeleteAsync(StorageDeleteOption.PermanentDelete);

    internal static string CreateStagingFileName(string destinationPath, Guid operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var baseName = Path.GetFileNameWithoutExtension(destinationPath);
        if (baseName.Length > MaximumStagingBaseNameLength)
        {
            var length = MaximumStagingBaseNameLength;
            if (char.IsHighSurrogate(baseName[length - 1]) && char.IsLowSurrogate(baseName[length]))
            {
                length--;
            }

            baseName = baseName[..length];
        }

        return $"{baseName}.cutflow-{operationId:N}.mp4";
    }

    internal static Task CommitAsync(
        string stagingPath,
        string destinationPath,
        Action<string, string>? move = null) =>
        Task.Run(() => (move ?? MoveStagingFile)(stagingPath, destinationPath));

    private static void MoveStagingFile(string stagingPath, string destinationPath) =>
        File.Move(stagingPath, destinationPath, overwrite: true);

    internal static ProjectDocument CreateProjectSnapshot(ProjectDocument project) =>
        ProjectDocumentCloner.Clone(project);

    private static ExportResult SourceMediaConflict(string destinationPath) =>
        new(
            ExportResultStatus.ValidationFailed,
            destinationPath,
            "Choose a different output file. Export cannot overwrite source media used by this project.");

    private static bool IsSourceMediaPath(
        ProjectDocument project,
        ProjectDocument sourceGuardProject,
        string destinationPath) =>
        IsSourceMediaPath(project, destinationPath) ||
        (!ReferenceEquals(project, sourceGuardProject) && IsSourceMediaPath(sourceGuardProject, destinationPath));

    private static bool IsSourceMediaPath(ProjectDocument project, string destinationPath)
    {
        foreach (var asset in project.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.SourcePath))
            {
                continue;
            }

            try
            {
                if (string.Equals(
                    Path.GetFullPath(asset.SourcePath),
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Invalid source paths are reported by preflight as missing media.
            }
        }

        return false;
    }

    private static async Task<TranscodeFailureReason> RenderNativeAsync(
        MediaComposition composition,
        StorageFile stagingFile,
        MediaEncodingProfile profile,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        var operation = composition.RenderToFileAsync(
            stagingFile,
            MediaTrimmingPreference.Precise,
            profile);
        return await operation.AsTask(cancellationToken, progress);
    }
}

public enum ExportResultStatus
{
    Success,
    ValidationFailed,
    Failed
}

public sealed record ExportResult(
    ExportResultStatus Status,
    string DestinationPath,
    string ErrorMessage);

public static class ExportPreflight
{
    private const int MaximumReportedMissingAssets = 3;
    private const int MaximumMissingAssetNameLength = 96;
    private const int MaximumMissingAssetPathLength = 120;

    public static ExportPreflightResult Validate(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ValidateCore(
            project,
            refreshMissingFlags: false,
            MediaImportService.SourceMatchesRecordedSnapshot,
            CancellationToken.None);
    }

    public static Task<ExportPreflightResult> ValidateAsync(
        ProjectDocument project,
        CancellationToken cancellationToken = default) =>
        ValidateAsync(project, refreshMissingFlags: false, cancellationToken);

    internal static Task<ExportPreflightResult> ValidateAsync(
        ProjectDocument project,
        bool refreshMissingFlags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Task.Run(
            () => ValidateCore(
                project,
                refreshMissingFlags,
                MediaImportService.SourceMatchesRecordedSnapshot,
                cancellationToken),
            cancellationToken);
    }

    internal static Task<ExportPreflightResult> ValidateAsync(
        ProjectDocument project,
        bool refreshMissingFlags,
        Func<string, bool> fileExists,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileExists);
        return Task.Run(
            () => ValidateCore(project, refreshMissingFlags, asset => fileExists(asset.SourcePath), cancellationToken),
            cancellationToken);
    }

    private static ExportPreflightResult ValidateCore(
        ProjectDocument project,
        bool refreshMissingFlags,
        Func<ProjectAsset, bool> sourceIsCurrent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!project.VideoItems.Any(item => item.DurationMilliseconds > 0))
        {
            return new ExportPreflightResult(false, "Add at least one visual item to V1 before exporting.", []);
        }

        var assets = project.Assets.ToDictionary(asset => asset.Id);
        var missingAssets = new List<(Guid Id, ProjectAsset? Asset, string Name)>();
        var referencedVideoAssetIds = project.Settings.VideoTrackVisible
            ? project.VideoItems
                .Where(item => item.DurationMilliseconds > 0)
                .Select(item => item.AssetId)
            : Enumerable.Empty<Guid>();
        var referencedAssetIds = referencedVideoAssetIds
            .Concat(project.AudioItems
                .Where(item => item.DurationMilliseconds > 0)
                .Select(item => item.AssetId));
        var seenAssetIds = new HashSet<Guid>();
        foreach (var assetId in referencedAssetIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seenAssetIds.Add(assetId)) continue;

            assets.TryGetValue(assetId, out var asset);
            var exists = asset is not null && sourceIsCurrent(asset);
            if (asset is not null && refreshMissingFlags)
            {
                asset.IsMissing = !exists;
            }

            if (exists) continue;
            missingAssets.Add((assetId, asset, MissingAssetName(asset, assetId)));
        }

        var sharedNames = missingAssets
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var missingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in missingAssets)
        {
            var name = sharedNames.Contains(item.Name) && item.Asset is not null
                ? $"{item.Name} ({MissingAssetPath(item.Asset, item.Id)})"
                : item.Name;
            if (missingNames.Add(name)) missing.Add(name);
        }

        return missing.Count == 0
            ? new ExportPreflightResult(true, string.Empty, [])
            : new ExportPreflightResult(false, MissingMediaMessage(missing), missing);
    }

    private static string MissingAssetName(ProjectAsset? asset, Guid assetId)
    {
        var name = asset?.FileName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"Unknown asset {assetId:D}";
        }

        return TruncateEnd(NormalizeDisplayText(name), MaximumMissingAssetNameLength);
    }

    private static string MissingAssetPath(ProjectAsset asset, Guid assetId)
    {
        if (string.IsNullOrWhiteSpace(asset.SourcePath))
        {
            return $"asset {assetId:D}";
        }

        return TruncateMiddle(NormalizeDisplayText(asset.SourcePath), MaximumMissingAssetPathLength);
    }

    private static string MissingMediaMessage(IReadOnlyList<string> missing)
    {
        var visible = string.Join(", ", missing.Take(MaximumReportedMissingAssets));
        if (missing.Count > MaximumReportedMissingAssets)
        {
            visible += $", and {missing.Count - MaximumReportedMissingAssets} more";
        }

        return $"Missing or changed export media: {visible}. Relink the listed files before exporting.";
    }

    private static string NormalizeDisplayText(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string TruncateEnd(string value, int maximumLength)
    {
        if (value.Length <= maximumLength) return value;

        var length = maximumLength;
        if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
        {
            length--;
        }

        return value[..length].TrimEnd();
    }

    private static string TruncateMiddle(string value, int maximumLength)
    {
        if (value.Length <= maximumLength) return value;

        var prefixLength = (maximumLength - 1) / 2;
        if (char.IsHighSurrogate(value[prefixLength - 1]) && char.IsLowSurrogate(value[prefixLength]))
        {
            prefixLength--;
        }

        var suffixLength = maximumLength - prefixLength - 1;
        var suffixStart = value.Length - suffixLength;
        if (char.IsLowSurrogate(value[suffixStart]) && char.IsHighSurrogate(value[suffixStart - 1]))
        {
            suffixStart++;
        }

        return $"{value[..prefixLength]}…{value[suffixStart..]}";
    }
}

public sealed record ExportPreflightResult(
    bool CanExport,
    string ErrorMessage,
    IReadOnlyList<string> MissingAssetNames);

public enum ExportResolutionTier
{
    Hd720p,
    FullHd1080p
}

public enum ExportQuality
{
    Standard,
    High
}

public sealed record ExportOptions(ExportResolutionTier Resolution, ExportQuality Quality);

public static class ExportEncodingProfile
{
    private const uint Standard720pBitrate = 5_000_000;
    private const uint High720pBitrate = 8_000_000;
    private const uint Standard1080pBitrate = 8_000_000;
    private const uint High1080pBitrate = 12_000_000;
    private const uint AudioBitrate = 192_000;
    private const uint AudioSampleRate = 48_000;
    private const uint AudioChannelCount = 2;

    public static MediaEncodingProfile Create(AspectRatioPreset aspect, ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var profile = MediaEncodingProfile.CreateMp4(options.Resolution switch
        {
            ExportResolutionTier.Hd720p => VideoEncodingQuality.HD720p,
            ExportResolutionTier.FullHd1080p => VideoEncodingQuality.HD1080p,
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Resolution, null)
        });
        var dimensions = (aspect, options.Resolution) switch
        {
            (AspectRatioPreset.Landscape16By9, ExportResolutionTier.Hd720p) => (Width: 1280u, Height: 720u),
            (AspectRatioPreset.Portrait9By16, ExportResolutionTier.Hd720p) => (Width: 720u, Height: 1280u),
            (AspectRatioPreset.Square1By1, ExportResolutionTier.Hd720p) => (Width: 720u, Height: 720u),
            (AspectRatioPreset.Landscape16By9, ExportResolutionTier.FullHd1080p) => (Width: 1920u, Height: 1080u),
            (AspectRatioPreset.Portrait9By16, ExportResolutionTier.FullHd1080p) => (Width: 1080u, Height: 1920u),
            (AspectRatioPreset.Square1By1, ExportResolutionTier.FullHd1080p) => (Width: 1080u, Height: 1080u),
            _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, null)
        };
        profile.Video.Width = dimensions.Width;
        profile.Video.Height = dimensions.Height;
        profile.Video.FrameRate.Numerator = 30;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = (options.Resolution, options.Quality) switch
        {
            (ExportResolutionTier.Hd720p, ExportQuality.Standard) => Standard720pBitrate,
            (ExportResolutionTier.Hd720p, ExportQuality.High) => High720pBitrate,
            (ExportResolutionTier.FullHd1080p, ExportQuality.Standard) => Standard1080pBitrate,
            (ExportResolutionTier.FullHd1080p, ExportQuality.High) => High1080pBitrate,
            _ => throw new ArgumentOutOfRangeException(nameof(options), options, null)
        };
        profile.Audio.Bitrate = AudioBitrate;
        profile.Audio.SampleRate = AudioSampleRate;
        profile.Audio.ChannelCount = AudioChannelCount;
        return profile;
    }
}

public static class ExportFailureMapper
{
    public static string? GetMessage(TranscodeFailureReason reason) => reason switch
    {
        TranscodeFailureReason.None => null,
        TranscodeFailureReason.Unknown => "The encoder reported an unknown transcoding error.",
        TranscodeFailureReason.InvalidProfile => "The selected encoding profile is invalid.",
        TranscodeFailureReason.CodecNotFound => "The required H.264/AAC codec is unavailable.",
        _ => $"The encoder reported an unsupported failure reason ({reason})."
    };
}
