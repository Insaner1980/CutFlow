using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CutFlow.Models;
using CutFlow.Utilities;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI;

namespace CutFlow.Services;

public sealed class CompositionService
{
    public Task<CompositionBuildResult> BuildPreviewAsync(
        ProjectDocument project,
        CancellationToken cancellationToken) =>
        BuildAsync(project, null, cancellationToken);

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance method preserves the service API used by composition consumers.")]
    public async Task<CompositionBuildResult> BuildAsync(
        ProjectDocument project,
        TextOverlayRenderer? textOverlayRenderer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return await BuildAsync(
            project,
            textOverlayRenderer,
            project.Settings.Width,
            project.Settings.Height,
            cancellationToken);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance overload preserves the injected composition service seam used by export.")]
    [SuppressMessage("Major Code Smell", "S2325:Methods and properties that don't access instance data should be static", Justification = "The instance overload preserves the injected composition service seam used by export.")]
    internal async Task<CompositionBuildResult> BuildAsync(
        ProjectDocument project,
        TextOverlayRenderer? textOverlayRenderer,
        int outputWidth,
        int outputHeight,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var overlayPosition = CreateOverlayPosition(outputWidth, outputHeight);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = CompositionPlan.Create(project);
        var composition = new MediaComposition();
        var errors = plan.Errors.ToList();
        var backgroundColor = ResolveBackgroundColor(project.Settings.BackgroundColor);
        await AddVisualsAsync(composition, plan.Visuals, backgroundColor, errors, cancellationToken);
        await AddAudioTracksAsync(composition, plan.AudioTracks, errors, cancellationToken);
        await AddTextOverlaysAsync(
            composition,
            project,
            plan.TextOverlays,
            textOverlayRenderer,
            overlayPosition,
            errors,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new CompositionBuildResult(composition, errors, project.VideoItems.Count > 0);
    }

    private static async Task AddVisualsAsync(
        MediaComposition composition,
        IEnumerable<CompositionVisualPlan> visuals,
        Color backgroundColor,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var visual in visuals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            composition.Clips.Add(await CreateVisualClipAsync(visual, backgroundColor, errors, cancellationToken));
        }
    }

    private static async Task<MediaClip> CreateVisualClipAsync(
        CompositionVisualPlan visual,
        Color backgroundColor,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (visual.Kind == CompositionVisualKind.Filler)
        {
            return CreateBackgroundClip(backgroundColor, visual.DurationMilliseconds);
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(visual.SourcePath);
            cancellationToken.ThrowIfCancellationRequested();
            var clip = visual.Kind == CompositionVisualKind.Image
                ? await MediaClip.CreateFromImageFileAsync(file, ToTimeSpan(visual.DurationMilliseconds))
                : await MediaClip.CreateFromFileAsync(file);
            cancellationToken.ThrowIfCancellationRequested();
            if (visual.Kind == CompositionVisualKind.Video)
            {
                ApplyTrim(clip, visual.SourceInMilliseconds, visual.SourceOutMilliseconds);
                EnsureDurationMatches(clip.TrimmedDuration, visual.DurationMilliseconds);
            }

            clip.Volume = visual.Volume;
            return clip;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsItemFailure(exception))
        {
            errors.Add($"'{visual.AssetName}' could not be loaded and was replaced with the project background.");
            return CreateBackgroundClip(backgroundColor, visual.DurationMilliseconds);
        }
    }

    private static async Task AddAudioTracksAsync(
        MediaComposition composition,
        IEnumerable<CompositionAudioPlan> audioTracks,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        foreach (var audio in audioTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(audio.SourcePath);
                cancellationToken.ThrowIfCancellationRequested();
                var track = await BackgroundAudioTrack.CreateFromFileAsync(file);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyTrim(track, audio.SourceInMilliseconds, audio.SourceOutMilliseconds);
                EnsureDurationMatches(
                    track.TrimmedDuration,
                    audio.SourceOutMilliseconds - audio.SourceInMilliseconds);

                track.Delay = ToTimeSpan(audio.DelayMilliseconds);
                track.Volume = audio.Volume;
                composition.BackgroundAudioTracks.Add(track);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsItemFailure(exception))
            {
                errors.Add($"'{audio.AssetName}' could not be loaded and was omitted.");
            }
        }
    }

