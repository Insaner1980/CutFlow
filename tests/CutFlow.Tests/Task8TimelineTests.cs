using System.Text.Json;
using CutFlow.Controls;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task8TimelineTests
{
    [TestMethod]
    public void VideoBounds_ProjectMagneticStartsFromEffectiveDurations()
    {
        var project = TestProjects.WithVideo(1_000, 2_000, 750);

        var bounds = TimelineLayoutProjection.GetVideoBounds(project);

        CollectionAssert.AreEqual(new long[] { 0, 1_000, 3_000 }, bounds.Select(bound => bound.StartMilliseconds).ToArray());
        CollectionAssert.AreEqual(new long[] { 1_000, 2_000, 750 }, bounds.Select(bound => bound.DurationMilliseconds).ToArray());
        Assert.AreEqual(3_750L, bounds[^1].EndMilliseconds);
    }

    [TestMethod]
    public void VisibleRulerTicks_OnlyReturnsViewportAndOneIntervalBuffer()
    {
        var scale = new TimelineScale(100);

        var ticks = scale.GetVisibleRulerTicks(horizontalOffset: 1_000, viewportWidth: 500, durationMilliseconds: 30_000);

        Assert.IsTrue(ticks.Count <= 8);
        Assert.AreEqual(9_000L, ticks[0].Milliseconds);
        Assert.AreEqual(16_000L, ticks[^1].Milliseconds);
    }

    [TestMethod]
    public void ZoomOffset_KeepsPointerTimeAnchoredAndClampsAtContentEnd()
    {
        var offset = TimelineScale.CalculateAnchoredOffset(
            oldPixelsPerSecond: 100,
            newPixelsPerSecond: 200,
            oldHorizontalOffset: 400,
            pointerViewportX: 200,
            durationMilliseconds: 10_000,
            viewportWidth: 500);

        Assert.AreEqual(1_000d, offset, 0.001);
        Assert.AreEqual(1_500d, TimelineScale.ClampHorizontalOffset(99_000, 2_000, 500), 0.001);
    }

    [TestMethod]
    public void WheelZoom_UsesWheelDirectionAndScaleBounds()
    {
        Assert.AreEqual(112d, TimelineScale.CalculateWheelZoomTarget(100, 120), 0.001);
        Assert.AreEqual(100d / 1.12d, TimelineScale.CalculateWheelZoomTarget(100, -120), 0.001);
        Assert.AreEqual(TimelineScale.MaximumPixelsPerSecond, TimelineScale.CalculateWheelZoomTarget(399, 120), 0.001);
        Assert.AreEqual(TimelineScale.MinimumPixelsPerSecond, TimelineScale.CalculateWheelZoomTarget(21, -120), 0.001);
        Assert.AreEqual(100d, TimelineScale.CalculateWheelZoomTarget(100, 0), 0.001);
    }

    [DataTestMethod]
    [DataRow((int)TimelineDragOperation.VideoTrimStart, 250L)]
    [DataRow((int)TimelineDragOperation.VideoTrimEnd, 1_750L)]
    [DataRow((int)TimelineDragOperation.AudioMove, 600L)]
    [DataRow((int)TimelineDragOperation.AudioTrimStart, 250L)]
    [DataRow((int)TimelineDragOperation.AudioTrimEnd, 1_750L)]
    [DataRow((int)TimelineDragOperation.TextMove, 600L)]
    [DataRow((int)TimelineDragOperation.TextTrimStart, 600L)]
    [DataRow((int)TimelineDragOperation.TextTrimEnd, 2_600L)]
    public void DragInitialValue_NoPointerMovePreservesTheOriginalEditValue(
        int operationValue,
        long expected)
    {
        var bounds = new TimelineItemBounds(Guid.NewGuid(), EditorSelectionKind.AudioItem, 600, 2_000);

        var value = TimelineDragPreview.InitialValue((TimelineDragOperation)operationValue, bounds, 250, 1_750, isImage: false);

        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow((int)TimelineDragOperation.VideoTrimStart)]
    [DataRow((int)TimelineDragOperation.VideoTrimEnd)]
    public void DragInitialValue_ImageTrimPreservesTimelineDuration(int operationValue)
    {
        var bounds = new TimelineItemBounds(Guid.NewGuid(), EditorSelectionKind.VideoItem, 0, 5_000);

        var value = TimelineDragPreview.InitialValue((TimelineDragOperation)operationValue, bounds, 0, 5_000, isImage: true);

        Assert.AreEqual(5_000L, value);
    }

    [TestMethod]
    public void Snap_UsesHundredMillisecondsAndRelevantEdgesOnlyWhenEnabled()
    {
        Assert.AreEqual(1_000L, TimelineSnapper.Snap(1_047, true, [2_000]));
        Assert.AreEqual(2_000L, TimelineSnapper.Snap(1_960, true, [2_000]));
        Assert.AreEqual(1_047L, TimelineSnapper.Snap(1_047, false, [2_000]));
    }

    [TestMethod]
    public void VideoTrimStart_SnapsGlobalBoundaryOnlyWhenEnabled()
    {
        var bounds = new TimelineItemBounds(Guid.NewGuid(), EditorSelectionKind.VideoItem, 1_000, 2_000);

        var enabled = TimelineDragPreview.CalculateVideoTrimStart(bounds, sourceInMilliseconds: 250, deltaMilliseconds: 47, snappingEnabled: true, []);
        var disabled = TimelineDragPreview.CalculateVideoTrimStart(bounds, sourceInMilliseconds: 250, deltaMilliseconds: 47, snappingEnabled: false, []);

        Assert.AreEqual(new TimelineVideoTrimPreview(1_000, 2_000, 250), enabled);
        Assert.AreEqual(new TimelineVideoTrimPreview(1_047, 1_953, 297), disabled);
    }

    [TestMethod]
    public void VideoTrimEnd_SnapsGlobalBoundaryOnlyWhenEnabled()
    {
        var bounds = new TimelineItemBounds(Guid.NewGuid(), EditorSelectionKind.VideoItem, 1_000, 2_000);

        var enabled = TimelineDragPreview.CalculateVideoTrimEnd(bounds, sourceOutMilliseconds: 2_250, deltaMilliseconds: 47, snappingEnabled: true, []);
        var disabled = TimelineDragPreview.CalculateVideoTrimEnd(bounds, sourceOutMilliseconds: 2_250, deltaMilliseconds: 47, snappingEnabled: false, []);

        Assert.AreEqual(new TimelineVideoTrimPreview(3_000, 2_000, 2_250), enabled);
        Assert.AreEqual(new TimelineVideoTrimPreview(3_047, 2_047, 2_297), disabled);
    }

    [DataTestMethod]
    [DataRow(EditorSelectionKind.VideoItem, true)]
    [DataRow(EditorSelectionKind.AudioItem, true)]
    [DataRow(EditorSelectionKind.TextItem, true)]
    [DataRow(EditorSelectionKind.Asset, false)]
    [DataRow(EditorSelectionKind.Project, false)]
    [DataRow(EditorSelectionKind.None, false)]
    public void DuplicateCommand_IsAvailableForTimelineItemsOnly(EditorSelectionKind kind, bool expected)
    {
        Assert.AreEqual(expected, TimelineContextCommands.SupportsDuplicate(kind));
    }

    [DataTestMethod]
    [DataRow(EditorSelectionKind.VideoItem, true, true, true)]
    [DataRow(EditorSelectionKind.VideoItem, false, true, false)]
    [DataRow(EditorSelectionKind.VideoItem, true, false, false)]
    [DataRow(EditorSelectionKind.Project, true, true, false)]
    public void DuplicateShortcut_UsesTimelineDuplicateAvailability(
        EditorSelectionKind kind,
        bool controlDown,
        bool isDuplicateKey,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            TimelineContextCommands.ShouldHandleDuplicateShortcut(kind, controlDown, isDuplicateKey));
    }

    [TestMethod]
    public void ImageDuration_CanExceedStillMetadataAndResetToFiveSeconds()
    {
        var project = CreateProjectWithImage();
        var item = project.VideoItems.Single();

        Assert.IsTrue(TimelineEditingService.SetImageDuration(project, item.Id, 12_000));
        Assert.AreEqual(12_000L, item.DurationMilliseconds);
        Assert.IsTrue(TimelineEditingService.ResetImageDuration(project, item.Id));
        Assert.AreEqual(5_000L, item.DurationMilliseconds);
        Assert.AreEqual(5_000L, project.Assets.Single().DurationMilliseconds);
    }

    [TestMethod]
    public void AudioTrims_ClampSourceAndMoveLeftTimelineEdge()
    {
        var project = CreateProjectWithAudio();
        var item = project.AudioItems.Single();

        Assert.IsTrue(TimelineEditingService.TrimAudioStart(project, item.Id, 750));
        Assert.AreEqual(1_250L, item.StartMilliseconds);
        Assert.AreEqual(750L, item.SourceInMilliseconds);
        Assert.IsTrue(TimelineEditingService.TrimAudioEnd(project, item.Id, 99_000));
        Assert.AreEqual(4_000L, item.SourceOutMilliseconds);
        Assert.IsFalse(TimelineEditingService.TrimAudioEnd(project, item.Id, 4_000));
    }

    [TestMethod]
    public void TextEdgeTrims_ClampToMinimumDurationAndMoveBody()
    {
        var project = new ProjectDocument();
        var item = TestProjects.Text(1_000, 2_000);
        project.TextItems.Add(item);

        Assert.IsTrue(TimelineEditingService.TrimTextStart(project, item.Id, 2_950));
        Assert.AreEqual(2_900L, item.StartMilliseconds);
        Assert.AreEqual(100L, item.DurationMilliseconds);
        Assert.IsTrue(TimelineEditingService.TrimTextEnd(project, item.Id, 4_000));
        Assert.AreEqual(1_100L, item.DurationMilliseconds);
        Assert.IsTrue(TimelineEditingService.MoveTextItem(project, item.Id, 500));
        Assert.AreEqual(500L, item.StartMilliseconds);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("not a number")]
    public void NumericValidation_RejectsInvalidAndNonFiniteValues(string value)
    {
        Assert.IsFalse(TimelineInput.TryParseFiniteDouble(value, out _));
        Assert.IsFalse(TimelineInput.TryParseSeconds(value, out _));
    }

    [TestMethod]
    public void NewModelProperties_RoundTripAndOldJsonUsesSafeDefaults()
    {
        var project = CreateProjectWithAudio();
        project.Settings.BackgroundColor = "#FF123456";
        project.VideoItems.Add(new VideoTimelineItem { Id = Guid.NewGuid(), Volume = 0.4, IsMuted = true, SourceOutMilliseconds = 1_000 });
        project.AudioItems[0].FadeInMilliseconds = 125;
        project.AudioItems[0].FadeOutMilliseconds = 250;

        var roundTrip = JsonSerializer.Deserialize<ProjectDocument>(JsonSerializer.Serialize(project))!;

        Assert.AreEqual("#FF123456", roundTrip.Settings.BackgroundColor);
        Assert.AreEqual(0.4, roundTrip.VideoItems[^1].Volume);
        Assert.IsTrue(roundTrip.VideoItems[^1].IsMuted);
        Assert.AreEqual(125L, roundTrip.AudioItems[0].FadeInMilliseconds);
        Assert.AreEqual(250L, roundTrip.AudioItems[0].FadeOutMilliseconds);

        var oldSettings = JsonSerializer.Deserialize<ProjectSettings>("{\"width\":1920,\"height\":1080}")!;
        var oldVideo = JsonSerializer.Deserialize<VideoTimelineItem>("{\"sourceInMilliseconds\":0,\"sourceOutMilliseconds\":1000}")!;
        Assert.AreEqual(ProjectSettings.DefaultBackgroundColor, oldSettings.BackgroundColor);
        Assert.AreEqual(1d, oldVideo.Volume);
        Assert.IsFalse(oldVideo.IsMuted);
        Assert.AreEqual(1_000L, oldVideo.DurationMilliseconds);
    }

    [TestMethod]
    public void SuccessfulWrapper_CreatesExactlyOneUndoEntryAndNoOpCreatesNone()
    {
        var project = TestProjects.WithVideo(2_000);
        var item = project.VideoItems.Single();
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsFalse(viewModel.ReorderVideoItem(item.Id, 0));
        Assert.IsFalse(viewModel.TrimVideoEnd(item.Id, 2_000));
        Assert.IsFalse(viewModel.CanUndo);
        Assert.AreEqual(0, committed);

        Assert.IsTrue(viewModel.TrimVideoEnd(item.Id, 1_500));
        Assert.IsTrue(viewModel.CanUndo);
        Assert.AreEqual(1, committed);
        viewModel.Undo();
        Assert.AreEqual(2_000L, viewModel.Project.VideoItems.Single().DurationMilliseconds);
    }

    [TestMethod]
    public void SplitWrapper_RejectsBoundariesAndCommitsInteriorExactlyOnce()
    {
        var project = TestProjects.WithVideo(1_000);
        var item = project.VideoItems.Single();
        var viewModel = new EditorViewModel(project);

        Assert.IsFalse(viewModel.SplitVideoItem(item.Id, 0));
        Assert.IsFalse(viewModel.SplitVideoItem(item.Id, 1_000));
        Assert.IsFalse(viewModel.CanUndo);
        Assert.IsTrue(viewModel.SplitVideoItem(item.Id, 500));
        Assert.HasCount(2, viewModel.Project.VideoItems);
        viewModel.Undo();
        Assert.HasCount(1, viewModel.Project.VideoItems);
        Assert.IsFalse(viewModel.CanUndo);
    }

    [TestMethod]
    public void DeleteUndoRedo_NormalizesOrphanSelection()
    {
        var project = TestProjects.WithVideo(1_000);
        var item = project.VideoItems.Single();
        var viewModel = new EditorViewModel(project);
        viewModel.Select(new EditorSelection(EditorSelectionKind.VideoItem, item.Id));

        Assert.IsTrue(viewModel.DeleteSelection());
        Assert.AreEqual(EditorSelection.None, viewModel.Selection);
        viewModel.Undo();
        viewModel.Select(new EditorSelection(EditorSelectionKind.VideoItem, item.Id));
        viewModel.Redo();
        Assert.AreEqual(EditorSelection.None, viewModel.Selection);
    }

    [TestMethod]
    public void InspectorWrappers_CommitVolumeMuteAndBackgroundWithValidation()
    {
        var project = TestProjects.WithVideo(1_000);
        var video = project.VideoItems.Single();
        var audioAsset = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Audio, DurationMilliseconds = 2_000 };
        var audio = new AudioTimelineItem { Id = Guid.NewGuid(), AssetId = audioAsset.Id, SourceOutMilliseconds = 2_000 };
        project.Assets.Add(audioAsset);
        project.AudioItems.Add(audio);
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.SetVideoVolume(video.Id, 0.25));
        Assert.IsTrue(viewModel.SetVideoMuted(video.Id, true));
        Assert.IsTrue(viewModel.SetAudioVolume(audio.Id, 0.5));
        Assert.IsTrue(viewModel.SetAudioMuted(audio.Id, true));
        Assert.IsTrue(viewModel.SetBackgroundColor("#FF102030"));
        Assert.IsFalse(viewModel.SetBackgroundColor("#80102030"));
        Assert.IsFalse(viewModel.SetAudioVolume(audio.Id, double.NaN));
        Assert.AreEqual("#FF102030", viewModel.Project.Settings.BackgroundColor);
    }

    private static ProjectDocument CreateProjectWithImage()
    {
        var project = new ProjectDocument();
        var asset = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceOutMilliseconds = 5_000,
            DurationMilliseconds = 5_000
        });
        return project;
    }

    private static ProjectDocument CreateProjectWithAudio()
    {
        var project = new ProjectDocument();
        var asset = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Audio, DurationMilliseconds = 4_000 };
        project.Assets.Add(asset);
        project.AudioItems.Add(new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            StartMilliseconds = 1_000,
            SourceInMilliseconds = 500,
            SourceOutMilliseconds = 3_000
        });
        return project;
    }
}
