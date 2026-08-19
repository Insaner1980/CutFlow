using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class TimelineEditingTests
{
    [TestMethod]
    [DataRow(0L, false)]
    [DataRow(99L, false)]
    [DataRow(100L, true)]
    [DataRow(101L, true)]
    public void ProjectBounds_EnforcesMinimumDurationBoundary(long durationMilliseconds, bool expected)
    {
        Assert.AreEqual(expected, TimelineMath.IsWithinProjectBounds(0, durationMilliseconds));
    }

    [TestMethod]
    public void ProjectBounds_AcceptsExactTwentyFourHourEndAndRejectsOneMillisecondBeyond()
    {
        Assert.IsTrue(TimelineMath.IsWithinProjectBounds(
            0,
            ProjectDocument.MaximumTimelineDurationMilliseconds));
        Assert.IsTrue(TimelineMath.IsWithinProjectBounds(
            ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds,
            ProjectDocument.MinimumItemDurationMilliseconds));
        Assert.IsFalse(TimelineMath.IsWithinProjectBounds(
            ProjectDocument.MaximumTimelineDurationMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds + 1,
            ProjectDocument.MinimumItemDurationMilliseconds));
    }

    [TestMethod]
    public void CalculateProjectDuration_UsesTheLatestEndAcrossAllTracks()
    {
        var project = TestProjects.WithVideo(2_000, 3_000);
        project.AudioItems.Add(TestProjects.Audio(startMilliseconds: 4_000, sourceOutMilliseconds: 1_500));
        project.TextItems.Add(TestProjects.Text(startMilliseconds: 7_000, durationMilliseconds: 750));

        var duration = TimelineEditingService.CalculateProjectDuration(project);

        Assert.AreEqual(7_750L, duration);
    }

    [TestMethod]
    public void ReorderVideoItem_MovesItemAndKeepsMagneticOrder()
    {
        var project = TestProjects.WithVideo(1_000, 2_000, 3_000);
        var moved = project.VideoItems[0];
        var expectedFirst = project.VideoItems[1].Id;
        var expectedSecond = project.VideoItems[2].Id;

        var changed = TimelineEditingService.ReorderVideoItem(project, moved.Id, 2);

        Assert.IsTrue(changed);
        Assert.AreSequenceEqual(
            new[] { expectedFirst, expectedSecond, moved.Id }, project.VideoItems.Select(item => item.Id).ToArray());
        Assert.AreEqual(6_000L, TimelineEditingService.CalculateProjectDuration(project));
    }

    [TestMethod]
    public void ReorderVideoItem_ClampsTargetIndexToLastItem()
    {
        var project = TestProjects.WithVideo(1_000, 2_000);
        var moved = project.VideoItems[0];

        var changed = TimelineEditingService.ReorderVideoItem(project, moved.Id, 99);

        Assert.IsTrue(changed);
        Assert.AreEqual(moved.Id, project.VideoItems[1].Id);
    }

    [TestMethod]
    public void SplitVideoItem_UsesTimelinePositionAndCreatesAdjacentSourceRanges()
    {
        var project = TestProjects.WithVideo(4_000, 5_000);
        var original = project.VideoItems[1];

        var changed = TimelineEditingService.SplitVideoItem(project, original.Id, 6_000);

        Assert.IsTrue(changed);
        Assert.HasCount(3, project.VideoItems);
        Assert.AreEqual(0L, project.VideoItems[1].SourceInMilliseconds);
        Assert.AreEqual(2_000L, project.VideoItems[1].SourceOutMilliseconds);
        Assert.AreEqual(2_000L, project.VideoItems[2].SourceInMilliseconds);
        Assert.AreEqual(5_000L, project.VideoItems[2].SourceOutMilliseconds);
        Assert.AreNotEqual(project.VideoItems[1].Id, project.VideoItems[2].Id);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(4_000L)]
    [DataRow(3_950L)]
    [DataRow(50L)]
    public void SplitVideoItem_RejectsClipBoundariesAndSubMinimumSegments(long timelinePositionMilliseconds)
    {
        var project = TestProjects.WithVideo(4_000);
        var original = project.VideoItems[0];

        var changed = TimelineEditingService.SplitVideoItem(project, original.Id, timelinePositionMilliseconds);

        Assert.IsFalse(changed);
        Assert.HasCount(1, project.VideoItems);
        Assert.AreEqual(original.SourceInMilliseconds, project.VideoItems[0].SourceInMilliseconds);
        Assert.AreEqual(original.SourceOutMilliseconds, project.VideoItems[0].SourceOutMilliseconds);
    }

    [TestMethod]
    public void TrimVideoStart_ClampsToSourceStartAndMinimumDuration()
    {
        var project = TestProjects.WithVideo(1_000);
        var item = project.VideoItems[0];
        item.SourceInMilliseconds = 200;

        Assert.IsTrue(TimelineEditingService.TrimVideoStart(project, item.Id, -500));
        Assert.AreEqual(0L, item.SourceInMilliseconds);

        Assert.IsTrue(TimelineEditingService.TrimVideoStart(project, item.Id, 999));
        Assert.AreEqual(900L, item.SourceInMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, item.DurationMilliseconds);
    }

    [TestMethod]
    public void TrimVideoEnd_ClampsToKnownAssetDurationAndMinimumDuration()
    {
        var project = TestProjects.WithVideo(1_000);
        var item = project.VideoItems[0];
        item.SourceOutMilliseconds = 900;

        Assert.IsTrue(TimelineEditingService.TrimVideoEnd(project, item.Id, 2_000));
        Assert.AreEqual(1_000L, item.SourceOutMilliseconds);

        Assert.IsTrue(TimelineEditingService.TrimVideoEnd(project, item.Id, 50));
        Assert.AreEqual(100L, item.SourceOutMilliseconds);
        Assert.AreEqual(ProjectDocument.MinimumItemDurationMilliseconds, item.DurationMilliseconds);
    }

    [TestMethod]
    public void TrimVideoEnd_CurrentAssetEndRepairsDurationAndRipplesLaterItems()
    {
        var project = TestProjects.WithVideo(5_000, 1_000);
        var item = project.VideoItems[0];
        item.SourceInMilliseconds = 500;
        item.DurationMilliseconds = 1_000;

        var changed = TimelineEditingService.TrimVideoEnd(project, item.Id, 5_000);

        Assert.IsTrue(changed);
        Assert.AreEqual(500L, item.SourceInMilliseconds);
        Assert.AreEqual(5_000L, item.SourceOutMilliseconds);
        Assert.AreEqual(4_500L, item.DurationMilliseconds);
        Assert.AreEqual(4_500L, TimelineLayoutProjection.GetVideoBounds(project)[1].StartMilliseconds);
    }

    [TestMethod]
    public void MoveAudioItem_ClampsNegativeStartToZero()
    {
        var project = new ProjectDocument();
        var audio = TestProjects.Audio(startMilliseconds: 500, sourceOutMilliseconds: 1_000);
        project.AudioItems.Add(audio);

        var changed = TimelineEditingService.MoveAudioItem(project, audio.Id, -250);

        Assert.IsTrue(changed);
        Assert.AreEqual(0L, audio.StartMilliseconds);
    }

    [TestMethod]
    public void MoveTextItem_ClampsNegativeStartToZero()
    {
        var project = new ProjectDocument();
        var text = TestProjects.Text(startMilliseconds: 500, durationMilliseconds: 1_000);
        project.TextItems.Add(text);

        var changed = TimelineEditingService.MoveTextItem(project, text.Id, -250);

        Assert.IsTrue(changed);
        Assert.AreEqual(0L, text.StartMilliseconds);
    }

    [TestMethod]
    public void DeleteSelection_RipplesVideoItemsAndRejectsNoSelection()
    {
        var project = TestProjects.WithVideo(1_000, 2_000, 3_000);
        var deleted = project.VideoItems[1];

        Assert.IsTrue(TimelineEditingService.DeleteSelection(
            project,
            new EditorSelection(EditorSelectionKind.VideoItem, deleted.Id)));
        Assert.HasCount(2, project.VideoItems);
        Assert.AreEqual(4_000L, TimelineEditingService.CalculateProjectDuration(project));
        Assert.IsFalse(TimelineEditingService.DeleteSelection(project, EditorSelection.None));
    }

    [TestMethod]
    [DataRow(EditorSelectionKind.VideoItem)]
    [DataRow(EditorSelectionKind.AudioItem)]
    [DataRow(EditorSelectionKind.TextItem)]
    public void DuplicateSelection_CopiesSupportedItemWithFreshIdAndSelectsCopy(EditorSelectionKind kind)
    {
        var project = TestProjects.WithVideo(1_000);
        var selection = TestProjects.AddSelectedItem(project, kind);

        var changed = TimelineEditingService.DuplicateSelection(project, selection, out var duplicatedSelection);

        Assert.IsTrue(changed);
        Assert.AreEqual(kind, duplicatedSelection.Kind);
        Assert.IsNotNull(duplicatedSelection.ItemId);
        Assert.AreNotEqual(selection.ItemId, duplicatedSelection.ItemId);
        Assert.AreEqual(2, TestProjects.CountItems(project, kind));
        Assert.IsFalse(TimelineEditingService.DuplicateSelection(project, EditorSelection.None, out _));
    }

    [TestMethod]
    public void DuplicateSelection_AudioStartsAtSourceEndPreservesFieldsAndAllowsOverlap()
    {
        var project = new ProjectDocument();
        var source = new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            StartMilliseconds = 2_000,
            SourceInMilliseconds = 500,
            SourceOutMilliseconds = 2_000,
            Volume = 0.35,
            FadeInMilliseconds = 125,
            FadeOutMilliseconds = 250,
            IsMuted = true
        };
        project.AudioItems.AddRange(
        [
            source,
            TestProjects.Audio(startMilliseconds: 3_000, sourceOutMilliseconds: 2_000)
        ]);

        Assert.IsTrue(TimelineEditingService.DuplicateSelection(
            project,
            new EditorSelection(EditorSelectionKind.AudioItem, source.Id),
            out var duplicatedSelection));

        var duplicate = project.AudioItems.Single(item => item.Id == duplicatedSelection.ItemId);
        Assert.AreNotEqual(source.Id, duplicate.Id);
        Assert.AreEqual(3_500L, duplicate.StartMilliseconds);
        Assert.AreEqual(source.AssetId, duplicate.AssetId);
        Assert.AreEqual(source.SourceInMilliseconds, duplicate.SourceInMilliseconds);
        Assert.AreEqual(source.SourceOutMilliseconds, duplicate.SourceOutMilliseconds);
        Assert.AreEqual(source.Volume, duplicate.Volume);
        Assert.AreEqual(source.IsMuted, duplicate.IsMuted);
        Assert.AreEqual(source.FadeInMilliseconds, duplicate.FadeInMilliseconds);
        Assert.AreEqual(source.FadeOutMilliseconds, duplicate.FadeOutMilliseconds);
    }

    [TestMethod]
    public void EditingOperations_ReturnFalseForUnknownItemIds()
    {
        var project = TestProjects.WithVideo(1_000);

        Assert.IsFalse(TimelineEditingService.ReorderVideoItem(project, Guid.NewGuid(), 0));
        Assert.IsFalse(TimelineEditingService.SplitVideoItem(project, Guid.NewGuid(), 500));
        Assert.IsFalse(TimelineEditingService.TrimVideoStart(project, Guid.NewGuid(), 100));
        Assert.IsFalse(TimelineEditingService.TrimVideoEnd(project, Guid.NewGuid(), 900));
        Assert.IsFalse(TimelineEditingService.MoveAudioItem(project, Guid.NewGuid(), 0));
        Assert.IsFalse(TimelineEditingService.MoveTextItem(project, Guid.NewGuid(), 0));
    }
}

