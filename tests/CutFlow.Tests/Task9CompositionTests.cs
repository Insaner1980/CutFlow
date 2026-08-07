using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task9CompositionTests
{
    [TestMethod]
    public void CreatePlan_PreservesMagneticOrderTrimsVolumeAndAudioDelay()
    {
        var project = ProjectDocument.CreateNew("Composition", DateTimeOffset.UnixEpoch);
        var video = Asset(ProjectAssetKind.Video, "first.mp4", 8_000);
        var image = Asset(ProjectAssetKind.Image, "still.png", 5_000);
        var audio = Asset(ProjectAssetKind.Audio, "music.wav", 9_000);
        project.Assets.AddRange([video, image, audio]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = video.Id, SourceInMilliseconds = 1_000,
            SourceOutMilliseconds = 4_500, DurationMilliseconds = 3_500, Volume = 0.65
        });
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = image.Id, DurationMilliseconds = 2_250, IsMuted = true
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = audio.Id, StartMilliseconds = 750,
            SourceInMilliseconds = 500, SourceOutMilliseconds = 5_500, Volume = 0.4
        });

        var plan = CompositionPlan.Create(project);

        Assert.HasCount(2, plan.Visuals);
        Assert.AreEqual(CompositionVisualKind.Video, plan.Visuals[0].Kind);
        Assert.AreEqual(1_000L, plan.Visuals[0].SourceInMilliseconds);
        Assert.AreEqual(4_500L, plan.Visuals[0].SourceOutMilliseconds);
        Assert.AreEqual(3_500L, plan.Visuals[0].DurationMilliseconds);
        Assert.AreEqual(0.65, plan.Visuals[0].Volume);
        Assert.AreEqual(CompositionVisualKind.Image, plan.Visuals[1].Kind);
        Assert.AreEqual(2_250L, plan.Visuals[1].DurationMilliseconds);
        Assert.AreEqual(0d, plan.Visuals[1].Volume);
        Assert.HasCount(1, plan.AudioTracks);
        Assert.AreEqual(750L, plan.AudioTracks[0].DelayMilliseconds);
        Assert.AreEqual(500L, plan.AudioTracks[0].SourceInMilliseconds);
        Assert.AreEqual(5_500L, plan.AudioTracks[0].SourceOutMilliseconds);
        Assert.AreEqual(0.4, plan.AudioTracks[0].Volume);
    }

    [TestMethod]
    public void CreatePlan_MissingVisualUsesSameDurationFillerAndNamesError()
    {
        var project = ProjectDocument.CreateNew("Missing", DateTimeOffset.UnixEpoch);
        var missing = Asset(ProjectAssetKind.Video, "gone.mp4", 4_000);
        missing.IsMissing = true;
        project.Assets.Add(missing);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = missing.Id, SourceOutMilliseconds = 4_000, DurationMilliseconds = 4_000
        });
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = Guid.NewGuid(), DurationMilliseconds = 2_000
        });

        var plan = CompositionPlan.Create(project);

        Assert.HasCount(2, plan.Visuals);
        Assert.IsTrue(plan.Visuals.All(visual => visual.Kind == CompositionVisualKind.Filler));
        CollectionAssert.AreEqual(new long[] { 4_000, 2_000 }, plan.Visuals.Select(visual => visual.DurationMilliseconds).ToArray());
        Assert.IsTrue(plan.Errors.Any(error => error.Contains("gone.mp4", StringComparison.Ordinal)));
        Assert.IsTrue(plan.Errors.Any(error => error.Contains("Unknown visual", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreatePlan_AudioOrTextBeyondV1AppendsOneTrailingFillerToExactDuration()
    {
        var project = ProjectDocument.CreateNew("Tail", DateTimeOffset.UnixEpoch);
        var image = Asset(ProjectAssetKind.Image, "still.png", 1_000);
        project.Assets.Add(image);
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), AssetId = image.Id, DurationMilliseconds = 1_000 });
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 4_000, DurationMilliseconds = 2_000 });

        var plan = CompositionPlan.Create(project);

        Assert.AreEqual(6_000L, plan.TargetDurationMilliseconds);
        Assert.HasCount(2, plan.Visuals);
        Assert.AreEqual(CompositionVisualKind.Filler, plan.Visuals[1].Kind);
        Assert.AreEqual(5_000L, plan.Visuals[1].DurationMilliseconds);
    }

    [TestMethod]
    public void CreatePlan_TextOverlayCarriesExactDelayAndDuration()
    {
        var project = ProjectDocument.CreateNew("Overlay", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), StartMilliseconds = 1_250, DurationMilliseconds = 2_750 };
        project.TextItems.Add(item);

        var overlay = CompositionPlan.Create(project).TextOverlays.Single();

        Assert.AreEqual(item.Id, overlay.ItemId);
        Assert.AreEqual(1_250L, overlay.DelayMilliseconds);
        Assert.AreEqual(2_750L, overlay.DurationMilliseconds);
    }

    [DataTestMethod]
    [DataRow(1280, 720)]
    [DataRow(1920, 1080)]
    public void CreateOverlayPosition_UsesTheActualOutputFrame(int outputWidth, int outputHeight)
    {
        var position = CompositionService.CreateOverlayPosition(outputWidth, outputHeight);

        Assert.AreEqual(0d, position.X);
        Assert.AreEqual(0d, position.Y);
        Assert.AreEqual(outputWidth, position.Width);
        Assert.AreEqual(outputHeight, position.Height);
    }

    [TestMethod]
    public void CreatePlan_MissingAudioIsOmittedAndNamedWithoutShiftingDuration()
    {
        var project = ProjectDocument.CreateNew("Missing audio", DateTimeOffset.UnixEpoch);
        var audio = Asset(ProjectAssetKind.Audio, "lost.wav", 2_000);
        audio.IsMissing = true;
        project.Assets.Add(audio);
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = audio.Id, StartMilliseconds = 500, SourceOutMilliseconds = 2_000
        });

        var plan = CompositionPlan.Create(project);

        Assert.HasCount(0, plan.AudioTracks);
        Assert.AreEqual(2_500L, plan.TargetDurationMilliseconds);
        Assert.HasCount(1, plan.Visuals);
        Assert.AreEqual(2_500L, plan.Visuals[0].DurationMilliseconds);
        Assert.IsTrue(plan.Errors.Any(error => error.Contains("lost.wav", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PreviewRebuildGate_NewerLeaseCancelsAndObsoletesOlderLease()
    {
        using var gate = new PreviewRebuildGate();
        using var first = gate.Begin();
        using var second = gate.Begin();
        var committedVersion = 0L;

        Assert.IsTrue(first.Token.IsCancellationRequested);
        Assert.IsFalse(first.IsCurrent);
        Assert.IsFalse(first.TryCommit(() => committedVersion = first.Version));
        Assert.IsTrue(second.IsCurrent);
        Assert.IsFalse(second.Token.IsCancellationRequested);
        Assert.IsTrue(second.TryCommit(() => committedVersion = second.Version));
        Assert.AreEqual(second.Version, committedVersion);
    }

    [TestMethod]
    public void PreviewRebuildGate_DisposeCancelsCurrentAndIsIdempotent()
    {
        var gate = new PreviewRebuildGate();
        using var lease = gate.Begin();

        gate.Dispose();
        gate.Dispose();

        Assert.IsTrue(lease.Token.IsCancellationRequested);
        Assert.IsFalse(lease.IsCurrent);
    }

    [TestMethod]
    public void PreviewResourceCleanup_DetachesElementBeforeDisposingNativeResources()
    {
        var calls = new List<string>();

        PreviewResourceCleanup.Run(
            () => calls.Add("pause"),
            () => calls.Add("clear-source"),
            () => calls.Add("detach-element"),
            () => calls.Add("dispose-source"),
            () => calls.Add("dispose-player"));

        CollectionAssert.AreEqual(
            new[] { "pause", "clear-source", "detach-element", "dispose-source", "dispose-player" },
            calls);
    }

    [TestMethod]
    public void TimelinePlaybackMath_ClampsAndScrollsOnlyOutsideViewport()
    {
        Assert.AreEqual(0L, TimelinePlaybackMath.ClampPosition(-1, 5_000));
        Assert.AreEqual(5_000L, TimelinePlaybackMath.ClampPosition(7_000, 5_000));
        Assert.AreEqual(2_500L, TimelinePlaybackMath.ClampPosition(2_500, 5_000));
        Assert.AreEqual(200d, TimelinePlaybackMath.EnsureVisibleOffset(450, 200, 500, 1_200));
        Assert.AreEqual(520d, TimelinePlaybackMath.EnsureVisibleOffset(1_020, 200, 500, 1_200));
        Assert.AreEqual(100d, TimelinePlaybackMath.EnsureVisibleOffset(100, 200, 500, 1_200));
    }

    [TestMethod]
    public async Task BuildAsync_MissingVisualCreatesRealSameDurationBlackClip()
    {
        var project = ProjectDocument.CreateNew("Native filler", DateTimeOffset.UnixEpoch);
        var missing = Asset(ProjectAssetKind.Video, "missing.mp4", 1_750);
        missing.IsMissing = true;
        project.Assets.Add(missing);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = missing.Id, DurationMilliseconds = 1_750, SourceOutMilliseconds = 1_750
        });

        var result = await new CompositionService().BuildAsync(project, null, CancellationToken.None);

        Assert.HasCount(1, result.Composition.Clips);
        Assert.AreEqual(1_750d, result.Composition.Duration.TotalMilliseconds, 1);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing.mp4", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task BuildAsync_RealFilesApplyVisualTrimsAndAudioDelayVolume()
    {
        var mediaRoot = FindMediaRoot();
        var project = ProjectDocument.CreateNew("Native media", DateTimeOffset.UnixEpoch);
        var video = Asset(ProjectAssetKind.Video, "valid-video.mp4", 2_000);
        video.SourcePath = Path.Combine(mediaRoot, video.FileName);
        var image = Asset(ProjectAssetKind.Image, "valid-image.jpg", 5_000);
        image.SourcePath = Path.Combine(mediaRoot, image.FileName);
        var audio = Asset(ProjectAssetKind.Audio, "valid-audio.wav", 2_000);
        audio.SourcePath = Path.Combine(mediaRoot, audio.FileName);
        project.Assets.AddRange([video, image, audio]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = video.Id, SourceInMilliseconds = 100,
            SourceOutMilliseconds = 1_100, DurationMilliseconds = 1_000, Volume = 0.6
        });
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = image.Id, DurationMilliseconds = 500, IsMuted = true
        });
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(), AssetId = audio.Id, StartMilliseconds = 250,
            SourceInMilliseconds = 100, SourceOutMilliseconds = 800, Volume = 0.3
        });

        var result = await new CompositionService().BuildAsync(project, null, CancellationToken.None);

        Assert.HasCount(2, result.Composition.Clips);
        Assert.AreEqual(100d, result.Composition.Clips[0].TrimTimeFromStart.TotalMilliseconds, 2);
        Assert.AreEqual(1_000d, result.Composition.Clips[0].TrimmedDuration.TotalMilliseconds, 2);
        Assert.AreEqual(0.6, result.Composition.Clips[0].Volume);
        Assert.AreEqual(0d, result.Composition.Clips[1].Volume);
        Assert.HasCount(1, result.Composition.BackgroundAudioTracks);
        Assert.AreEqual(250d, result.Composition.BackgroundAudioTracks[0].Delay.TotalMilliseconds, 2);
        Assert.AreEqual(100d, result.Composition.BackgroundAudioTracks[0].TrimTimeFromStart.TotalMilliseconds, 2);
        Assert.AreEqual(0.3, result.Composition.BackgroundAudioTracks[0].Volume);
        Assert.HasCount(0, result.Errors);
    }

    [TestMethod]
    public async Task BuildPreviewAsync_LeavesTextForTheSingleLiveXamlLayer()
    {
        var mediaRoot = FindMediaRoot();
        var project = ProjectDocument.CreateNew("Live preview text", DateTimeOffset.UnixEpoch);
        var image = Asset(ProjectAssetKind.Image, "valid-image.jpg", 1_000);
        image.SourcePath = Path.Combine(mediaRoot, image.FileName);
        project.Assets.Add(image);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = image.Id,
            DurationMilliseconds = 1_000
        });
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            Text = "One visible layer",
            DurationMilliseconds = 1_000
        });

        var result = await new CompositionService().BuildPreviewAsync(project, CancellationToken.None);

        Assert.HasCount(1, result.Composition.Clips);
        Assert.HasCount(0, result.Composition.OverlayLayers);
        Assert.HasCount(0, result.Errors);
    }

    [TestMethod]
    public async Task BuildPreviewAsync_AudioOnlyKeepsPlaybackButReportsNoVisualContent()
    {
        var mediaRoot = FindMediaRoot();
        var project = ProjectDocument.CreateNew("Audio only", DateTimeOffset.UnixEpoch);
        var audio = Asset(ProjectAssetKind.Audio, "valid-audio.wav", 2_000);
        audio.SourcePath = Path.Combine(mediaRoot, audio.FileName);
        project.Assets.Add(audio);
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = audio.Id,
            SourceOutMilliseconds = 2_000
        });

        var result = await new CompositionService().BuildPreviewAsync(project, CancellationToken.None);

        Assert.IsTrue(result.HasContent);
        Assert.IsFalse(result.HasVisualContent);
        Assert.HasCount(1, result.Composition.BackgroundAudioTracks);
    }

    [TestMethod]
    public async Task BuildAsync_WhenAudioSourceIsShorterThanRequestedTrim_OmitsTrackAndNamesAsset()
    {
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        var fixturePath = Path.Combine(fixtureDirectory, "one-second.wav");
        WriteSilentWave(fixturePath, durationMilliseconds: 1_000);
        var project = ProjectDocument.CreateNew("Short native audio", DateTimeOffset.UnixEpoch);
        var audio = Asset(ProjectAssetKind.Audio, "one-second.wav", 1_000);
        audio.SourcePath = fixturePath;
        project.Assets.Add(audio);
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = audio.Id,
            SourceOutMilliseconds = 2_000
        });

        try
        {
            var result = await new CompositionService().BuildAsync(project, null, CancellationToken.None);

            Assert.HasCount(0, result.Composition.BackgroundAudioTracks);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("one-second.wav", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void PreviewPlaybackPolicy_RestartsAtEndOnlyWhenLoopEnabled(bool loopEnabled, bool expected)
    {
        Assert.AreEqual(expected, PreviewPlaybackPolicy.ShouldRestartAtEnd(loopEnabled));
    }

    private static string FindMediaRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".superpowers", "sdd", "2026-08-04-cutflow-v1", "scratch", "task-7-media");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Task media fixtures were not found.");
    }

    private static ProjectAsset Asset(ProjectAssetKind kind, string name, long duration) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, FileName = name, SourcePath = Path.Combine(Path.GetTempPath(), name),
        DurationMilliseconds = duration
    };

    private static void WriteSilentWave(string path, int durationMilliseconds)
    {
        const int sampleRate = 8_000;
        const short channelCount = 1;
        const short bitsPerSample = 16;
        var sampleCount = sampleRate * durationMilliseconds / 1_000;
        var dataLength = sampleCount * channelCount * bitsPerSample / 8;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channelCount * bitsPerSample / 8);
        writer.Write((short)(channelCount * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
    }
}
