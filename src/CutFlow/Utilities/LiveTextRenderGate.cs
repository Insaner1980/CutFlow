using CutFlow.Models;

namespace CutFlow.Utilities;

internal sealed class LiveTextRenderGate
{
    private LiveTextRenderKey? _lastRenderedKey;

    public bool ShouldRender(LiveTextRenderKey key, bool hasPointerCapture)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (hasPointerCapture || key.Equals(_lastRenderedKey))
        {
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

    private LiveTextRenderKey(double surfaceWidth, double surfaceHeight, bool isTrackVisible, LiveTextItemRenderKey[] items)
    {
        _surfaceWidth = surfaceWidth;
        _surfaceHeight = surfaceHeight;
        _isTrackVisible = isTrackVisible;
        _items = items;
    }

    public static LiveTextRenderKey Create(
        ProjectDocument project,
        EditorSelection selection,
        long positionMilliseconds,
        double surfaceWidth,
        double surfaceHeight)
    {
        ArgumentNullException.ThrowIfNull(project);
        var items = project.TextItems
            .Where(item => TimelineMath.IsActiveAt(positionMilliseconds, item.StartMilliseconds, item.DurationMilliseconds))
            .Select(item => new LiveTextItemRenderKey(
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
                selection is { Kind: EditorSelectionKind.TextItem, ItemId: Guid id } && id == item.Id))
            .ToArray();
        return new LiveTextRenderKey(surfaceWidth, surfaceHeight, project.Settings.TextTrackVisible, items);
    }

    public bool Equals(LiveTextRenderKey? other) =>
        other is not null &&
        _surfaceWidth.Equals(other._surfaceWidth) &&
        _surfaceHeight.Equals(other._surfaceHeight) &&
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
