using CutFlow.Models;

namespace CutFlow.Utilities;

internal static class TimelineMath
{
    public static long SaturatingAdd(long left, long right)
    {
        if (right > 0 && left > long.MaxValue - right)
        {
            return long.MaxValue;
        }

        if (right < 0 && left < long.MinValue - right)
        {
            return long.MinValue;
        }

        return left + right;
    }

    public static long End(long startMilliseconds, long durationMilliseconds) =>
        SaturatingAdd(Math.Max(0, startMilliseconds), Math.Max(0, durationMilliseconds));

    public static long ClampItemStart(long startMilliseconds, long durationMilliseconds)
    {
        var duration = Math.Clamp(
            durationMilliseconds,
            ProjectDocument.MinimumItemDurationMilliseconds,
            ProjectDocument.MaximumTimelineDurationMilliseconds);
        return Math.Clamp(
            startMilliseconds,
            0,
            ProjectDocument.MaximumTimelineDurationMilliseconds - duration);
    }

    public static long ClampItemDuration(long durationMilliseconds, long startMilliseconds)
    {
        var start = Math.Clamp(
            startMilliseconds,
            0,
            ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
        return Math.Clamp(
            durationMilliseconds,
            ProjectDocument.MinimumItemDurationMilliseconds,
            ProjectDocument.MaximumTimelineDurationMilliseconds - start);
    }

    public static bool IsWithinProjectBounds(long startMilliseconds, long durationMilliseconds) =>
        startMilliseconds >= 0 &&
        durationMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds &&
        startMilliseconds <= ProjectDocument.MaximumTimelineDurationMilliseconds - durationMilliseconds;

    public static bool IsActiveAt(long positionMilliseconds, long startMilliseconds, long durationMilliseconds)
    {
        var start = Math.Max(0, startMilliseconds);
        return positionMilliseconds >= start && positionMilliseconds < End(start, durationMilliseconds);
    }
}
