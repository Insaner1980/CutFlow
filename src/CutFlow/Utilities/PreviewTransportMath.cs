namespace CutFlow.Utilities;

internal static class PreviewTransportMath
{
    private const long FramesPerSecond = 30;
    private const long MillisecondsPerSecond = 1_000;

    public const double FrameRate = FramesPerSecond;

    public static long StepByFrame(long positionMilliseconds, long durationMilliseconds, bool forward)
    {
        var duration = Math.Max(0, durationMilliseconds);
        var position = Math.Clamp(positionMilliseconds, 0, duration);
        var currentFrame = (long)((decimal)position * FramesPerSecond / MillisecondsPerSecond);
        var targetFrame = currentFrame + (forward ? 1 : -1);
        var target = decimal.Ceiling((decimal)targetFrame * MillisecondsPerSecond / FramesPerSecond);
        if (target <= 0) return 0;
        if (target >= duration) return duration;
        return (long)target;
    }

    public static PreviewFitSize CalculateFitSize(
        double availableWidth,
        double availableHeight,
        double contentWidth,
        double contentHeight)
    {
        if (!IsPositiveFinite(availableWidth) ||
            !IsPositiveFinite(availableHeight) ||
            !IsPositiveFinite(contentWidth) ||
            !IsPositiveFinite(contentHeight))
        {
            return default;
        }

        var scale = Math.Min(availableWidth / contentWidth, availableHeight / contentHeight);
        return new PreviewFitSize(contentWidth * scale, contentHeight * scale);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}

internal readonly record struct PreviewFitSize(double Width, double Height);
