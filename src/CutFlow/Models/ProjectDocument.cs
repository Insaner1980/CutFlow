using System.Text.Json.Serialization;

namespace CutFlow.Models;

public sealed class ProjectDocument
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumNameLength = 120;
    public const long MinimumItemDurationMilliseconds = 100;
    public const long MaximumTimelineDurationMilliseconds = 24 * 60 * 60 * 1_000;

    [JsonPropertyName("schemaVersion")]
    [JsonRequired]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    [JsonConverter(typeof(UtcDateTimeOffsetConverter))]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("modifiedAt")]
    [JsonConverter(typeof(UtcDateTimeOffsetConverter))]
    public DateTimeOffset ModifiedAt { get; set; }

    [JsonPropertyName("settings")]
    public ProjectSettings Settings { get; set; } = new();

    [JsonPropertyName("assets")]
    public List<ProjectAsset> Assets { get; set; } = [];

    [JsonPropertyName("videoItems")]
    public List<VideoTimelineItem> VideoItems { get; set; } = [];

    [JsonPropertyName("audioItems")]
    public List<AudioTimelineItem> AudioItems { get; set; } = [];

    [JsonPropertyName("textItems")]
    public List<TextTimelineItem> TextItems { get; set; } = [];

    public static ProjectDocument CreateNew(string name, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(name);
        var createdAtUtc = createdAt.ToUniversalTime();

        return new ProjectDocument
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = createdAtUtc,
            ModifiedAt = createdAtUtc
        };
    }
}

public sealed class ProjectSettings
{
    public const string DefaultBackgroundColor = "#FF000000";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;

    [JsonPropertyName("frameRate")]
    public double FrameRate { get; set; } = 30;

    [JsonPropertyName("aspectRatio")]
    public AspectRatioPreset AspectRatio { get; set; } = AspectRatioPreset.Landscape16By9;

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = DefaultBackgroundColor;

    [JsonPropertyName("videoTrackVisible")]
    public bool VideoTrackVisible { get; set; } = true;

    [JsonPropertyName("textTrackVisible")]
    public bool TextTrackVisible { get; set; } = true;

    [JsonPropertyName("audioTrackMuted")]
    public bool AudioTrackMuted { get; set; }

    public void ApplyAspectRatio(AspectRatioPreset preset)
    {
        AspectRatio = preset;

        (Width, Height) = preset switch
        {
            AspectRatioPreset.Landscape16By9 => (1920, 1080),
            AspectRatioPreset.Portrait9By16 => (1080, 1920),
            AspectRatioPreset.Square1By1 => (1080, 1080),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }
}

[JsonConverter(typeof(StableStringEnumConverter<AspectRatioPreset>))]
public enum AspectRatioPreset
{
    Landscape16By9,
    Portrait9By16,
    Square1By1
}

[JsonConverter(typeof(StableStringEnumConverter<ProjectAssetKind>))]
public enum ProjectAssetKind
{
    Video,
    Image,
    Audio
}

public sealed class ProjectAsset
{
    [JsonPropertyName("id")]
    [JsonRequired]
    public Guid Id { get; set; }

    [JsonPropertyName("kind")]
    [JsonRequired]
    public ProjectAssetKind Kind { get; set; }

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("durationMilliseconds")]
    public long DurationMilliseconds { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("fileSize")]
    public ulong FileSize { get; set; }

    [JsonPropertyName("lastWriteUtc")]
    [JsonConverter(typeof(UtcDateTimeOffsetConverter))]
    public DateTimeOffset LastWriteUtc { get; set; }

    [JsonPropertyName("thumbnailCachePath")]
    public string ThumbnailCachePath { get; set; } = string.Empty;

    [JsonPropertyName("isMissing")]
    public bool IsMissing { get; set; }
}

