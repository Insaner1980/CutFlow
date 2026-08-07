using CutFlow.Models;
using Windows.System;

namespace CutFlow.Utilities;

public enum TimelineTrackKind
{
    None,
    Video,
    Text,
    Audio
}

internal enum TimelineDropFailure
{
    None,
    NoTrack,
    LockedTrack,
    IncompatibleAsset
}

internal readonly record struct TimelineDropDecision(bool IsAllowed, TimelineDropFailure Failure);

internal static class TimelineDropPolicy
{
    public static TimelineTrackKind HitTest(
        double pointerY,
        double rulerHeight,
        double videoHeight,
        double textHeight,
        double audioHeight)
    {
        if (!double.IsFinite(pointerY) || pointerY < 0)
        {
            return TimelineTrackKind.None;
        }

        var videoStart = NonNegative(rulerHeight);
        var textStart = videoStart + NonNegative(videoHeight);
        var audioStart = textStart + NonNegative(textHeight);
        var contentEnd = audioStart + NonNegative(audioHeight);

        return pointerY < videoStart
            ? TimelineTrackKind.None
            : pointerY < textStart
                ? TimelineTrackKind.Video
                : pointerY < audioStart
                    ? TimelineTrackKind.Text
                    : pointerY < contentEnd
                        ? TimelineTrackKind.Audio
                        : TimelineTrackKind.None;
    }

    public static TimelineDropDecision Evaluate(
        TimelineTrackKind track,
        ProjectAssetKind assetKind,
        bool isLocked)
    {
        if (track == TimelineTrackKind.None)
        {
            return new(false, TimelineDropFailure.NoTrack);
        }

        if (isLocked)
        {
            return new(false, TimelineDropFailure.LockedTrack);
        }

        var allowed = track switch
        {
            TimelineTrackKind.Video => assetKind is ProjectAssetKind.Video or ProjectAssetKind.Image,
            TimelineTrackKind.Audio => assetKind == ProjectAssetKind.Audio,
            _ => false
        };
        return allowed
            ? new(true, TimelineDropFailure.None)
            : new(false, TimelineDropFailure.IncompatibleAsset);
    }

    public static TimelineTrackKind TrackForSelection(EditorSelectionKind selectionKind) => selectionKind switch
    {
        EditorSelectionKind.VideoItem => TimelineTrackKind.Video,
        EditorSelectionKind.TextItem => TimelineTrackKind.Text,
        EditorSelectionKind.AudioItem => TimelineTrackKind.Audio,
        _ => TimelineTrackKind.None
    };

    public static string DisplayName(TimelineTrackKind track) => track switch
    {
        TimelineTrackKind.Video => "V1",
        TimelineTrackKind.Text => "T1",
        TimelineTrackKind.Audio => "A1",
        _ => "timeline"
    };

    private static double NonNegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
}

internal sealed class TimelineTrackLocks
{
    private readonly HashSet<TimelineTrackKind> _lockedTracks = [];

    public bool IsLocked(TimelineTrackKind track) => track != TimelineTrackKind.None && _lockedTracks.Contains(track);

    public void SetLocked(TimelineTrackKind track, bool isLocked)
    {
        if (track == TimelineTrackKind.None)
        {
            return;
        }

        if (isLocked)
        {
            _lockedTracks.Add(track);
        }
        else
        {
            _lockedTracks.Remove(track);
        }
    }

    public bool CanEdit(EditorSelectionKind selectionKind) =>
        !IsLocked(TimelineDropPolicy.TrackForSelection(selectionKind));
}

internal static class MediaAssetDragPayload
{
    public const string FormatId = "application/x-cutflow-media-asset";

    public static string Create(Guid assetId) => assetId.ToString("D");

    public static bool TryParseAssetId(object? payload, out Guid assetId)
    {
        assetId = default;
        return payload is string text && Guid.TryParseExact(text, "D", out assetId);
    }
}

internal static class MediaAssetActivationPolicy
{
    public static bool ShouldActivate(VirtualKey key, bool canAdd) =>
        canAdd && key is VirtualKey.Enter or VirtualKey.Space;
}
