using System.Globalization;
using CutFlow.Models;

namespace CutFlow.Utilities;

public static class TimelineInput
{
    public static bool TryParseFiniteDouble(string? text, out double value)
    {
        var parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (!parsed || !double.IsFinite(value))
        {
            value = 0;
            return false;
        }

        return true;
    }

    public static bool TryParseSeconds(string? text, out long milliseconds)
    {
        milliseconds = 0;
        if (!TryParseFiniteDouble(text, out var seconds) || seconds < 0 ||
            seconds > ProjectDocument.MaximumTimelineDurationMilliseconds / 1_000d)
        {
            return false;
        }

        milliseconds = (long)Math.Round(seconds * 1_000d, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool IsOpaqueArgb(string? value)
    {
        if (value is null || value.Length != 9 || value[0] != '#' ||
            value[1] is not ('F' or 'f') || value[2] is not ('F' or 'f'))
        {
            return false;
        }

        for (var index = 3; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
