using System.Text.Json;
using System.Text.Json.Serialization;
using CutFlow.Utilities;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class AppSettings
{
    public const int CurrentWindowBoundsVersion = 1;
    public const double MinimumWindowWidth = 1180;
    public const double MinimumWindowHeight = 720;
    public const double DefaultWindowWidth = 1500;
    public const double DefaultWindowHeight = 900;
    public const double DefaultTimelineHeight = 300;
    public const double MinimumTimelineHeight = 180;
    public const double MaximumTimelineHeight = 600;

    public double WindowX { get; set; }

    public double WindowY { get; set; }

    public double WindowWidth { get; set; }

    public double WindowHeight { get; set; }

    public int WindowBoundsVersion { get; set; }

    public int WindowPixelX { get; set; }

    public int WindowPixelY { get; set; }

    public int WindowPixelWidth { get; set; }

    public int WindowPixelHeight { get; set; }

    public double TimelineZoom { get; set; } = 80;

    public double TimelineHeight { get; set; }

    public bool LoopPlayback { get; set; }

    public string LastExportFolder { get; set; } = string.Empty;

    public static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        var hasPhysicalBounds = settings.WindowBoundsVersion == CurrentWindowBoundsVersion &&
                                settings.WindowPixelWidth > 0 &&
                                settings.WindowPixelHeight > 0;
        return new AppSettings
        {
            WindowX = NormalizePosition(settings.WindowX),
            WindowY = NormalizePosition(settings.WindowY),
            WindowWidth = NormalizeWindowDimension(settings.WindowWidth, DefaultWindowWidth, MinimumWindowWidth),
            WindowHeight = NormalizeWindowDimension(settings.WindowHeight, DefaultWindowHeight, MinimumWindowHeight),
            WindowBoundsVersion = hasPhysicalBounds ? CurrentWindowBoundsVersion : 0,
            WindowPixelX = hasPhysicalBounds ? settings.WindowPixelX : 0,
            WindowPixelY = hasPhysicalBounds ? settings.WindowPixelY : 0,
            WindowPixelWidth = hasPhysicalBounds ? settings.WindowPixelWidth : 0,
            WindowPixelHeight = hasPhysicalBounds ? settings.WindowPixelHeight : 0,
            TimelineZoom = NormalizeRange(settings.TimelineZoom, TimelineScale.MinimumPixelsPerSecond, TimelineScale.MaximumPixelsPerSecond, 80),
            TimelineHeight = NormalizeTimelineHeight(settings.TimelineHeight),
            LoopPlayback = settings.LoopPlayback,
            LastExportFolder = NormalizeFolderPath(settings.LastExportFolder)
        };
    }

    private static double NormalizePosition(double value) => double.IsFinite(value) ? value : 0;

    private static double NormalizeWindowDimension(double value, double fallback, double minimum) =>
        !double.IsFinite(value) || value <= 0 ? fallback : Math.Max(minimum, value);

    private static double NormalizeRange(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static double NormalizeTimelineHeight(double value) =>
        !double.IsFinite(value) || value <= 0
            ? DefaultTimelineHeight
            : Math.Clamp(value, MinimumTimelineHeight, MaximumTimelineHeight);

    private static string NormalizeFolderPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(value.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService(string? rootPath = null)
    {
        var localRootPath = rootPath ?? ApplicationData.Current.LocalFolder.Path;
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            throw new ArgumentException("A local settings root path is required.", nameof(rootPath));
        }

        _settingsPath = Path.Combine(Path.GetFullPath(localRootPath), "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Normalize(null);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(_settingsPath, cancellationToken), JsonOptions);
            return AppSettings.Normalize(settings);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The settings JSON could not be read.", exception);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var normalized = AppSettings.Normalize(settings);
        await File.WriteAllTextAsync(_settingsPath, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken);
    }
}
