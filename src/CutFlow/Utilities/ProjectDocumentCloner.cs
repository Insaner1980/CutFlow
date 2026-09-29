using CutFlow.Models;

namespace CutFlow.Utilities;

internal static class ProjectDocumentCloner
{
    public static ProjectDocument Clone(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new ProjectDocument
        {
            SchemaVersion = project.SchemaVersion,
            Id = project.Id,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt,
            Settings = Clone(project.Settings),
            Assets = project.Assets.Select(Clone).ToList(),
            VideoItems = project.VideoItems.Select(Clone).ToList(),
            AudioItems = project.AudioItems.Select(Clone).ToList(),
            TextItems = project.TextItems.Select(Clone).ToList()
        };
    }

    private static ProjectSettings Clone(ProjectSettings settings) => new()
    {
        Width = settings.Width,
        Height = settings.Height,
        FrameRate = settings.FrameRate,
        AspectRatio = settings.AspectRatio,
        BackgroundColor = settings.BackgroundColor,
        VideoTrackVisible = settings.VideoTrackVisible,
        TextTrackVisible = settings.TextTrackVisible,
        AudioTrackMuted = settings.AudioTrackMuted
    };

    private static ProjectAsset Clone(ProjectAsset asset) => new()
    {
        Id = asset.Id,
        Kind = asset.Kind,
        SourcePath = asset.SourcePath,
        FileName = asset.FileName,
        DurationMilliseconds = asset.DurationMilliseconds,
        Width = asset.Width,
        Height = asset.Height,
        FileSize = asset.FileSize,
        LastWriteUtc = asset.LastWriteUtc,
        ThumbnailCachePath = asset.ThumbnailCachePath,
        IsMissing = asset.IsMissing
    };

    private static VideoTimelineItem Clone(VideoTimelineItem item) => new()
    {
        Id = item.Id,
        AssetId = item.AssetId,
        SourceInMilliseconds = item.SourceInMilliseconds,
        SourceOutMilliseconds = item.SourceOutMilliseconds,
        DurationMilliseconds = item.DurationMilliseconds,
        Volume = item.Volume,
        IsMuted = item.IsMuted
    };

    private static AudioTimelineItem Clone(AudioTimelineItem item) => new()
    {
        Id = item.Id,
        AssetId = item.AssetId,
        StartMilliseconds = item.StartMilliseconds,
        SourceInMilliseconds = item.SourceInMilliseconds,
        SourceOutMilliseconds = item.SourceOutMilliseconds,
        Volume = item.Volume,
        FadeInMilliseconds = item.FadeInMilliseconds,
        FadeOutMilliseconds = item.FadeOutMilliseconds,
        IsMuted = item.IsMuted
    };

    private static TextTimelineItem Clone(TextTimelineItem item) => new()
    {
        Id = item.Id,
        StartMilliseconds = item.StartMilliseconds,
        DurationMilliseconds = item.DurationMilliseconds,
        Text = item.Text,
        FontFamily = item.FontFamily,
        FontSize = item.FontSize,
        FontWeight = item.FontWeight,
        IsItalic = item.IsItalic,
        TextColor = item.TextColor,
        BackgroundColor = item.BackgroundColor,
        BackgroundEnabled = item.BackgroundEnabled,
        Opacity = item.Opacity,
        Alignment = item.Alignment,
        NormalizedX = item.NormalizedX,
        NormalizedY = item.NormalizedY
    };
}
