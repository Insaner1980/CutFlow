using CutFlow.Models;
using CutFlow.Controls;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class PreviewTimingTests
{
    [TestMethod]
    [DataRow(1920, 1080, 1280, 720)]
    [DataRow(1080, 1080, 720, 720)]
    [DataRow(1080, 1920, 396, 704)]
    [DataRow(1001, 777, 926, 720)]
    [DataRow(1919, 1080, 1278, 720)]
    [DataRow(640, 360, 640, 360)]
    [DataRow(3, 5, 2, 4)]
    [DataRow(2, 2, 2, 2)]
    public void PreviewStreamSize_FitsAspectInsideSingle1280By720Envelope(
        int sourceWidth,
        int sourceHeight,
        int expectedWidth,
        int expectedHeight)
    {
        var size = PreviewStreamSize.Fit(sourceWidth, sourceHeight, 1280, 720);

        Assert.AreEqual(expectedWidth, size.Width);
        Assert.AreEqual(expectedHeight, size.Height);
        Assert.IsLessThanOrEqualTo(sourceWidth, size.Width);
        Assert.IsLessThanOrEqualTo(sourceHeight, size.Height);
        Assert.IsLessThanOrEqualTo(1280, size.Width);
        Assert.IsLessThanOrEqualTo(720, size.Height);
        Assert.AreEqual(0, size.Width % 2);
        Assert.AreEqual(0, size.Height % 2);
        Assert.AreEqual(
            sourceWidth / (double)sourceHeight,
            size.Width / (double)size.Height,
            2d / size.Height);
    }

    [TestMethod]
    [DataRow(1920, 1080)]
    [DataRow(1080, 1920)]
    [DataRow(1080, 1080)]
    public void PreviewStreamSize_PreservesPresetAspectRatioExactly(int sourceWidth, int sourceHeight)
    {
        var size = PreviewStreamSize.Fit(sourceWidth, sourceHeight, 1280, 720);

        Assert.AreEqual((long)sourceWidth * size.Height, (long)sourceHeight * size.Width);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 1)]
    public void PreviewStreamSize_RejectsCanvasTooSmallForAlignedOutput(int sourceWidth, int sourceHeight)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PreviewStreamSize.Fit(sourceWidth, sourceHeight, 1280, 720));
    }

    [TestMethod]
    public void TimelineMath_EndSaturatesAndUsesAnExclusiveActiveEnd()
    {
        Assert.AreEqual(long.MaxValue, TimelineMath.End(long.MaxValue - 50, 100));
        Assert.IsTrue(TimelineMath.IsActiveAt(long.MaxValue - 1, long.MaxValue - 50, 100));
        Assert.IsFalse(TimelineMath.IsActiveAt(long.MaxValue, long.MaxValue - 50, 100));
    }

    [TestMethod]
    public void TimelinePlaybackMath_SaturatingStepAtMaximumDoesNotWrapToStart()
    {
        Assert.AreEqual(
            long.MaxValue,
            TimelinePlaybackMath.StepPosition(long.MaxValue - 50, 100, long.MaxValue));
    }

    [TestMethod]
    public void EventGenerationGate_QueuedOldCallbackCannotRunAfterSourceAdvances()
    {
        var gate = new EventGenerationGate();
        var firstSource = gate.Advance();
        var calls = 0;

        Assert.IsTrue(gate.TryRun(firstSource, () => calls++));
        var secondSource = gate.Advance();
        Assert.IsFalse(gate.TryRun(firstSource, () => calls++));
        Assert.IsTrue(gate.TryRun(secondSource, () => calls++));

        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void FailedSourceSwapCleanup_InvalidatesBeforeNativeCleanupAndReattachesLast()
    {
        var calls = new List<string>();

        PreviewPane.RunFailedSourceSwapCleanup(
            () => calls.Add("invalidate-generation"),
            () => calls.Add("detach-events"),
            () => calls.Add("pause"),
            () => calls.Add("clear-source-and-state"),
            () => calls.Add("dispose-source"),
            () => calls.Add("attach-events"));

        CollectionAssert.AreEqual(
            new[]
            {
                "invalidate-generation",
                "detach-events",
                "pause",
                "clear-source-and-state",
                "dispose-source",
                "attach-events"
            },
            calls);

        calls.Clear();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            PreviewPane.RunFailedSourceSwapCleanup(
                () => calls.Add("invalidate-generation"),
                () => calls.Add("detach-events"),
                () =>
                {
                    calls.Add("pause");
                    throw new InvalidOperationException();
                },
                () => calls.Add("clear-source-and-state"),
                () => calls.Add("dispose-source"),
                () => calls.Add("attach-events")));
        CollectionAssert.AreEqual(
            new[]
            {
                "invalidate-generation",
                "detach-events",
                "pause",
                "clear-source-and-state",
                "dispose-source",
                "attach-events"
            },
            calls);
    }

    [TestMethod]
    public void SuccessfulSourceSwap_StabilizesNewStateBeforeAttachingCurrentGenerationEvents()
    {
        var calls = new List<string>();

        PreviewPane.RunSuccessfulSourceSwap(
            () => calls.Add("invalidate-generation"),
            () => calls.Add("detach-events"),
            () => calls.Add("pause"),
            () => calls.Add("clear-source-and-state"),
            () => calls.Add("dispose-old-source"),
            () => calls.Add("install-new-source-and-state"),
            () => calls.Add("restore-position-and-intent"),
            () => calls.Add("attach-events"),
            () => calls.Add("resume-playback"));

        CollectionAssert.AreEqual(
            new[]
            {
                "invalidate-generation",
                "detach-events",
                "pause",
                "clear-source-and-state",
                "dispose-old-source",
                "install-new-source-and-state",
                "restore-position-and-intent",
                "attach-events",
                "resume-playback"
            },
            calls);
    }

    [TestMethod]
    public void PlaybackStateCoordinator_IntentChangesDoNotReportUntilActualStateArrives()
    {
        var state = new PreviewPlaybackStateCoordinator();
        var reports = new List<bool>();

        Assert.IsTrue(state.ToggleIntent());
        Assert.HasCount(0, reports);

        Assert.IsTrue(state.ReportActual(isPlaying: true, reports.Add));
        CollectionAssert.AreEqual(new[] { true }, reports);

        Assert.IsFalse(state.ToggleIntent());
        CollectionAssert.AreEqual(new[] { true }, reports);

        Assert.IsTrue(state.ReportActual(isPlaying: false, reports.Add));
        CollectionAssert.AreEqual(new[] { true, false }, reports);
        Assert.IsFalse(state.ReportActual(isPlaying: false, reports.Add));
    }

    [TestMethod]
    public void PlaybackStateCoordinator_DelayedNativeStateCannotOverrideNewerIntent()
    {
        var state = new PreviewPlaybackStateCoordinator();
        var reports = new List<bool>();

        state.SetIntent(false);
        Assert.IsTrue(state.ReportActual(isPlaying: false, reports.Add));

        state.SetIntent(true);
        state.SetIntent(false);
        Assert.IsFalse(state.ReportActual(isPlaying: true, reports.Add));

        state.SetIntent(true);
        Assert.IsTrue(state.ReportActual(isPlaying: true, reports.Add));

        state.SetIntent(false);
        state.SetIntent(true);
        Assert.IsFalse(state.ReportActual(isPlaying: false, reports.Add));

        CollectionAssert.AreEqual(new[] { false, true }, reports);
    }

    [TestMethod]
    [DataRow(9_000L, 4_000L, false, 4_000L)]
    [DataRow(9_000L, 4_000L, true, 0L)]
    [DataRow(3_000L, 4_000L, true, 3_000L)]
    [DataRow(-1L, 4_000L, true, 0L)]
    public void RebuildStartPosition_ClampsAndRestartsOnlyWhenPlaybackIsIntendedAtEnd(
        long positionMilliseconds,
        long durationMilliseconds,
        bool playIntent,
        long expected)
    {
        Assert.AreEqual(
            expected,
            PreviewPlaybackPolicy.GetRebuildStartPosition(
                positionMilliseconds,
                durationMilliseconds,
                playIntent));
    }

    [TestMethod]
    [DataRow(4_000d, 4_000d, true)]
    [DataRow(4_000.4d, 4_000d, true)]
    [DataRow(3_999.51d, 4_000d, true)]
    [DataRow(3_999.49d, 4_000d, false)]
    [DataRow(3_999d, 4_000d, false)]
    [DataRow(3_966.667d, 4_000d, false)]
    [DataRow(4_000d, 3_000d, true)]
    [DataRow(4_000d, 5_000d, false)]
    [DataRow(0d, 0d, false)]
    [DataRow(1_000d, 0d, false)]
    public void PlayStart_RestartsOnlyAtTheRoundedPositiveDuration(
        double positionMilliseconds,
        double durationMilliseconds,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            PreviewPlaybackPolicy.ShouldRestartForPlay(
                TimeSpan.FromMilliseconds(positionMilliseconds),
                TimeSpan.FromMilliseconds(durationMilliseconds)));
    }

    [TestMethod]
    public void LiveTextRenderKey_IsStableInsideSameActiveIntervalAndChangesAtBoundaryOrOnContentEdit()
    {
        var project = ProjectDocument.CreateNew("Text key", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            StartMilliseconds = 500,
            DurationMilliseconds = 1_000,
            Text = "Original"
        };
        project.TextItems.Add(item);
        var selection = new EditorSelection(EditorSelectionKind.TextItem, item.Id);

        var firstTick = LiveTextRenderKey.Create(project, selection, 1_000, 1280, 720);
        var nextTick = LiveTextRenderKey.Create(project, selection, 1_033, 1280, 720);
        var inactive = LiveTextRenderKey.Create(project, selection, 1_500, 1280, 720);
        item.Text = "Changed";
        var edited = LiveTextRenderKey.Create(project, selection, 1_033, 1280, 720);

        Assert.AreEqual(firstTick, nextTick);
        Assert.AreNotEqual(firstTick, inactive);
        Assert.AreNotEqual(nextTick, edited);
    }

    [TestMethod]
    public void LiveTextRenderGate_DefersChangedTreeUntilPointerCaptureEnds()
    {
        var project = ProjectDocument.CreateNew("Drag", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem { Id = Guid.NewGuid(), DurationMilliseconds = 2_000, Text = "Before" };
        project.TextItems.Add(item);
        var selection = new EditorSelection(EditorSelectionKind.TextItem, item.Id);
        var gate = new LiveTextRenderGate();
        var initial = LiveTextRenderKey.Create(project, selection, 100, 1280, 720);

        Assert.IsTrue(gate.ShouldRender(initial, hasPointerCapture: false));
        Assert.IsFalse(gate.ShouldRender(initial, hasPointerCapture: false));

        item.Text = "During drag";
        var changed = LiveTextRenderKey.Create(project, selection, 133, 1280, 720);
        Assert.IsFalse(gate.ShouldRender(changed, hasPointerCapture: true));
        Assert.IsTrue(gate.ShouldRender(changed, hasPointerCapture: false));
    }

    [TestMethod]
    public void LiveTextRenderGate_DirtyDraggedVisualForcesCanonicalRerenderWithSameKey()
    {
        var project = ProjectDocument.CreateNew("Clamped drag", DateTimeOffset.UnixEpoch);
        var item = new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            DurationMilliseconds = 2_000,
            NormalizedX = 0
        };
        project.TextItems.Add(item);
        var key = LiveTextRenderKey.Create(
            project,
            new EditorSelection(EditorSelectionKind.TextItem, item.Id),
            100,
            1280,
            720);
        var gate = new LiveTextRenderGate();

        Assert.IsTrue(gate.ShouldRender(key, hasPointerCapture: false));
        gate.MarkVisualDirty();

        Assert.IsTrue(gate.ShouldRender(key, hasPointerCapture: false));
    }
}
