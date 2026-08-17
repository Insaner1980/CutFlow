using CutFlow.Models;
using CutFlow.Utilities;

namespace CutFlow.Services;

public sealed class CompositionPlan
{
    private const int MaximumAssetNameLength = 120;

    private CompositionPlan(
        IReadOnlyList<CompositionVisualPlan> visuals,
        IReadOnlyList<CompositionAudioPlan> audioTracks,
        IReadOnlyList<CompositionTextOverlayPlan> textOverlays,
        IReadOnlyList<string> errors,
        long targetDurationMilliseconds)
    {
        Visuals = visuals;
        AudioTracks = audioTracks;
        TextOverlays = textOverlays;
        Errors = errors;
        TargetDurationMilliseconds = targetDurationMilliseconds;
    }

    public IReadOnlyList<CompositionVisualPlan> Visuals { get; }
    public IReadOnlyList<CompositionAudioPlan> AudioTracks { get; }
    public IReadOnlyList<CompositionTextOverlayPlan> TextOverlays { get; }
    public IReadOnlyList<string> Errors { get; }
    public long TargetDurationMilliseconds { get; }

    public static CompositionPlan Create(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!TimelineEditingService.IsWithinProjectDurationLimit(project))
        {
            return new CompositionPlan(
                [],
                [],
                [],
                ["Project timeline must be between 100 milliseconds and 24 hours."],
                0);
        }

        var assets = project.Assets.ToDictionary(asset => asset.Id);
        var visuals = new List<CompositionVisualPlan>();
        var audioTracks = new List<CompositionAudioPlan>();
        var textOverlays = new List<CompositionTextOverlayPlan>();
        var errors = new List<string>();
        var visualDuration = AddVisuals(project, assets, visuals, errors);
        AddAudioTracks(project, assets, audioTracks, errors);
        AddTextOverlays(project, textOverlays);

        var targetDuration = Math.Max(0, TimelineEditingService.CalculateProjectDuration(project));
        if (targetDuration > visualDuration)
        {
            visuals.Add(CompositionVisualPlan.Filler(Guid.Empty, targetDuration - visualDuration));
        }

        return new CompositionPlan(
            visuals,
            audioTracks,
            textOverlays,
            errors,
            targetDuration);
    }

    private static long AddVisuals(
        ProjectDocument project,
        Dictionary<Guid, ProjectAsset> assets,
        List<CompositionVisualPlan> visuals,
        List<string> errors)
    {
        long visualDuration = 0;
        foreach (var item in project.VideoItems.Where(item => item.DurationMilliseconds > 0))
        {
            visuals.Add(CreateVisualPlan(item, project.Settings.VideoTrackVisible, assets, errors));
            visualDuration = TimelineMath.SaturatingAdd(visualDuration, item.DurationMilliseconds);
        }

        return visualDuration;
    }

    private static CompositionVisualPlan CreateVisualPlan(
        VideoTimelineItem item,
        bool videoTrackVisible,
        Dictionary<Guid, ProjectAsset> assets,
        List<string> errors)
    {
        if (!videoTrackVisible)
        {
            return CompositionVisualPlan.Filler(item.Id, item.DurationMilliseconds);
        }

        if (!assets.TryGetValue(item.AssetId, out var asset))
        {
            errors.Add($"Unknown visual ({item.Id:D}) is unavailable.");
            return CompositionVisualPlan.Filler(item.Id, item.DurationMilliseconds);
        }

        if (asset.Kind is not (ProjectAssetKind.Video or ProjectAssetKind.Image))
        {
            errors.Add($"'{AssetName(asset)}' has an unsupported visual kind and was replaced with the project background.");
            return CompositionVisualPlan.Filler(item.Id, item.DurationMilliseconds, AssetName(asset));
        }

        if (asset.IsMissing)
        {
            errors.Add($"'{AssetName(asset)}' is missing and was replaced with the project background.");
            return CompositionVisualPlan.Filler(item.Id, item.DurationMilliseconds, AssetName(asset));
        }

        return new CompositionVisualPlan(
            item.Id,
            asset.Kind == ProjectAssetKind.Image ? CompositionVisualKind.Image : CompositionVisualKind.Video,
            asset.SourcePath,
            AssetName(asset),
            item.DurationMilliseconds,
            Math.Max(0, item.SourceInMilliseconds),
            Math.Max(item.SourceInMilliseconds, item.SourceOutMilliseconds),
            item.IsMuted ? 0 : ClampVolume(item.Volume));
    }

    private static void AddAudioTracks(
        ProjectDocument project,
        Dictionary<Guid, ProjectAsset> assets,
        List<CompositionAudioPlan> audioTracks,
        List<string> errors)
    {
        foreach (var item in project.AudioItems.Where(item => item.DurationMilliseconds > 0))
        {
            if (!assets.TryGetValue(item.AssetId, out var asset) || asset.IsMissing)
            {
                errors.Add($"'{(asset is null ? $"Unknown audio ({item.Id:D})" : AssetName(asset))}' is unavailable and was omitted.");
                continue;
            }

            audioTracks.Add(new CompositionAudioPlan(
                item.Id,
                asset.SourcePath,
                AssetName(asset),
                Math.Max(0, item.StartMilliseconds),
                Math.Max(0, item.SourceInMilliseconds),
                Math.Max(item.SourceInMilliseconds, item.SourceOutMilliseconds),
                project.Settings.AudioTrackMuted || item.IsMuted ? 0 : ClampVolume(item.Volume)));
        }
    }

    private static void AddTextOverlays(ProjectDocument project, List<CompositionTextOverlayPlan> textOverlays)
    {
        if (!project.Settings.TextTrackVisible)
        {
            return;
        }

        textOverlays.AddRange(project.TextItems
            .Where(item => item.DurationMilliseconds > 0)
            .Select(item => new CompositionTextOverlayPlan(
                item.Id,
                Math.Max(0, item.StartMilliseconds),
                item.DurationMilliseconds)));
    }

    private static string AssetName(ProjectAsset asset)
    {
        var name = string.IsNullOrWhiteSpace(asset.FileName) ? Path.GetFileName(asset.SourcePath) : asset.FileName;
        name = name.ReplaceLineEndings(" ").Trim();
        if (name.Length <= MaximumAssetNameLength)
        {
            return name;
        }

        var length = char.IsSurrogatePair(name, MaximumAssetNameLength - 1)
            ? MaximumAssetNameLength - 1
            : MaximumAssetNameLength;
        return name[..length].TrimEnd();
    }

    private static double ClampVolume(double volume) => double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 1;

}

public enum CompositionVisualKind
{
    Video,
    Image,
    Filler
}

public sealed record CompositionVisualPlan(
    Guid ItemId,
    CompositionVisualKind Kind,
    string SourcePath,
    string AssetName,
    long DurationMilliseconds,
    long SourceInMilliseconds,
    long SourceOutMilliseconds,
    double Volume)
{
    public static CompositionVisualPlan Filler(Guid itemId, long durationMilliseconds, string assetName = "") =>
        new(itemId, CompositionVisualKind.Filler, string.Empty, assetName, durationMilliseconds, 0, 0, 0);
}

public sealed record CompositionAudioPlan(
    Guid ItemId,
    string SourcePath,
    string AssetName,
    long DelayMilliseconds,
    long SourceInMilliseconds,
    long SourceOutMilliseconds,
    double Volume);

public sealed record CompositionTextOverlayPlan(Guid ItemId, long DelayMilliseconds, long DurationMilliseconds);
