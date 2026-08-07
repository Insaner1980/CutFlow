namespace CutFlow.Utilities;

public static class TimelineCardText
{
    public static string ToSingleLine(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
