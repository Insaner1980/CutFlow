namespace CutFlow.Utilities;

internal static class PreviewTransportMath
{
    public const double FrameRate = 30d;

    private const double FrameDurationMilliseconds = 1_000d / FrameRate;

    public static long StepByFrame(long positionMilliseconds, long durationMilliseconds, bool forward)
    {
        var duration = Math.Max(0, durationMilliseconds);
        var position = Math.Clamp(positionMilliseconds, 0, duration);
        var currentFrame = Math.Round(position / FrameDurationMilliseconds, MidpointRounding.AwayFromZero);
        var targetFrame = currentFrame + (forward ? 1 : -1);
        var target = (long)Math.Round(targetFrame * FrameDurationMilliseconds, MidpointRounding.AwayFromZero);
        return Math.Clamp(target, 0, duration);
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
