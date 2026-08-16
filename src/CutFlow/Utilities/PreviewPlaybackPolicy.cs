namespace CutFlow.Utilities;

public static class PreviewPlaybackPolicy
{
    public static bool ShouldRestartAtEnd(bool loopEnabled) => loopEnabled;

    public static bool ShouldRestartForPlay(TimeSpan position, TimeSpan duration)
    {
        var roundedDuration = Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero);
        if (roundedDuration <= 0)
        {
            return false;
        }

        var roundedPosition = Math.Round(
            Math.Max(0, position.TotalMilliseconds),
            MidpointRounding.AwayFromZero);
        return roundedPosition >= roundedDuration;
    }

    public static long GetRebuildStartPosition(
        long positionMilliseconds,
        long durationMilliseconds,
        bool playIntent)
    {
        var duration = Math.Max(0, durationMilliseconds);
        var position = TimelinePlaybackMath.ClampPosition(positionMilliseconds, duration);
        return playIntent && duration > 0 && position == duration ? 0 : position;
    }
}
