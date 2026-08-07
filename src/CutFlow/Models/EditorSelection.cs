namespace CutFlow.Models;

public readonly record struct EditorSelection(EditorSelectionKind Kind, Guid? ItemId)
{
    public static EditorSelection None => new(EditorSelectionKind.None, null);
}

public enum EditorSelectionKind
{
    None,
    Project,
    Asset,
    VideoItem,
    AudioItem,
    TextItem
}
