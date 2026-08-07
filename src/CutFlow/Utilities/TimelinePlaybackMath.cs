namespace CutFlow.Utilities;

internal static class TimelinePlaybackMath
{
    public static long ClampPosition(long positionMilliseconds, long durationMilliseconds) =>
        Math.Clamp(positionMilliseconds, 0, Math.Max(0, durationMilliseconds));

    public static long StepPosition(long positionMilliseconds, long deltaMilliseconds, long durationMilliseconds) =>
        ClampPosition(TimelineMath.SaturatingAdd(positionMilliseconds, deltaMilliseconds), durationMilliseconds);

    public static double EnsureVisibleOffset(
        double playheadPixel,
        double currentOffset,
        double viewportWidth,
        double contentWidth)
    {
        var maximumOffset = Math.Max(0, contentWidth - Math.Max(0, viewportWidth));
        var offset = Math.Clamp(currentOffset, 0, maximumOffset);
        if (playheadPixel < offset)
        {
            return Math.Clamp(playheadPixel, 0, maximumOffset);
        }

        if (playheadPixel > offset + viewportWidth)
        {
            return Math.Clamp(playheadPixel - viewportWidth, 0, maximumOffset);
        }

        return offset;
    }
}