    private static async Task AddTextOverlaysAsync(
        MediaComposition composition,
        ProjectDocument project,
        IEnumerable<CompositionTextOverlayPlan> overlayPlans,
        TextOverlayRenderer? textOverlayRenderer,
        Windows.Foundation.Rect overlayPosition,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (textOverlayRenderer?.RenderHost is not { } renderHost)
        {
            return;
        }

        var layer = new MediaOverlayLayer();
        foreach (var overlayPlan in overlayPlans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = project.TextItems.First(item => item.Id == overlayPlan.ItemId);
            try
            {
                var path = await textOverlayRenderer.RenderAsync(project, item, renderHost, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var file = await StorageFile.GetFileFromPathAsync(path);
                var overlayClip = await MediaClip.CreateFromImageFileAsync(file, ToTimeSpan(overlayPlan.DurationMilliseconds));
                cancellationToken.ThrowIfCancellationRequested();
                layer.Overlays.Add(new MediaOverlay(overlayClip, overlayPosition, 1)
                {
                    AudioEnabled = false,
                    Delay = ToTimeSpan(overlayPlan.DelayMilliseconds)
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsItemFailure(exception) || exception is InvalidOperationException)
            {
                var name = string.IsNullOrWhiteSpace(item.Text) ? item.Id.ToString("D") : item.Text;
                errors.Add($"Text '{name}' could not be rendered and was omitted.");
            }
        }

        if (layer.Overlays.Count > 0)
        {
            composition.OverlayLayers.Add(layer);
        }
    }

    private static void EnsureDurationMatches(TimeSpan actualDuration, long requestedDurationMilliseconds)
    {
        if (Math.Abs((actualDuration - ToTimeSpan(requestedDurationMilliseconds)).TotalMilliseconds) > 2)
        {
            throw new InvalidDataException("The source range does not match the timeline duration.");
        }
    }

    internal static Windows.Foundation.Rect CreateOverlayPosition(int outputWidth, int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);
        return new Windows.Foundation.Rect(0, 0, outputWidth, outputHeight);
    }

    internal static Color ResolveBackgroundColor(string? opaqueArgb)
    {
        var value = TimelineInput.IsOpaqueArgb(opaqueArgb)
            ? opaqueArgb!
            : ProjectSettings.DefaultBackgroundColor;
        return Color.FromArgb(
            255,
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static MediaClip CreateBackgroundClip(Color backgroundColor, long durationMilliseconds) =>
        MediaClip.CreateFromColor(backgroundColor, ToTimeSpan(durationMilliseconds));

    internal static void ApplyTrim(MediaClip clip, long sourceInMilliseconds, long sourceOutMilliseconds)
    {
        var sourceIn = ToTimeSpan(sourceInMilliseconds);
        var sourceOut = ToTimeSpan(sourceOutMilliseconds);
        clip.TrimTimeFromStart = sourceIn < clip.OriginalDuration ? sourceIn : clip.OriginalDuration;
        clip.TrimTimeFromEnd = sourceOut < clip.OriginalDuration ? clip.OriginalDuration - sourceOut : TimeSpan.Zero;
    }

    private static void ApplyTrim(BackgroundAudioTrack track, long sourceInMilliseconds, long sourceOutMilliseconds)
    {
        var sourceIn = ToTimeSpan(sourceInMilliseconds);
        var sourceOut = ToTimeSpan(sourceOutMilliseconds);
        track.TrimTimeFromStart = sourceIn < track.OriginalDuration ? sourceIn : track.OriginalDuration;
        track.TrimTimeFromEnd = sourceOut < track.OriginalDuration ? track.OriginalDuration - sourceOut : TimeSpan.Zero;
    }

    private static bool IsItemFailure(Exception exception) =>
        MediaImportService.IsExpectedMediaFailure(exception);

    private static TimeSpan ToTimeSpan(long milliseconds) => TimeSpan.FromMilliseconds(Math.Clamp(
        milliseconds,
        0,
        ProjectDocument.MaximumTimelineDurationMilliseconds));
}

public sealed record CompositionBuildResult(
    MediaComposition Composition,
    IReadOnlyList<string> Errors,
    bool HasVisualContent)
{
    public bool HasContent => Composition.Clips.Count > 0 && Composition.Duration > TimeSpan.Zero;

    internal static IReadOnlyList<string> SelectPreviewErrors(IEnumerable<string> errors) => errors
        .Select(error => error.ReplaceLineEndings(" ").Trim())
        .Where(error => error.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Take(3)
        .ToArray();
}
