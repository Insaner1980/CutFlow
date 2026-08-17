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

    public static WindowGeometry SelectBoundsForPersistence(
        WindowGeometry currentBounds,
        WindowGeometry? restoredBounds,
        bool isRestored) =>
        isRestored || restoredBounds is null ? currentBounds : restoredBounds.Value;

    public static int SelectTargetDisplay(WindowGeometry requested, IReadOnlyList<WindowDisplayGeometry> displays) =>
        SelectTargetDisplay(displays, _ => requested);

    public static int SelectLegacyTargetDisplay(AppSettings settings, IReadOnlyList<WindowDisplayGeometry> displays)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SelectTargetDisplay(displays, display => FromLegacyLogicalSettings(settings, display.Dpi));
    }

    private static int SelectTargetDisplay(
        IReadOnlyList<WindowDisplayGeometry> displays,
        Func<WindowDisplayGeometry, WindowGeometry> getRequestedBounds)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (displays.Count == 0)
        {
            throw new ArgumentException("At least one display is required.", nameof(displays));
        }

        var bestIndex = 0;
        var bestCoverage = -1d;
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < displays.Count; index++)
        {
            var display = displays[index];
            var requested = getRequestedBounds(display);
            var coverage = GetIntersectionCoverage(requested, display.PhysicalBounds);
            var distance = GetSquaredDistance(requested, display.PhysicalBounds);
            var coverageComparison = coverage.CompareTo(bestCoverage);
            var distanceComparison = distance.CompareTo(bestDistance);
            if (coverageComparison > 0 ||
                coverageComparison == 0 && distanceComparison < 0 ||
                coverageComparison == 0 && distanceComparison == 0 && IsPreferredTieBreak(display, displays[bestIndex]))
            {
                bestIndex = index;
                bestCoverage = coverage;
                bestDistance = distance;
            }
        }

        return bestIndex;
    }

    private static bool IsPreferredTieBreak(WindowDisplayGeometry candidate, WindowDisplayGeometry current)
    {
        if (candidate.IsPrimary != current.IsPrimary)
        {
            return candidate.IsPrimary;
        }

        var candidateBounds = candidate.PhysicalBounds;
        var currentBounds = current.PhysicalBounds;
        return (candidateBounds.X, candidateBounds.Y, candidateBounds.Width, candidateBounds.Height, candidate.Dpi, candidate.StableId)
            .CompareTo((currentBounds.X, currentBounds.Y, currentBounds.Width, currentBounds.Height, current.Dpi, current.StableId)) < 0;
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

    private static double GetIntersectionCoverage(WindowGeometry first, WindowGeometry second)
    {
        var width = Math.Max(0L, Math.Min((long)first.X + first.Width, (long)second.X + second.Width) - Math.Max(first.X, second.X));
        var height = Math.Max(0L, Math.Min((long)first.Y + first.Height, (long)second.Y + second.Height) - Math.Max(first.Y, second.Y));
        return width * (double)height / (first.Width * (double)first.Height);
    }

    private static double GetSquaredDistance(WindowGeometry first, WindowGeometry second)
    {
        var horizontal = GetAxisDistance(first.X, first.Width, second.X, second.Width);
        var vertical = GetAxisDistance(first.Y, first.Height, second.Y, second.Height);
        return horizontal * horizontal + vertical * vertical;
    }

    private static double GetAxisDistance(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var firstEnd = (long)firstStart + firstLength;
        var secondEnd = (long)secondStart + secondLength;
        if (firstEnd < secondStart)
        {
            return secondStart - firstEnd;
        }

        return secondEnd < firstStart ? firstStart - secondEnd : 0;
    }

    private static uint NormalizeDpi(uint dpi) => dpi == 0 ? StandardDpi : dpi;
}

public readonly record struct WindowMinimumSize(int Width, int Height);

public readonly record struct WindowDisplayGeometry(
    WindowGeometry PhysicalBounds,
    uint Dpi,
    bool IsPrimary = false,
    ulong StableId = 0);
