namespace CutFlow.Utilities;

internal static class PreviewStreamSize
{
    public static PreviewStreamDimensions Fit(int sourceWidth, int sourceHeight, int maximumWidth, int maximumHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeight);

        var scale = Math.Min(1d, Math.Min(maximumWidth / (double)sourceWidth, maximumHeight / (double)sourceHeight));
        var width = Math.Clamp((int)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero), 1, maximumWidth);
        var height = Math.Clamp((int)Math.Round(sourceHeight * scale, MidpointRounding.AwayFromZero), 1, maximumHeight);
        return new PreviewStreamDimensions(width, height);
    }
}

internal readonly record struct PreviewStreamDimensions(int Width, int Height);
