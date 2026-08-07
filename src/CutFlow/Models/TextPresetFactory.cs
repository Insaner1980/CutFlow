namespace CutFlow.Models;

public static class TextPresetFactory
{
    public static TextTimelineItem Create(TextPreset preset, long startMilliseconds)
    {
        var item = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            StartMilliseconds = Math.Clamp(
                startMilliseconds,
                0,
                ProjectDocument.MaximumTimelineDurationMilliseconds - 3_000),
            DurationMilliseconds = 3_000
        };

        switch (preset)
        {
            case TextPreset.Default:
                item.Text = "Text";
                break;
            case TextPreset.Title:
                item.Text = "Title";
                item.FontSize = 96;
                item.FontWeight = 700;
                item.NormalizedY = 0.22;
                break;
            case TextPreset.Subtitle:
                item.Text = "Subtitle";
                item.FontSize = 52;
                item.NormalizedY = 0.78;
                break;
            case TextPreset.MinimalLabel:
                item.Text = "Label";
                item.FontSize = 38;
                item.BackgroundColor = "#CC000000";
                item.BackgroundEnabled = true;
                item.NormalizedY = 0.88;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }

        return item;
    }
}