internal static class TestProjects
{
    public static ProjectDocument WithVideo(params long[] durations)
    {
        var project = new ProjectDocument();
        foreach (var duration in durations)
        {
            var assetId = Guid.NewGuid();
            project.Assets.Add(new ProjectAsset
            {
                Id = assetId,
                Kind = ProjectAssetKind.Video,
                DurationMilliseconds = duration
            });
            project.VideoItems.Add(new VideoTimelineItem
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                SourceOutMilliseconds = duration
            });
        }

        return project;
    }

    public static AudioTimelineItem Audio(long startMilliseconds, long sourceOutMilliseconds) => new()
    {
        Id = Guid.NewGuid(),
        AssetId = Guid.NewGuid(),
        StartMilliseconds = startMilliseconds,
        SourceOutMilliseconds = sourceOutMilliseconds
    };

    public static TextTimelineItem Text(long startMilliseconds, long durationMilliseconds) => new()
    {
        Id = Guid.NewGuid(),
        StartMilliseconds = startMilliseconds,
        DurationMilliseconds = durationMilliseconds
    };

    public static EditorSelection AddSelectedItem(ProjectDocument project, EditorSelectionKind kind)
    {
        return kind switch
        {
            EditorSelectionKind.VideoItem => new EditorSelection(kind, project.VideoItems[0].Id),
            EditorSelectionKind.AudioItem => AddAudio(project),
            EditorSelectionKind.TextItem => AddText(project),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static int CountItems(ProjectDocument project, EditorSelectionKind kind) => kind switch
    {
        EditorSelectionKind.VideoItem => project.VideoItems.Count,
        EditorSelectionKind.AudioItem => project.AudioItems.Count,
        EditorSelectionKind.TextItem => project.TextItems.Count,
        _ => 0
    };

    private static EditorSelection AddAudio(ProjectDocument project)
    {
        var item = Audio(1_000, 1_000);
        project.AudioItems.Add(item);
        return new EditorSelection(EditorSelectionKind.AudioItem, item.Id);
    }

    private static EditorSelection AddText(ProjectDocument project)
    {
        var item = Text(1_000, 1_000);
        project.TextItems.Add(item);
        return new EditorSelection(EditorSelectionKind.TextItem, item.Id);
    }
}
