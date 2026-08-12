using CutFlow.Models;
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
        foreach (var visual in plan.Visuals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MediaClip clip;
            if (visual.Kind == CompositionVisualKind.Filler)
            {
                clip = CreateBlackClip(visual.DurationMilliseconds);
            }
            else
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(visual.SourcePath);
                    cancellationToken.ThrowIfCancellationRequested();
                    clip = visual.Kind == CompositionVisualKind.Image
                        ? await MediaClip.CreateFromImageFileAsync(file, ToTimeSpan(visual.DurationMilliseconds))
                        : await MediaClip.CreateFromFileAsync(file);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (visual.Kind == CompositionVisualKind.Video)
                    {
                        ApplyTrim(clip, visual.SourceInMilliseconds, visual.SourceOutMilliseconds);
                        if (Math.Abs((clip.TrimmedDuration - ToTimeSpan(visual.DurationMilliseconds)).TotalMilliseconds) > 2)
                        {
                            throw new InvalidDataException("The source range does not match the timeline duration.");
                        }
                    }

                    clip.Volume = visual.Volume;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (IsItemFailure(exception))
                {
                    errors.Add($"'{visual.AssetName}' could not be loaded and was replaced with black video.");
                    clip = CreateBlackClip(visual.DurationMilliseconds);
                }
            }

            composition.Clips.Add(clip);
        }

        foreach (var audio in plan.AudioTracks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(audio.SourcePath);
                cancellationToken.ThrowIfCancellationRequested();
                var track = await BackgroundAudioTrack.CreateFromFileAsync(file);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyTrim(track, audio.SourceInMilliseconds, audio.SourceOutMilliseconds);
                var requestedDuration = ToTimeSpan(audio.SourceOutMilliseconds - audio.SourceInMilliseconds);
                if (Math.Abs((track.TrimmedDuration - requestedDuration).TotalMilliseconds) > 2)
                {
                    throw new InvalidDataException("The source range does not match the timeline duration.");
                }

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
                errors.Add($"'{audio.AssetName}' could not be loaded and was omitted: {exception.Message}");
            }
        }

        if (textOverlayRenderer?.RenderHost is { } renderHost)
        {
            var layer = new MediaOverlayLayer();
            foreach (var overlayPlan in plan.TextOverlays)
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
                    var overlay = new MediaOverlay(
                        overlayClip,
                        overlayPosition,
                        1)
                    {
                        AudioEnabled = false,
                        Delay = ToTimeSpan(overlayPlan.DelayMilliseconds)
                    };
                    layer.Overlays.Add(overlay);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (IsItemFailure(exception) || exception is InvalidOperationException)
                {
                    var name = string.IsNullOrWhiteSpace(item.Text) ? item.Id.ToString("D") : item.Text;
                    errors.Add($"Text '{name}' could not be rendered and was omitted: {exception.Message}");
                }
            }

            if (layer.Overlays.Count > 0)
            {
                composition.OverlayLayers.Add(layer);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new CompositionBuildResult(composition, errors, project.VideoItems.Count > 0);
    }

    internal static Windows.Foundation.Rect CreateOverlayPosition(int outputWidth, int outputHeight)
    {
        if (outputWidth <= 0) throw new ArgumentOutOfRangeException(nameof(outputWidth));
        if (outputHeight <= 0) throw new ArgumentOutOfRangeException(nameof(outputHeight));
        return new Windows.Foundation.Rect(0, 0, outputWidth, outputHeight);
    }

    private static MediaClip CreateBlackClip(long durationMilliseconds) =>
        MediaClip.CreateFromColor(Color.FromArgb(255, 0, 0, 0), ToTimeSpan(durationMilliseconds));

    private static void ApplyTrim(MediaClip clip, long sourceInMilliseconds, long sourceOutMilliseconds)
    {
        clip.TrimTimeFromStart = ToTimeSpan(Math.Min(sourceInMilliseconds, (long)clip.OriginalDuration.TotalMilliseconds));
        clip.TrimTimeFromEnd = ToTimeSpan(Math.Max(0, (long)clip.OriginalDuration.TotalMilliseconds - sourceOutMilliseconds));
    }

    private static void ApplyTrim(BackgroundAudioTrack track, long sourceInMilliseconds, long sourceOutMilliseconds)
    {
        track.TrimTimeFromStart = ToTimeSpan(Math.Min(sourceInMilliseconds, (long)track.OriginalDuration.TotalMilliseconds));
        track.TrimTimeFromEnd = ToTimeSpan(Math.Max(0, (long)track.OriginalDuration.TotalMilliseconds - sourceOutMilliseconds));
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
}