public sealed class VideoTimelineItem
{
    private long _durationMilliseconds;

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("assetId")]
    public Guid AssetId { get; set; }

    [JsonPropertyName("sourceInMilliseconds")]
    public long SourceInMilliseconds { get; set; }

    [JsonPropertyName("sourceOutMilliseconds")]
    public long SourceOutMilliseconds { get; set; }

    [JsonPropertyName("durationMilliseconds")]
    public long DurationMilliseconds
    {
        get => _durationMilliseconds > 0
            ? _durationMilliseconds
            : CalculateFallbackDuration(SourceInMilliseconds, SourceOutMilliseconds);
        set => _durationMilliseconds = Math.Max(0, value);
    }

    private static long CalculateFallbackDuration(long sourceInMilliseconds, long sourceOutMilliseconds)
    {
        if (sourceOutMilliseconds <= sourceInMilliseconds)
        {
            return 0;
        }

        return sourceInMilliseconds < 0 && sourceOutMilliseconds > long.MaxValue + sourceInMilliseconds
            ? long.MaxValue
            : sourceOutMilliseconds - sourceInMilliseconds;
    }

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 1;

    [JsonPropertyName("isMuted")]
    public bool IsMuted { get; set; }
}

public sealed class AudioTimelineItem
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("assetId")]
    public Guid AssetId { get; set; }

    [JsonPropertyName("startMilliseconds")]
    public long StartMilliseconds { get; set; }

    [JsonPropertyName("sourceInMilliseconds")]
    public long SourceInMilliseconds { get; set; }

    [JsonPropertyName("sourceOutMilliseconds")]
    public long SourceOutMilliseconds { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 1;

    [JsonPropertyName("fadeInMilliseconds")]
    public long FadeInMilliseconds { get; set; }

    [JsonPropertyName("fadeOutMilliseconds")]
    public long FadeOutMilliseconds { get; set; }

    [JsonPropertyName("isMuted")]
    public bool IsMuted { get; set; }

    [JsonIgnore]
    public long DurationMilliseconds
    {
        get
        {
            if (SourceOutMilliseconds <= SourceInMilliseconds)
            {
                return 0;
            }

            return SourceInMilliseconds < 0 && SourceOutMilliseconds > long.MaxValue + SourceInMilliseconds
                ? long.MaxValue
                : SourceOutMilliseconds - SourceInMilliseconds;
        }
    }
}

public sealed class TextTimelineItem
{
    public const string DefaultFontFamily = "Segoe UI";
    public const double DefaultFontSize = 64;
    public const int DefaultFontWeight = 600;
    public const int BoldFontWeight = 700;
    public const string DefaultTextColor = "#FFFFFFFF";
    public const string DefaultBackgroundColor = "#00000000";
    public const double DefaultOpacity = 1;

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("startMilliseconds")]
    public long StartMilliseconds { get; set; }

    [JsonPropertyName("durationMilliseconds")]
    public long DurationMilliseconds { get; set; } = 3_000;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; } = DefaultFontFamily;

    [JsonPropertyName("fontSize")]
    public double FontSize { get; set; } = DefaultFontSize;

    [JsonPropertyName("fontWeight")]
    public int FontWeight { get; set; } = DefaultFontWeight;

    [JsonPropertyName("isItalic")]
    public bool IsItalic { get; set; }

    [JsonPropertyName("textColor")]
    public string TextColor { get; set; } = DefaultTextColor;

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = DefaultBackgroundColor;

    [JsonPropertyName("backgroundEnabled")]
    public bool BackgroundEnabled { get; set; }

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = DefaultOpacity;

    [JsonPropertyName("alignment")]
    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Center;

    [JsonPropertyName("normalizedX")]
    public double NormalizedX { get; set; } = 0.5;

    [JsonPropertyName("normalizedY")]
    public double NormalizedY { get; set; } = 0.5;
}

[JsonConverter(typeof(StableStringEnumConverter<TextHorizontalAlignment>))]
public enum TextHorizontalAlignment
{
    Left,
    Center,
    Right
}

[JsonConverter(typeof(StableStringEnumConverter<TextPreset>))]
public enum TextPreset
{
    Default,
    Title,
    Subtitle,
    MinimalLabel
}
