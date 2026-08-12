using CutFlow.Models;
using CutFlow.Utilities;

namespace CutFlow.Services;

public static class TimelineEditingService
{
    public static long CalculateProjectDuration(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var videoBounds = TimelineLayoutProjection.GetVideoBounds(project);
        var duration = videoBounds.Count == 0 ? 0 : videoBounds[^1].EndMilliseconds;

        foreach (var item in project.AudioItems)
        {
            duration = Math.Max(duration, TimelineMath.End(item.StartMilliseconds, item.DurationMilliseconds));
        }

        foreach (var item in project.TextItems)
        {
            duration = Math.Max(duration, TimelineMath.End(item.StartMilliseconds, item.DurationMilliseconds));
        }

        return Math.Min(duration, ProjectDocument.MaximumTimelineDurationMilliseconds);
    }

    public static bool IsWithinProjectDurationLimit(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        long videoStart = 0;
        foreach (var item in project.VideoItems)
        {
            if (!TimelineMath.IsWithinProjectBounds(videoStart, item.DurationMilliseconds) ||
                item.SourceInMilliseconds < 0 || item.SourceOutMilliseconds < item.SourceInMilliseconds ||
                item.SourceOutMilliseconds > ProjectDocument.MaximumTimelineDurationMilliseconds)
            {
                return false;
            }

            videoStart += item.DurationMilliseconds;
        }

        foreach (var item in project.AudioItems)
        {
            if (!TimelineMath.IsWithinProjectBounds(item.StartMilliseconds, item.DurationMilliseconds) ||
                item.SourceInMilliseconds < 0 || item.SourceOutMilliseconds < item.SourceInMilliseconds ||
                item.SourceOutMilliseconds > ProjectDocument.MaximumTimelineDurationMilliseconds)
            {
                return false;
            }
        }

        return project.TextItems.All(item =>
            TimelineMath.IsWithinProjectBounds(item.StartMilliseconds, item.DurationMilliseconds));
    }

    public static bool ReorderVideoItem(ProjectDocument project, Guid itemId, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(project);

        var sourceIndex = project.VideoItems.FindIndex(item => item.Id == itemId);
        if (sourceIndex < 0)
        {
            return false;
        }

        var clampedIndex = Math.Clamp(targetIndex, 0, project.VideoItems.Count - 1);
        if (sourceIndex == clampedIndex)
        {
            return false;
        }

        var item = project.VideoItems[sourceIndex];
        project.VideoItems.RemoveAt(sourceIndex);
        project.VideoItems.Insert(clampedIndex, item);
        return true;
    }

    public static bool SplitVideoItem(ProjectDocument project, Guid itemId, long timelinePositionMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var itemIndex = project.VideoItems.FindIndex(item => item.Id == itemId);
        if (itemIndex < 0)
        {
            return false;
        }

        var item = project.VideoItems[itemIndex];
        var itemStart = TimelineLayoutProjection.GetVideoBounds(project)[itemIndex].StartMilliseconds;
        var itemEnd = TimelineMath.End(itemStart, item.DurationMilliseconds);
        if (timelinePositionMilliseconds <= itemStart || timelinePositionMilliseconds >= itemEnd)
        {
            return false;
        }

        var splitOffset = timelinePositionMilliseconds - itemStart;
        if (splitOffset < ProjectDocument.MinimumItemDurationMilliseconds ||
            item.DurationMilliseconds - splitOffset < ProjectDocument.MinimumItemDurationMilliseconds)
        {
            return false;
        }

        var asset = project.Assets.FirstOrDefault(candidate => candidate.Id == item.AssetId);
        var isImage = asset?.Kind == ProjectAssetKind.Image;
        var splitSourcePosition = isImage
            ? item.SourceInMilliseconds
            : TimelineMath.SaturatingAdd(item.SourceInMilliseconds, splitOffset);

        var secondItem = new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = item.AssetId,
            SourceInMilliseconds = isImage ? item.SourceInMilliseconds : splitSourcePosition,
            SourceOutMilliseconds = item.SourceOutMilliseconds,
            DurationMilliseconds = item.DurationMilliseconds - splitOffset,
            Volume = item.Volume,
            IsMuted = item.IsMuted
        };
        if (!isImage)
        {
            item.SourceOutMilliseconds = splitSourcePosition;
        }

