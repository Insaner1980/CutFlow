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
    public void ManyMinimumVideoClipsAtLowZoom_KeepVisualWidthSeparateFromTimeHitTargets()
    {
        var project = TestProjects.WithVideo(Enumerable.Repeat(100L, 50).ToArray());
        var bounds = TimelineLayoutProjection.GetVideoBounds(project);
        var scale = new TimelineScale(TimelineScale.MinimumPixelsPerSecond);
        var cards = bounds.Select(bound => scale.GetCardGeometry(bound, 18, 3)).ToArray();

        Assert.HasCount(50, cards);
        for (var index = 0; index < cards.Length; index++)
        {
            Assert.AreEqual(2d, cards[index].HitWidth, 0.001);
            Assert.AreEqual(18d, cards[index].VisualWidth, 0.001);
            Assert.AreEqual(index, TimelineLayoutProjection.GetVideoTargetIndex(
                bounds,
                bounds[index].ItemId,
                bounds[index].StartMilliseconds + 25));
            Assert.AreEqual(index, TimelineLayoutProjection.GetVideoTargetIndex(
                bounds,
                bounds[index].ItemId,
                bounds[index].StartMilliseconds + 75));
            Assert.AreEqual(TimelineCardHit.Start, TimelineCardHitTest.Resolve(0.5, cards[index].HitWidth, 8));
            Assert.AreEqual(TimelineCardHit.Body, TimelineCardHitTest.Resolve(1, cards[index].HitWidth, 8));
            Assert.AreEqual(TimelineCardHit.End, TimelineCardHitTest.Resolve(1.5, cards[index].HitWidth, 8));
            if (index + 1 < cards.Length)
            {
                Assert.AreEqual(cards[index].HitRight, cards[index + 1].HitLeft, 0.001);
                Assert.IsTrue(cards[index + 1].VisualLeft > cards[index].VisualLeft);
                Assert.IsTrue(cards[index].VisualLeft + cards[index].VisualWidth > cards[index + 1].VisualLeft);
            }
        }

        Assert.AreEqual(5_000L, bounds[^1].EndMilliseconds);
        Assert.AreEqual(100d, scale.TimeToPixels(bounds[^1].EndMilliseconds), 0.001);
        Assert.AreEqual(5_000L, scale.PixelsToTime(100));
        CollectionAssert.AreEqual(
            new long[] { 0, 5_000 },
            scale.GetVisibleRulerTicks(0, 100, bounds[^1].EndMilliseconds)
                .Select(tick => tick.Milliseconds)
                .ToArray());
    }

    [TestMethod]
    public void TrimHitTest_UsesInclusiveEightDipEdgesAndKeepsShortCardBodyReachable()
    {
        Assert.AreEqual(TimelineCardHit.Start, TimelineCardHitTest.Resolve(8, 40, 8));
        Assert.AreEqual(TimelineCardHit.Body, TimelineCardHitTest.Resolve(8.001, 40, 8));
        Assert.AreEqual(TimelineCardHit.Body, TimelineCardHitTest.Resolve(31.999, 40, 8));
        Assert.AreEqual(TimelineCardHit.End, TimelineCardHitTest.Resolve(32, 40, 8));

        Assert.AreEqual(TimelineCardHit.Start, TimelineCardHitTest.Resolve(7.5, 16, 8));
        Assert.AreEqual(TimelineCardHit.Body, TimelineCardHitTest.Resolve(8, 16, 8));
        Assert.AreEqual(TimelineCardHit.End, TimelineCardHitTest.Resolve(8.5, 16, 8));

        Assert.AreEqual(TimelineCardHit.Start, TimelineCardHitTest.Resolve(8, 18, 8));
        Assert.AreEqual(TimelineCardHit.Body, TimelineCardHitTest.Resolve(9, 18, 8));
        Assert.AreEqual(TimelineCardHit.End, TimelineCardHitTest.Resolve(10, 18, 8));
    }

    [TestMethod]
    public void VideoReorderTarget_SkipsDraggedClipAndUsesRemainingMidpointTies()
    {
        var bounds = TimelineLayoutProjection.GetVideoBounds(TestProjects.WithVideo(1_000, 2_000, 3_000));

        Assert.AreEqual(0, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[0].ItemId, 1_999));
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[0].ItemId, 2_000));
        Assert.AreEqual(2, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[0].ItemId, 4_500));

        Assert.AreEqual(0, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[1].ItemId, 499));
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[1].ItemId, 500));
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[1].ItemId, 4_499));
        Assert.AreEqual(2, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[1].ItemId, 4_500));

        Assert.AreEqual(0, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[2].ItemId, 499));
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[2].ItemId, 500));
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[2].ItemId, 1_999));
        Assert.AreEqual(2, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[2].ItemId, 2_000));
    }

    [TestMethod]
    public void VideoReorderTarget_ScrolledContentCoordinateKeepsPointerTime()
    {
        var bounds = TimelineLayoutProjection.GetVideoBounds(TestProjects.WithVideo(5_000, 5_000, 5_000));
        var scale = new TimelineScale(100);
        const double horizontalOffset = 900;
        const double pointerViewportX = 200;

        var pointerTime = scale.PixelsToTime(horizontalOffset + pointerViewportX);

        Assert.AreEqual(11_000L, pointerTime);
        Assert.AreEqual(1, TimelineLayoutProjection.GetVideoTargetIndex(bounds, bounds[1].ItemId, pointerTime));
    }

    [TestMethod]
    public void OverlappingAudioSelection_CyclesEveryFullyCoveredItemInRenderOrder()
    {
        var first = TestProjects.Audio(startMilliseconds: 1_000, sourceOutMilliseconds: 2_000);
        var second = TestProjects.Audio(startMilliseconds: 1_000, sourceOutMilliseconds: 2_000);
        var third = TestProjects.Audio(startMilliseconds: 1_000, sourceOutMilliseconds: 2_000);
        IReadOnlyList<AudioTimelineItem> items = [first, second, third];

        Assert.AreEqual(second.Id, TimelineOverlapSelection.GetPreviousAudioItemId(items, 1_500, third.Id));
        Assert.AreEqual(first.Id, TimelineOverlapSelection.GetPreviousAudioItemId(items, 1_500, second.Id));
        Assert.AreEqual(third.Id, TimelineOverlapSelection.GetPreviousAudioItemId(items, 1_500, first.Id));
        Assert.AreEqual(third.Id, TimelineOverlapSelection.GetPreviousAudioItemId(items, 500, third.Id));
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
    public void ZoomOffset_UsesTrailingContentExtentToPreservePointerTimeNearEnd()
    {
        var offset = TimelineScale.CalculateAnchoredOffset(
            oldPixelsPerSecond: TimelineScale.MinimumPixelsPerSecond,
            newPixelsPerSecond: TimelineScale.MaximumPixelsPerSecond,
            oldHorizontalOffset: 1_727_548,
            pointerViewportX: 450,
            durationMilliseconds: ProjectDocument.MaximumTimelineDurationMilliseconds,
            viewportWidth: 500,
            contentEndPadding: 48);

        Assert.AreEqual(34_559_510d, offset, 0.001);
        Assert.AreEqual(
            86_399_900d,
            (offset + 450) * 1_000d / TimelineScale.MaximumPixelsPerSecond,
            0.001);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(500L)]
    public void ZoomOffset_UsesOneSecondMinimumForEmptyAndSubSecondProjects(long durationMilliseconds)
    {
        var offset = TimelineScale.CalculateAnchoredOffset(
            oldPixelsPerSecond: 80,
            newPixelsPerSecond: TimelineScale.MaximumPixelsPerSecond,
            oldHorizontalOffset: 28,
            pointerViewportX: 50,
            durationMilliseconds,
            viewportWidth: 100,
            contentEndPadding: 48);

        Assert.AreEqual(340d, offset, 0.001);
        Assert.AreEqual(975d, (offset + 50) * 1_000d / TimelineScale.MaximumPixelsPerSecond, 0.001);
    }

    [TestMethod]
    public void WheelZoom_UsesWheelDirectionAndScaleBounds()
    {
        Assert.AreEqual(112d, TimelineScale.CalculateWheelZoomTarget(100, 120), 0.001);
        Assert.AreEqual(100d / 1.12d, TimelineScale.CalculateWheelZoomTarget(100, -120), 0.001);
        Assert.AreEqual(100d * Math.Sqrt(1.12d), TimelineScale.CalculateWheelZoomTarget(100, 60), 0.001);
        Assert.AreEqual(100d / Math.Sqrt(1.12d), TimelineScale.CalculateWheelZoomTarget(100, -60), 0.001);
        Assert.AreEqual(100d * 1.12d * 1.12d, TimelineScale.CalculateWheelZoomTarget(100, 240), 0.001);
        Assert.AreEqual(TimelineScale.MaximumPixelsPerSecond, TimelineScale.CalculateWheelZoomTarget(399, 120), 0.001);
        Assert.AreEqual(TimelineScale.MinimumPixelsPerSecond, TimelineScale.CalculateWheelZoomTarget(21, -120), 0.001);
        Assert.AreEqual(100d, TimelineScale.CalculateWheelZoomTarget(100, 0), 0.001);
    }

    [TestMethod]
    public void WheelZoom_RepeatedInAndOutCyclesDoNotDriftFromThePointerTime()
    {
        const long durationMilliseconds = 100_000;
        const double viewportWidth = 500;
        const double pointerViewportX = 250;
        var zoom = 80d;
        var offset = 1_000d;
        var initialPointerTime = (offset + pointerViewportX) / zoom;

        for (var cycle = 0; cycle < 1_000; cycle++)
        {
            var nextZoom = TimelineScale.CalculateWheelZoomTarget(zoom, 120);
            offset = TimelineScale.CalculateAnchoredOffset(
                zoom, nextZoom, offset, pointerViewportX, durationMilliseconds, viewportWidth, contentEndPadding: 48);
            zoom = nextZoom;

            nextZoom = TimelineScale.CalculateWheelZoomTarget(zoom, -120);
            offset = TimelineScale.CalculateAnchoredOffset(
                zoom, nextZoom, offset, pointerViewportX, durationMilliseconds, viewportWidth, contentEndPadding: 48);
            zoom = nextZoom;
        }

        Assert.AreEqual(80d, zoom, 1e-12);
        Assert.AreEqual(initialPointerTime, (offset + pointerViewportX) / zoom, 1e-9);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void AudioTrimEnd_ExtremePointerValueSaturatesInsteadOfWrapping()
    {
        var project = ProjectDocument.CreateNew("Extreme audio trim", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            DurationMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds
        };
        var item = new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            StartMilliseconds = 0,
            SourceInMilliseconds = 1_000,
            SourceOutMilliseconds = 2_000
        };
        project.Assets.Add(asset);
        project.AudioItems.Add(item);
        var sourceOut = TimelineDragPreview.CalculateAudioTrimEndSource(
            item.SourceOutMilliseconds,
            boundaryMilliseconds: long.MaxValue,
            originalBoundaryMilliseconds: item.DurationMilliseconds);
        var viewModel = new EditorViewModel(project);

        Assert.AreEqual(long.MaxValue, sourceOut);
        Assert.IsTrue(viewModel.TrimAudioEnd(item.Id, sourceOut));
        Assert.AreEqual(
            ProjectDocument.MaximumTimelineDurationMilliseconds,
            viewModel.Project.AudioItems.Single().SourceOutMilliseconds);
        Assert.IsTrue(TimelineEditingService.IsWithinProjectDurationLimit(viewModel.Project));
    }

    [TestMethod]
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

    [TestMethod]
    public void ContextMenuState_ResolvesCurrentItemLockPlayheadAndSourceByGuid()
    {
        var project = TestProjects.WithVideo(1_000);
        var video = project.VideoItems.Single();
        project.Assets.Single().SourcePath = @"C:\Media\clip.mp4";
        var text = TestProjects.Text(0, 1_000);
        project.TextItems.Add(text);
        var locks = new TimelineTrackLocks();

        var edge = TimelineContextCommands.Resolve(project, video.Id, locks, 50);
        Assert.IsFalse(edge.CanSplit);
        Assert.IsTrue(edge.CanDuplicate);
        Assert.IsTrue(edge.CanDelete);
        Assert.IsTrue(edge.CanShowSourceFile);

        var interior = TimelineContextCommands.Resolve(project, video.Id, locks, 500);
        Assert.IsTrue(interior.CanSplit);

        locks.SetLocked(TimelineTrackKind.Video, true);
        var locked = TimelineContextCommands.Resolve(project, video.Id, locks, 500);
        Assert.IsFalse(locked.CanSplit);
        Assert.IsFalse(locked.CanDuplicate);
        Assert.IsFalse(locked.CanDelete);
        Assert.IsTrue(locked.CanShowSourceFile);

        var textState = TimelineContextCommands.Resolve(project, text.Id, locks, 500);
        Assert.IsFalse(textState.CanSplit);
        Assert.IsTrue(textState.CanDuplicate);
        Assert.IsTrue(textState.CanDelete);
        Assert.IsFalse(textState.CanShowSourceFile);

        Assert.AreEqual(default, TimelineContextCommands.Resolve(project, Guid.NewGuid(), locks, 500));
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
    public void AudioTrims_ClampCompatibilityFadesToTrimmedDuration()
    {
        var project = CreateProjectWithAudio();
        var itemId = project.AudioItems.Single().Id;
        project.AudioItems.Single().FadeInMilliseconds = 2_400;
        project.AudioItems.Single().FadeOutMilliseconds = 2_000;
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.TrimAudioStart(itemId, 2_500));
        var trimmed = viewModel.Project.AudioItems.Single();
        Assert.AreEqual(500L, trimmed.DurationMilliseconds);
        Assert.AreEqual(500L, trimmed.FadeInMilliseconds);
        Assert.AreEqual(500L, trimmed.FadeOutMilliseconds);

        Assert.IsTrue(viewModel.TrimAudioEnd(itemId, 2_700));
        trimmed = viewModel.Project.AudioItems.Single();
        Assert.AreEqual(200L, trimmed.DurationMilliseconds);
        Assert.AreEqual(200L, trimmed.FadeInMilliseconds);
        Assert.AreEqual(200L, trimmed.FadeOutMilliseconds);

        viewModel.Select(new EditorSelection(EditorSelectionKind.AudioItem, itemId));
        Assert.IsTrue(viewModel.DuplicateSelection());
        var duplicate = viewModel.Project.AudioItems.Single(candidate => candidate.Id != itemId);
        Assert.AreEqual(200L, duplicate.DurationMilliseconds);
        Assert.AreEqual(200L, duplicate.FadeInMilliseconds);
        Assert.AreEqual(200L, duplicate.FadeOutMilliseconds);
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

    [TestMethod]
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
    public void NoOpReorder_PreservesProjectItemsSourceRangesAndHistory()
    {
        var project = TestProjects.WithVideo(1_000, 2_000, 3_000);
        project.VideoItems[1].SourceInMilliseconds = 250;
        project.VideoItems[1].SourceOutMilliseconds = 1_750;
        project.VideoItems[1].DurationMilliseconds = 1_500;
        var viewModel = new EditorViewModel(project);
        var originalProject = viewModel.Project;
        var originalItems = viewModel.Project.VideoItems
            .Select(item => (item.Id, item.AssetId, item.SourceInMilliseconds, item.SourceOutMilliseconds, item.DurationMilliseconds))
            .ToArray();
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsFalse(viewModel.ReorderVideoItem(project.VideoItems[1].Id, 1));

        Assert.AreSame(originalProject, viewModel.Project);
        CollectionAssert.AreEqual(originalItems, viewModel.Project.VideoItems
            .Select(item => (item.Id, item.AssetId, item.SourceInMilliseconds, item.SourceOutMilliseconds, item.DurationMilliseconds))
            .ToArray());
        Assert.AreEqual(0L, viewModel.Revision);
        Assert.AreEqual(0, committed);
        Assert.IsFalse(viewModel.CanUndo);
        Assert.IsFalse(viewModel.CanRedo);
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
