using CutFlow.Models;

namespace CutFlow.Utilities;

internal sealed class LiveTextRenderGate
{
    private LiveTextRenderKey? _lastRenderedKey;

    public bool ShouldBuildKey(
        ProjectDocument project,
        long revision,
        EditorSelection selection,
        long positionMilliseconds,
        double surfaceWidth,
        double surfaceHeight,
        bool hasPointerCapture) =>
        !hasPointerCapture &&
        (_lastRenderedKey is null || !_lastRenderedKey.Covers(
            project,
            revision,
            selection,
            positionMilliseconds,
            surfaceWidth,
            surfaceHeight));

    public bool ShouldRender(LiveTextRenderKey key, bool hasPointerCapture)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (hasPointerCapture)
        {
            return false;
        }

        if (key.Equals(_lastRenderedKey))
        {
            _lastRenderedKey = key;
            return false;
        }

        _lastRenderedKey = key;
        return true;
    }

    public void MarkVisualDirty() => _lastRenderedKey = null;
}

internal sealed class LiveTextRenderKey : IEquatable<LiveTextRenderKey>
{
    private readonly double _surfaceWidth;
    private readonly double _surfaceHeight;
    private readonly bool _isTrackVisible;
    private readonly LiveTextItemRenderKey[] _items;
    private readonly ProjectDocument _project;
    private readonly long _revision;
    private readonly EditorSelection _selection;
    private readonly long _intervalStart;
    private readonly long _intervalEnd;

    private LiveTextRenderKey(
        ProjectDocument project,
        long revision,
        EditorSelection selection,
        double surfaceWidth,
        double surfaceHeight,
        bool isTrackVisible,
        LiveTextItemRenderKey[] items,
        long intervalStart,
        long intervalEnd)
    {
        _project = project;
        _revision = revision;
        _selection = selection;
        _surfaceWidth = surfaceWidth;
        _surfaceHeight = surfaceHeight;
        _isTrackVisible = isTrackVisible;
        _items = items;
        _intervalStart = intervalStart;
        _intervalEnd = intervalEnd;
    }

    public static LiveTextRenderKey Create(
        ProjectDocument project,
        EditorSelection selection,
        long positionMilliseconds,
        double surfaceWidth,
        double surfaceHeight,
        long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.Settings.TextTrackVisible)
        {
            return new LiveTextRenderKey(
                project,
                revision,
                selection,
                surfaceWidth,
                surfaceHeight,
                isTrackVisible: false,
                [],
                0,
                long.MaxValue);
        }

        var intervalStart = 0L;
        var intervalEnd = long.MaxValue;
        var items = new List<LiveTextItemRenderKey>();
        foreach (var item in project.TextItems)
        {
            var start = Math.Max(0, item.StartMilliseconds);
            var end = TimelineMath.End(start, item.DurationMilliseconds);
            UpdateIntervalBoundary(start, positionMilliseconds, ref intervalStart, ref intervalEnd);
            UpdateIntervalBoundary(end, positionMilliseconds, ref intervalStart, ref intervalEnd);
            if (!TimelineMath.IsActiveAt(positionMilliseconds, start, item.DurationMilliseconds))
            {
                continue;
            }

            items.Add(new LiveTextItemRenderKey(
                item.Id,
                item.Text,
                TextStyle.NormalizeFontFamily(item.FontFamily),
                item.FontSize,
                item.FontWeight,
                item.IsItalic,
                item.TextColor,
                item.BackgroundColor,
                item.BackgroundEnabled,
                TextStyle.ClampOpacity(item.Opacity),
                item.Alignment,
                item.NormalizedX,
                item.NormalizedY,
                selection is { Kind: EditorSelectionKind.TextItem, ItemId: Guid id } && id == item.Id));
        }

        return new LiveTextRenderKey(
            project,
            revision,
            selection,
            surfaceWidth,
            surfaceHeight,
            isTrackVisible: true,
            items.ToArray(),
            intervalStart,
            intervalEnd);
    }

    public bool Covers(
        ProjectDocument project,
        long revision,
        EditorSelection selection,
        long positionMilliseconds,
        double surfaceWidth,
        double surfaceHeight) =>
        ReferenceEquals(_project, project) &&
        _revision == revision &&
        _selection == selection &&
        BitConverter.DoubleToInt64Bits(_surfaceWidth) == BitConverter.DoubleToInt64Bits(surfaceWidth) &&
        BitConverter.DoubleToInt64Bits(_surfaceHeight) == BitConverter.DoubleToInt64Bits(surfaceHeight) &&
        positionMilliseconds >= _intervalStart &&
        positionMilliseconds < _intervalEnd;

    private static void UpdateIntervalBoundary(
        long boundary,
        long positionMilliseconds,
        ref long intervalStart,
        ref long intervalEnd)
    {
        if (boundary <= positionMilliseconds)
        {
            intervalStart = Math.Max(intervalStart, boundary);
        }
        else
        {
            intervalEnd = Math.Min(intervalEnd, boundary);
        }
    }

    public bool Equals(LiveTextRenderKey? other) =>
        other is not null &&
        BitConverter.DoubleToInt64Bits(_surfaceWidth) == BitConverter.DoubleToInt64Bits(other._surfaceWidth) &&
        BitConverter.DoubleToInt64Bits(_surfaceHeight) == BitConverter.DoubleToInt64Bits(other._surfaceHeight) &&
        _isTrackVisible == other._isTrackVisible &&
        _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as LiveTextRenderKey);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_surfaceWidth);
        hash.Add(_surfaceHeight);
        hash.Add(_isTrackVisible);
        foreach (var item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

internal readonly record struct LiveTextItemRenderKey(
    Guid Id,
    string Text,
    string FontFamily,
    double FontSize,
    int FontWeight,
    bool IsItalic,
    string TextColor,
    string BackgroundColor,
    bool BackgroundEnabled,
    double Opacity,
    TextHorizontalAlignment Alignment,
    double NormalizedX,
    double NormalizedY,
    bool IsSelected);
