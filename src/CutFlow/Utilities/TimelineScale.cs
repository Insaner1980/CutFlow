namespace CutFlow.Utilities;

public sealed class TimelineScale
{
    private const double WheelDeltaPerStep = 120d;
    private const double WheelZoomFactor = 1.12d;
    public const double MinimumPixelsPerSecond = 20;
    public const double MaximumPixelsPerSecond = 400;
    private const double MinimumRulerIntervalPixels = 80;
    private static readonly long[] RulerIntervalsMilliseconds = [100, 250, 500, 1_000, 2_000, 5_000, 10_000, 30_000, 60_000];

    public TimelineScale(double pixelsPerSecond)
    {
        if (!double.IsFinite(pixelsPerSecond) || pixelsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerSecond));
        }

        PixelsPerSecond = pixelsPerSecond;
    }

    public double PixelsPerSecond { get; }

    public double TimeToPixels(long milliseconds)
    {
        return Math.Max(0, milliseconds) * PixelsPerSecond / 1_000d;
    }

    public TimelineCardGeometry GetCardGeometry(
        TimelineItemBounds bounds,
        double minimumVisualWidth,
        double margin)
    {
        var hitLeft = TimeToPixels(bounds.StartMilliseconds);
        var hitWidth = TimeToPixels(bounds.DurationMilliseconds);
        return new TimelineCardGeometry(
            hitLeft,
            hitWidth,
            hitLeft + margin / 2,
            Math.Max(minimumVisualWidth, hitWidth - margin));
    }

    public bool IsCardVisible(
        TimelineItemBounds bounds,
        double rangeStart,
        double rangeEnd,
        double minimumVisualWidth,
        double margin)
    {
        if (!double.IsFinite(rangeStart) || !double.IsFinite(rangeEnd) || rangeEnd < rangeStart)
        {
            return false;
        }

        var geometry = GetCardGeometry(bounds, minimumVisualWidth, margin);
        var cardRight = Math.Max(geometry.HitRight, geometry.VisualLeft + geometry.VisualWidth);
        return cardRight >= rangeStart && geometry.HitLeft <= rangeEnd;
    }

    public long PixelsToTime(double pixels)
    {
        if (double.IsNaN(pixels) || pixels <= 0)
        {
            return 0;
        }

        var milliseconds = pixels * 1_000d / PixelsPerSecond;
        return !double.IsFinite(milliseconds) || milliseconds >= long.MaxValue
            ? long.MaxValue
            : (long)Math.Round(milliseconds, MidpointRounding.AwayFromZero);
    }

    public long GetRulerIntervalMilliseconds()
    {
        return RulerIntervalsMilliseconds.FirstOrDefault(
            interval => TimeToPixels(interval) >= MinimumRulerIntervalPixels,
            RulerIntervalsMilliseconds[^1]);
    }

    internal static string FormatRulerLabel(long milliseconds, long intervalMilliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        var label = time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
        return intervalMilliseconds < 1_000 ? $"{label}.{time.Milliseconds:000}" : label;
    }

    public IReadOnlyList<RulerTick> GetVisibleRulerTicks(
        double horizontalOffset,
        double viewportWidth,
        long durationMilliseconds,
        int bufferIntervals = 1)
    {
        if (!double.IsFinite(horizontalOffset) || !double.IsFinite(viewportWidth) || viewportWidth <= 0 ||
            durationMilliseconds < 0 || bufferIntervals < 0)
        {
            return [];
        }

        var interval = GetRulerIntervalMilliseconds();
        var firstVisible = PixelsToTime(Math.Max(0, horizontalOffset));
        var lastVisible = PixelsToTime(Math.Max(0, horizontalOffset) + viewportWidth);
        var firstTick = Math.Max(0, (firstVisible / interval - bufferIntervals) * interval);
        var bufferedEnd = lastVisible > long.MaxValue - interval * bufferIntervals
            ? long.MaxValue
            : lastVisible + interval * bufferIntervals;
        var lastTick = Math.Min(durationMilliseconds, bufferedEnd / interval * interval);
        var ticks = new List<RulerTick>();
        for (var time = firstTick; time <= lastTick; time += interval)
        {
            ticks.Add(new RulerTick(time, TimeToPixels(time)));
            if (time > long.MaxValue - interval)
            {
                break;
            }
        }

        return ticks;
    }

    public static double CalculateAnchoredOffset(
        double oldPixelsPerSecond,
        double newPixelsPerSecond,
        double oldHorizontalOffset,
        double pointerViewportX,
        long durationMilliseconds,
        double viewportWidth,
        double contentEndPadding = 0)
    {
        if (!double.IsFinite(oldPixelsPerSecond) || oldPixelsPerSecond <= 0 ||
            !double.IsFinite(newPixelsPerSecond) || newPixelsPerSecond <= 0)
        {
            return 0;
        }

        var pointer = Math.Max(0, pointerViewportX);
        var anchoredTimeSeconds = (Math.Max(0, oldHorizontalOffset) + pointer) / oldPixelsPerSecond;
        var candidate = anchoredTimeSeconds * newPixelsPerSecond - pointer;
        var contentWidth = Math.Max(1_000, durationMilliseconds) * newPixelsPerSecond / 1_000d +
            Math.Max(0, contentEndPadding);
        return ClampHorizontalOffset(candidate, contentWidth, viewportWidth);
    }

    public static double CalculateWheelZoomTarget(double currentPixelsPerSecond, int wheelDelta)
    {
        if (!double.IsFinite(currentPixelsPerSecond))
        {
            return MinimumPixelsPerSecond;
        }

        var current = Math.Clamp(currentPixelsPerSecond, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
        if (wheelDelta == 0)
        {
            return current;
        }

        var target = current * Math.Pow(WheelZoomFactor, wheelDelta / WheelDeltaPerStep);
        return Math.Clamp(target, MinimumPixelsPerSecond, MaximumPixelsPerSecond);
    }

    public static double ClampHorizontalOffset(double offset, double contentWidth, double viewportWidth)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(contentWidth) || !double.IsFinite(viewportWidth))
        {
            return 0;
        }

        return Math.Clamp(offset, 0, Math.Max(0, contentWidth - Math.Max(0, viewportWidth)));
    }
}

