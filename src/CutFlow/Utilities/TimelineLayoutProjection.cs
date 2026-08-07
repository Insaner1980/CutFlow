using CutFlow.Models;

namespace CutFlow.Utilities;

public static class TimelineLayoutProjection
{
    public static IReadOnlyList<TimelineItemBounds> GetVideoBounds(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var bounds = new List<TimelineItemBounds>(project.VideoItems.Count);
        var start = 0L;
        foreach (var item in project.VideoItems)
        {
            var duration = Math.Max(0, item.DurationMilliseconds);
            bounds.Add(new TimelineItemBounds(item.Id, EditorSelectionKind.VideoItem, start, duration));
            start = TimelineMath.End(start, duration);
        }

        return bounds;
    }

    public static IReadOnlyList<TimelineItemBounds> GetAllBounds(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var bounds = new List<TimelineItemBounds>(project.VideoItems.Count + project.TextItems.Count + project.AudioItems.Count);
        bounds.AddRange(GetVideoBounds(project));
        bounds.AddRange(project.TextItems.Select(item => new TimelineItemBounds(
            item.Id,
            EditorSelectionKind.TextItem,
            Math.Max(0, item.StartMilliseconds),
            Math.Max(0, item.DurationMilliseconds))));
        bounds.AddRange(project.AudioItems.Select(item => new TimelineItemBounds(
            item.Id,
            EditorSelectionKind.AudioItem,
            Math.Max(0, item.StartMilliseconds),
            Math.Max(0, item.DurationMilliseconds))));
        return bounds;
    }

}

public readonly record struct TimelineItemBounds(
    Guid ItemId,
    EditorSelectionKind Kind,
    long StartMilliseconds,
    long DurationMilliseconds)
{
    public long EndMilliseconds => TimelineMath.End(StartMilliseconds, DurationMilliseconds);
}