        item.DurationMilliseconds = splitOffset;
        project.VideoItems.Insert(itemIndex + 1, secondItem);
        return true;
    }

    public static bool TrimVideoStart(ProjectDocument project, Guid itemId, long sourceInMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = FindVideoItem(project, itemId);
        var asset = FindKnownAsset(project, item?.AssetId);
        if (item is null || asset is null)
        {
            return false;
        }

        var maximumSourceIn = Math.Min(item.SourceOutMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds,
            asset.DurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
        if (maximumSourceIn < 0)
        {
            return false;
        }

        var clampedSourceIn = Math.Clamp(sourceInMilliseconds, 0, maximumSourceIn);
        if (item.SourceInMilliseconds == clampedSourceIn)
        {
            return false;
        }

        item.SourceInMilliseconds = clampedSourceIn;
        item.DurationMilliseconds = item.SourceOutMilliseconds - item.SourceInMilliseconds;
        return true;
    }

    public static bool TrimVideoEnd(ProjectDocument project, Guid itemId, long sourceOutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = FindVideoItem(project, itemId);
        var asset = FindKnownAsset(project, item?.AssetId);
        if (item is null || asset is null)
        {
            return false;
        }

        var timelineStart = TimelineLayoutProjection.GetVideoBounds(project)
            .First(bound => bound.ItemId == itemId)
            .StartMilliseconds;
        var minimumSourceOut = TimelineMath.SaturatingAdd(item.SourceInMilliseconds, ProjectDocument.MinimumItemDurationMilliseconds);
        var maximumSourceOut = Math.Min(
            Math.Min(asset.DurationMilliseconds, ProjectDocument.MaximumTimelineDurationMilliseconds),
            item.SourceInMilliseconds + ProjectDocument.MaximumTimelineDurationMilliseconds - timelineStart);
        if (minimumSourceOut > maximumSourceOut)
        {
            return false;
        }

        var clampedSourceOut = Math.Clamp(sourceOutMilliseconds, minimumSourceOut, maximumSourceOut);
        var duration = clampedSourceOut - item.SourceInMilliseconds;
        if (item.SourceOutMilliseconds == clampedSourceOut && item.DurationMilliseconds == duration)
        {
            return false;
        }

        item.SourceOutMilliseconds = clampedSourceOut;
        item.DurationMilliseconds = duration;
        return true;
    }

    public static bool SetImageDuration(ProjectDocument project, Guid itemId, long durationMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = FindVideoItem(project, itemId);
        var asset = project.Assets.FirstOrDefault(candidate => candidate.Id == item?.AssetId && candidate.Kind == ProjectAssetKind.Image);
        if (item is null || asset is null)
        {
            return false;
        }

        var timelineStart = TimelineLayoutProjection.GetVideoBounds(project)
            .First(bound => bound.ItemId == itemId)
            .StartMilliseconds;
        var clampedDuration = TimelineMath.ClampItemDuration(durationMilliseconds, timelineStart);
        if (item.DurationMilliseconds == clampedDuration)
        {
            return false;
        }

        item.DurationMilliseconds = clampedDuration;
        return true;
    }

    public static bool ResetImageDuration(ProjectDocument project, Guid itemId) =>
        SetImageDuration(project, itemId, 5_000);

    public static bool MoveAudioItem(ProjectDocument project, Guid itemId, long startMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.AudioItems.FirstOrDefault(item => item.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var clampedStart = TimelineMath.ClampItemStart(startMilliseconds, item.DurationMilliseconds);
        if (item.StartMilliseconds == clampedStart)
        {
            return false;
        }

        item.StartMilliseconds = clampedStart;
        return true;
    }

    public static bool MoveTextItem(ProjectDocument project, Guid itemId, long startMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.TextItems.FirstOrDefault(item => item.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var clampedStart = TimelineMath.ClampItemStart(startMilliseconds, item.DurationMilliseconds);
        if (item.StartMilliseconds == clampedStart)
        {
            return false;
        }

        item.StartMilliseconds = clampedStart;
        return true;
    }

    public static bool TrimAudioStart(ProjectDocument project, Guid itemId, long sourceInMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.AudioItems.FirstOrDefault(candidate => candidate.Id == itemId);
        var asset = FindKnownAsset(project, item?.AssetId);
        if (item is null || asset is null)
        {
            return false;
        }

        var maximumSourceIn = Math.Min(
            item.SourceOutMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds,
            asset.DurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
        if (maximumSourceIn < 0)
        {
            return false;
        }

        var clampedSourceIn = Math.Clamp(sourceInMilliseconds, 0, maximumSourceIn);
        var desiredStart = item.StartMilliseconds + clampedSourceIn - item.SourceInMilliseconds;
        if (desiredStart < 0)
        {
            clampedSourceIn = Math.Min(maximumSourceIn, clampedSourceIn - desiredStart);
            desiredStart = 0;
        }

        if (item.SourceInMilliseconds == clampedSourceIn && item.StartMilliseconds == desiredStart)
        {
            return false;
        }

        item.SourceInMilliseconds = clampedSourceIn;
        item.StartMilliseconds = desiredStart;
        item.FadeInMilliseconds = Math.Clamp(item.FadeInMilliseconds, 0, item.DurationMilliseconds);
        item.FadeOutMilliseconds = Math.Clamp(item.FadeOutMilliseconds, 0, item.DurationMilliseconds);
        return true;
    }

    public static bool TrimAudioEnd(ProjectDocument project, Guid itemId, long sourceOutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.AudioItems.FirstOrDefault(candidate => candidate.Id == itemId);
        var asset = FindKnownAsset(project, item?.AssetId);
        if (item is null || asset is null)
        {
            return false;
        }

        var minimumSourceOut = TimelineMath.SaturatingAdd(item.SourceInMilliseconds, ProjectDocument.MinimumItemDurationMilliseconds);
        var maximumSourceOut = Math.Min(
            Math.Min(asset.DurationMilliseconds, ProjectDocument.MaximumTimelineDurationMilliseconds),
            item.SourceInMilliseconds + ProjectDocument.MaximumTimelineDurationMilliseconds - item.StartMilliseconds);
        if (minimumSourceOut > maximumSourceOut)
        {
            return false;
        }

        var clampedSourceOut = Math.Clamp(sourceOutMilliseconds, minimumSourceOut, maximumSourceOut);
        if (item.SourceOutMilliseconds == clampedSourceOut)
        {
            return false;
        }

        item.SourceOutMilliseconds = clampedSourceOut;
        item.FadeInMilliseconds = Math.Clamp(item.FadeInMilliseconds, 0, item.DurationMilliseconds);
        item.FadeOutMilliseconds = Math.Clamp(item.FadeOutMilliseconds, 0, item.DurationMilliseconds);
        return true;
    }

    public static bool TrimTextStart(ProjectDocument project, Guid itemId, long startMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var end = TimelineMath.End(item.StartMilliseconds, item.DurationMilliseconds);
        var maximumStart = Math.Max(0, end - ProjectDocument.MinimumItemDurationMilliseconds);
        var clampedStart = Math.Clamp(startMilliseconds, 0, maximumStart);
        var duration = end - clampedStart;
        if (item.StartMilliseconds == clampedStart && item.DurationMilliseconds == duration)
        {
            return false;
        }

        item.StartMilliseconds = clampedStart;
        item.DurationMilliseconds = duration;
        return true;
    }

    public static bool TrimTextEnd(ProjectDocument project, Guid itemId, long endMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(project);

        var item = project.TextItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return false;
        }

        var minimumEnd = TimelineMath.SaturatingAdd(item.StartMilliseconds, ProjectDocument.MinimumItemDurationMilliseconds);
        var clampedEnd = Math.Clamp(
            endMilliseconds,
            minimumEnd,
            ProjectDocument.MaximumTimelineDurationMilliseconds);
        var duration = clampedEnd - item.StartMilliseconds;
        if (item.DurationMilliseconds == duration)
        {
            return false;
        }

        item.DurationMilliseconds = duration;
        return true;
    }

    public static bool SetVideoVolume(ProjectDocument project, Guid itemId, double volume)
    {
        ArgumentNullException.ThrowIfNull(project);
        var item = FindVideoItem(project, itemId);
        return item is not null && SetFiniteVolume(item.Volume, volume, value => item.Volume = value);
    }

    public static bool SetAudioVolume(ProjectDocument project, Guid itemId, double volume)
    {
        ArgumentNullException.ThrowIfNull(project);
        var item = project.AudioItems.FirstOrDefault(candidate => candidate.Id == itemId);
        return item is not null && SetFiniteVolume(item.Volume, volume, value => item.Volume = value);
    }

    public static bool SetVideoMuted(ProjectDocument project, Guid itemId, bool isMuted)
    {
        ArgumentNullException.ThrowIfNull(project);
        var item = FindVideoItem(project, itemId);
        if (item is null || item.IsMuted == isMuted)
        {
            return false;
        }

        item.IsMuted = isMuted;
        return true;
    }

    public static bool SetAudioMuted(ProjectDocument project, Guid itemId, bool isMuted)
    {
        ArgumentNullException.ThrowIfNull(project);
        var item = project.AudioItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item is null || item.IsMuted == isMuted)
        {
            return false;
        }

        item.IsMuted = isMuted;
        return true;
    }

    public static bool DeleteSelection(ProjectDocument project, EditorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(project);

        return selection.ItemId is Guid itemId && selection.Kind switch
        {
            EditorSelectionKind.VideoItem => project.VideoItems.RemoveAll(item => item.Id == itemId) > 0,
            EditorSelectionKind.AudioItem => project.AudioItems.RemoveAll(item => item.Id == itemId) > 0,
            EditorSelectionKind.TextItem => project.TextItems.RemoveAll(item => item.Id == itemId) > 0,
            _ => false
        };
    }

    public static bool DuplicateSelection(
        ProjectDocument project,
        EditorSelection selection,
        out EditorSelection duplicatedSelection)
    {
        ArgumentNullException.ThrowIfNull(project);

        duplicatedSelection = EditorSelection.None;
        if (selection.ItemId is not Guid itemId)
        {
            return false;
        }

        switch (selection.Kind)
        {
            case EditorSelectionKind.VideoItem:
                {
                    var index = project.VideoItems.FindIndex(item => item.Id == itemId);
                    if (index < 0)
                    {
                        return false;
                    }

                    var source = project.VideoItems[index];
                    var copy = new VideoTimelineItem
                    {
                        Id = Guid.NewGuid(),
                        AssetId = source.AssetId,
                        SourceInMilliseconds = source.SourceInMilliseconds,
                        SourceOutMilliseconds = source.SourceOutMilliseconds,
                        DurationMilliseconds = source.DurationMilliseconds,
                        Volume = source.Volume,
                        IsMuted = source.IsMuted
                    };
                    project.VideoItems.Insert(index + 1, copy);
                    duplicatedSelection = new EditorSelection(EditorSelectionKind.VideoItem, copy.Id);
                    return true;
                }
            case EditorSelectionKind.AudioItem:
                {
                    var source = project.AudioItems.FirstOrDefault(item => item.Id == itemId);
                    if (source is null)
                    {
                        return false;
                    }

                    var copy = new AudioTimelineItem
                    {
                        Id = Guid.NewGuid(),
                        AssetId = source.AssetId,
                        StartMilliseconds = TimelineMath.End(source.StartMilliseconds, source.DurationMilliseconds),
                        SourceInMilliseconds = source.SourceInMilliseconds,
                        SourceOutMilliseconds = source.SourceOutMilliseconds,
                        Volume = source.Volume,
                        FadeInMilliseconds = source.FadeInMilliseconds,
                        FadeOutMilliseconds = source.FadeOutMilliseconds,
                        IsMuted = source.IsMuted
                    };
                    project.AudioItems.Add(copy);
                    duplicatedSelection = new EditorSelection(EditorSelectionKind.AudioItem, copy.Id);
                    return true;
                }
            case EditorSelectionKind.TextItem:
                {
                    var source = project.TextItems.FirstOrDefault(item => item.Id == itemId);
                    if (source is null)
                    {
                        return false;
                    }

                    var copy = new TextTimelineItem
                    {
                        Id = Guid.NewGuid(),
                        StartMilliseconds = TimelineMath.End(source.StartMilliseconds, source.DurationMilliseconds),
                        DurationMilliseconds = source.DurationMilliseconds,
                        Text = source.Text,
                        FontFamily = source.FontFamily,
                        FontSize = source.FontSize,
                        FontWeight = source.FontWeight,
                        IsItalic = source.IsItalic,
                        TextColor = source.TextColor,
                        BackgroundColor = source.BackgroundColor,
                        BackgroundEnabled = source.BackgroundEnabled,
                        Opacity = source.Opacity,
                        Alignment = source.Alignment,
                        NormalizedX = source.NormalizedX,
                        NormalizedY = source.NormalizedY
                    };
                    project.TextItems.Add(copy);
                    duplicatedSelection = new EditorSelection(EditorSelectionKind.TextItem, copy.Id);
                    return true;
                }
            default:
                return false;
        }
    }

    private static VideoTimelineItem? FindVideoItem(ProjectDocument project, Guid itemId) =>
        project.VideoItems.FirstOrDefault(item => item.Id == itemId);

    private static ProjectAsset? FindKnownAsset(ProjectDocument project, Guid? assetId) =>
        assetId is Guid id
            ? project.Assets.FirstOrDefault(asset => asset.Id == id && asset.DurationMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds)
            : null;

    private static bool SetFiniteVolume(double current, double requested, Action<double> assign)
    {
        if (!double.IsFinite(requested))
        {
            return false;
        }

        var clamped = Math.Clamp(requested, 0, 1);
        if (Math.Abs(current - clamped) < 0.000_001)
        {
            return false;
        }

        assign(clamped);
        return true;
    }

}
