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
    private readonly Func<string, Task> _cleanupAsync;
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
        Func<string, Task>? cleanupAsync = null,
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
            () => project,
            destinationPath,
            options,
            progress,
            cancellationToken);

    internal async Task<ExportResult> ExportToPathAsync(
        ProjectDocument project,
        Func<ProjectDocument> getSourceGuardProject,
        string destinationPath,
        ExportOptions options,
        IProgress<double> progress,
        CancellationToken cancellationToken,
        object? sourceGuardLock = null,
        EditorImportGate? sourcePreparationGate = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(getSourceGuardProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();
        project = CreateProjectSnapshot(project);
        destinationPath = Path.GetFullPath(destinationPath);

        if (IsSourceMediaPath(project, getSourceGuardProject(), destinationPath))
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

        if (IsSourceMediaPath(project, getSourceGuardProject(), destinationPath))
        {
            return SourceMediaConflict(destinationPath);
        }

        var directoryPath = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The export destination folder is unavailable.");
        cancellationToken.ThrowIfCancellationRequested();
        string? stagingPath = null;
        FileStream? stagingReservation = null;
        try
        {
            var stagingFileName = CreateSafeStagingFileName(
                project,
                getSourceGuardProject,
                destinationPath,
                directoryPath);
            var staging = await CreateReservedStagingFileAsync(directoryPath, stagingFileName, cancellationToken);
            stagingPath = Path.GetFullPath(staging.File.Path);
            stagingReservation = staging.Reservation;
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

            var failure = await _renderAsync(build.Composition, staging.File, profile, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (failure != TranscodeFailureReason.None)
            {
                return new ExportResult(
                    ExportResultStatus.Failed,
                    destinationPath,
                    ExportFailureMapper.GetMessage(failure) ?? "Export failed.");
            }

            if (IsSourceMediaPath(project, getSourceGuardProject(), destinationPath))
            {
                return SourceMediaConflict(destinationPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            stagingReservation.Dispose();
            stagingReservation = null;
            Task<bool> CommitPreparedAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return CommitAsync(
                    stagingPath,
                    destinationPath,
                    canCommit: () => !IsSourceMediaPath(project, getSourceGuardProject(), destinationPath),
                    sourceGuardLock: sourceGuardLock,
                    cancellationToken: token);
            }

            // Finish any in-flight import/relink before checking and replacing its potential source.
            var committed = sourcePreparationGate is null
                ? await CommitPreparedAsync(cancellationToken)
                : await sourcePreparationGate.ExecuteAsync(CommitPreparedAsync, cancellationToken);
            if (!committed)
            {
                return SourceMediaConflict(destinationPath);
            }
            stagingPath = null;
            progress.Report(100);
            return new ExportResult(ExportResultStatus.Success, destinationPath, string.Empty);
        }
        finally
        {
            stagingReservation?.Dispose();
            if (stagingPath is not null)
            {
                await CleanupStagingAsync(stagingPath);
            }
        }
    }

    private static string CreateSafeStagingFileName(
        ProjectDocument project,
        Func<ProjectDocument> getSourceGuardProject,
        string destinationPath,
        string directoryPath)
    {
        string stagingFileName;
        do
        {
            stagingFileName = CreateStagingFileName(destinationPath, Guid.NewGuid());
        }
        while (IsSourceMediaPath(
            project,
            getSourceGuardProject(),
            Path.Combine(directoryPath, stagingFileName)));

        return stagingFileName;
    }

    internal static async Task<(StorageFile File, FileStream Reservation)> CreateReservedStagingFileAsync(
        string directoryPath,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(directoryPath, fileName);
        FileStream? reservation = null;
        await using var creation = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.Asynchronous);
        try
        {
            reservation = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 1,
                FileOptions.Asynchronous);
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
            return (file, reservation);
        }
        catch
        {
            reservation?.Dispose();
            await creation.DisposeAsync();
            try
            {
                File.Delete(path);
            }
            catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
            {
                // Preserve the original lookup failure or cancellation.
            }
            throw;
        }
    }

    private async Task CleanupStagingAsync(string stagingPath)
    {
        try
        {
            await _cleanupAsync(stagingPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            // Secondary cleanup must not replace the primary export failure or cancellation.
            if (_logService is not null)
            {
                _ = _logService.TryWriteAsync(
                    $"Export staging cleanup failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}",
                    CancellationToken.None);
            }
        }
    }

    private static Task DeleteStagingAsync(string stagingPath) =>
        Task.Run(() => File.Delete(stagingPath));

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

    internal static Task<bool> CommitAsync(
        string stagingPath,
        string destinationPath,
        Action<string, string>? move = null,
        Func<bool>? canCommit = null,
        object? sourceGuardLock = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            // The editor uses the same gate for import, relink, undo and other project edits.
            lock (sourceGuardLock ?? new object())
            {
                if (canCommit is not null && !canCommit()) return false;
                cancellationToken.ThrowIfCancellationRequested();
                (move ?? MoveStagingFile)(stagingPath, destinationPath);
                return true;
            }
        });

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

    private static bool IsSourceMediaPath(ProjectDocument project, string destinationPath) =>
        project.Assets
            .Select(asset => asset.SourcePath)
            .Any(sourcePath => IsSameFullPath(sourcePath, destinationPath));

    private static bool IsSameFullPath(string? sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return false;

        try
        {
            return string.Equals(Path.GetFullPath(sourcePath), destinationPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Invalid source paths are reported by preflight as missing media.
            return false;
        }
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

        var missingAssets = FindMissingAssets(project, refreshMissingFlags, sourceIsCurrent, cancellationToken);
        var missing = FormatMissingAssets(missingAssets);
        return missing.Count == 0
            ? new ExportPreflightResult(true, string.Empty, [])
            : new ExportPreflightResult(false, MissingMediaMessage(missing), missing);
    }

    private static IEnumerable<Guid> ReferencedAssetIds(ProjectDocument project)
    {
        var videoAssetIds = project.Settings.VideoTrackVisible
            ? project.VideoItems
                .Where(item => item.DurationMilliseconds > 0)
                .Select(item => item.AssetId)
            : Enumerable.Empty<Guid>();
        return videoAssetIds.Concat(project.AudioItems
            .Where(item => item.DurationMilliseconds > 0)
            .Select(item => item.AssetId));
    }

    private static List<(Guid Id, ProjectAsset? Asset, string Name)> FindMissingAssets(
        ProjectDocument project,
        bool refreshMissingFlags,
        Func<ProjectAsset, bool> sourceIsCurrent,
        CancellationToken cancellationToken)
    {
        var assets = project.Assets.ToDictionary(asset => asset.Id);
        var missingAssets = new List<(Guid Id, ProjectAsset? Asset, string Name)>();
        var seenAssetIds = new HashSet<Guid>();
        foreach (var assetId in ReferencedAssetIds(project))
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

        return missingAssets;
    }

    private static List<string> FormatMissingAssets(List<(Guid Id, ProjectAsset? Asset, string Name)> missingAssets)
    {
        return missingAssets
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count() == 1
                ? group.First().Name
                : $"{group.First().Name} ({group.Count()} missing items)")
            .ToList();
    }

    private static string MissingAssetName(ProjectAsset? asset, Guid assetId)
    {
        var name = asset?.FileName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown project media";
        }

        return TruncateEnd(NormalizeDisplayText(name), MaximumMissingAssetNameLength);
    }

    private static string MissingMediaMessage(List<string> missing)
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
        _ => "The encoder reported an unrecognized transcoding error."
    };

    internal static string? GetExceptionMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => $"{AppInfo.ProductName} could not write to that location. Choose another folder and try again.",
        IOException => "The output file could not be created. Check the destination and available disk space, then try again.",
        ArgumentException or NotSupportedException => "The output path is invalid. Choose another filename or folder.",
        System.Runtime.InteropServices.COMException => "Windows could not encode the project. Check the source files and try another output location.",
        _ => null
    };
}
