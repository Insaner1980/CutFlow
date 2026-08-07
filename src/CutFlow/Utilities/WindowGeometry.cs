using CutFlow.Services;

namespace CutFlow.Utilities;

public readonly record struct WindowGeometry(int X, int Y, int Width, int Height)
{
    private const uint StandardDpi = 96;

    public static WindowMinimumSize GetEffectiveMinimumSize(int physicalWorkAreaWidth, int physicalWorkAreaHeight, uint targetDpi) => new(
        Math.Min(ScaleMinimum(AppSettings.MinimumWindowWidth, targetDpi), Math.Max(1, physicalWorkAreaWidth)),
        Math.Min(ScaleMinimum(AppSettings.MinimumWindowHeight, targetDpi), Math.Max(1, physicalWorkAreaHeight)));

    public static uint ScaleFactorToDpi(int scalePercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scalePercent);
        return (uint)Math.Round(StandardDpi * scalePercent / 100d);
    }

    public static bool TryGetStoredPhysicalBounds(AppSettings settings, out WindowGeometry bounds)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.WindowBoundsVersion == AppSettings.CurrentWindowBoundsVersion &&
            settings.WindowPixelWidth > 0 &&
            settings.WindowPixelHeight > 0)
        {
            bounds = new WindowGeometry(
                settings.WindowPixelX,
                settings.WindowPixelY,
                settings.WindowPixelWidth,
                settings.WindowPixelHeight);
            return true;
        }

        bounds = default;
        return false;
    }

    public static WindowGeometry FromLegacyLogicalSettings(AppSettings settings, uint targetDpi)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = AppSettings.Normalize(settings);
        return new WindowGeometry(
            ScaleLogical(normalized.WindowX, targetDpi),
            ScaleLogical(normalized.WindowY, targetDpi),
            ScaleLogical(normalized.WindowWidth, targetDpi),
            ScaleLogical(normalized.WindowHeight, targetDpi));
    }

    public static WindowGeometry ClampPhysicalToWorkArea(
        WindowGeometry requested,
        WindowGeometry physicalWorkArea,
        uint targetDpi)
    {
        var minimum = GetEffectiveMinimumSize(physicalWorkArea.Width, physicalWorkArea.Height, targetDpi);
        var maximumWidth = Math.Max(1, physicalWorkArea.Width);
        var maximumHeight = Math.Max(1, physicalWorkArea.Height);
        var width = Math.Clamp(requested.Width, minimum.Width, maximumWidth);
        var height = Math.Clamp(requested.Height, minimum.Height, maximumHeight);
        var maximumX = Math.Max(physicalWorkArea.X, physicalWorkArea.X + physicalWorkArea.Width - width);
        var maximumY = Math.Max(physicalWorkArea.Y, physicalWorkArea.Y + physicalWorkArea.Height - height);

        return new WindowGeometry(
            Math.Clamp(requested.X, physicalWorkArea.X, maximumX),
            Math.Clamp(requested.Y, physicalWorkArea.Y, maximumY),
            width,
            height);
    }

    private static int ScaleLogical(double value, uint targetDpi) =>
        (int)Math.Round(value * NormalizeDpi(targetDpi) / StandardDpi);

    private static int ScaleMinimum(double value, uint targetDpi) =>
        (int)Math.Ceiling(value * NormalizeDpi(targetDpi) / StandardDpi);

    private static uint NormalizeDpi(uint dpi) => dpi == 0 ? StandardDpi : dpi;
}

public readonly record struct WindowMinimumSize(int Width, int Height);