public readonly record struct RulerTick(long Milliseconds, double Pixel);

public readonly record struct TimelineCardGeometry(
    double HitLeft,
    double HitWidth,
    double VisualLeft,
    double VisualWidth)
{
    public double HitRight => HitLeft + HitWidth;
}

public enum TimelineCardHit
{
    Start,
    Body,
    End
}

public static class TimelineCardHitTest
{
    private const double MinimumBodyHitWidth = 1;

    public static TimelineCardHit Resolve(double localX, double hitWidth, double trimHitWidth)
    {
        var width = Math.Max(0, hitWidth);
        var edgeWidth = Math.Min(
            Math.Max(0, trimHitWidth),
            Math.Max(0, (width - MinimumBodyHitWidth) / 2));
        if (localX <= edgeWidth)
        {
            return TimelineCardHit.Start;
        }

        return localX >= width - edgeWidth
            ? TimelineCardHit.End
            : TimelineCardHit.Body;
    }
}

public static class TimelineSnapper
{
    public static long Snap(long milliseconds, bool enabled, IEnumerable<long>? relevantEdges = null)
    {
        var value = Math.Max(0, milliseconds);
        if (!enabled)
        {
            return value;
        }

        const long interval = 100;
        const long tolerance = 60;
        var rounded = (long)Math.Round(value / (double)interval, MidpointRounding.AwayFromZero) * interval;
        var best = Math.Abs(value - rounded) <= tolerance ? rounded : value;
        var bestDistance = Math.Abs(value - best);
        if (relevantEdges is null)
        {
            return best;
        }

        foreach (var edge in relevantEdges)
        {
            var candidate = Math.Max(0, edge);
            var distance = Math.Abs(value - candidate);
            if (distance <= tolerance && distance <= bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
