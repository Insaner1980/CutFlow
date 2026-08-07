using System.Globalization;

namespace CutFlow.Utilities;

public static class TimecodeFormatter
{
    public static string Format(long milliseconds, double frameRate)
    {
        if (!double.IsFinite(frameRate) || frameRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameRate));
        }

        var clampedMilliseconds = Math.Max(0, milliseconds);
        var wholeSeconds = clampedMilliseconds / 1_000;
        var remainderMilliseconds = clampedMilliseconds % 1_000;
        var hours = wholeSeconds / 3_600;
        var minutes = wholeSeconds % 3_600 / 60;
        var seconds = wholeSeconds % 60;
        var frames = (long)Math.Floor(remainderMilliseconds * frameRate / 1_000d);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}:{2:D2}:{3:D2}",
            hours,
            minutes,
            seconds,
            frames);
    }
}
