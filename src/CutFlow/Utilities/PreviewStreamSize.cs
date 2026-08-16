namespace CutFlow.Utilities;

internal static class PreviewStreamSize
{
    private const int EncoderAlignment = 2;

    public static PreviewStreamDimensions Fit(int sourceWidth, int sourceHeight, int maximumWidth, int maximumHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHeight);

        var availableWidth = Math.Min(sourceWidth, maximumWidth);
        var availableHeight = Math.Min(sourceHeight, maximumHeight);
        var divisor = GreatestCommonDivisor(sourceWidth, sourceHeight);
        var ratioWidth = sourceWidth / divisor;
        var ratioHeight = sourceHeight / divisor;
        var exactScale = AlignDown(Math.Min(availableWidth / ratioWidth, availableHeight / ratioHeight));
        if (exactScale > 0 && IsProjectAspectRatio(ratioWidth, ratioHeight))
        {
            return new PreviewStreamDimensions(ratioWidth * exactScale, ratioHeight * exactScale);
        }

        int width;
        int height;
        if ((long)sourceWidth * availableHeight >= (long)sourceHeight * availableWidth)
        {
            width = availableWidth;
            height = checked((int)((long)sourceHeight * availableWidth / sourceWidth));
        }
        else
        {
            width = checked((int)((long)sourceWidth * availableHeight / sourceHeight));
            height = availableHeight;
        }

        width = AlignDown(width);
        height = AlignDown(height);
        if (width == 0 || height == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWidth),
                "The source and maximum dimensions must allow an encoder-aligned preview without upscaling.");
        }

        return new PreviewStreamDimensions(width, height);
    }

    private static int AlignDown(int value) => value - value % EncoderAlignment;

    private static bool IsProjectAspectRatio(int width, int height) =>
        (width, height) is (16, 9) or (9, 16) or (1, 1);

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}

internal readonly record struct PreviewStreamDimensions(int Width, int Height);
