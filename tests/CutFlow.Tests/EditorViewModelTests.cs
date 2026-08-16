using CutFlow.Models;
using CutFlow.Services;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class EditorViewModelTests
{
    [TestMethod]
    public void AddAssetToTimeline_AppendsVisualsToV1AndSelectsTheNewItem()
    {
        var project = ProjectDocument.CreateNew("Visuals", DateTimeOffset.UnixEpoch);
        var video = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Video, DurationMilliseconds = 2_000 };
        var image = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        project.Assets.AddRange([video, image]);
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.AddAssetToTimeline(video.Id));
        Assert.IsTrue(viewModel.AddAssetToTimeline(image.Id));

        Assert.HasCount(2, viewModel.Project.VideoItems);
        Assert.AreEqual(video.Id, viewModel.Project.VideoItems[0].AssetId);
        Assert.AreEqual(0L, viewModel.Project.VideoItems[0].SourceInMilliseconds);
        Assert.AreEqual(2_000L, viewModel.Project.VideoItems[0].SourceOutMilliseconds);
        Assert.AreEqual(image.Id, viewModel.Project.VideoItems[1].AssetId);
        Assert.AreEqual(5_000L, viewModel.Project.VideoItems[1].SourceOutMilliseconds);
        Assert.AreEqual(EditorSelectionKind.VideoItem, viewModel.Selection.Kind);
        Assert.AreEqual(viewModel.Project.VideoItems[1].Id, viewModel.Selection.ItemId);
        Assert.AreEqual(7_000L, Services.TimelineEditingService.CalculateProjectDuration(viewModel.Project));
    }

    [TestMethod]
    public void AddImportedAssetsToTimeline_CommitsOrderedVisualBatchAsOneUndoableEdit()
    {
        var project = ProjectDocument.CreateNew("Dropped visuals", DateTimeOffset.UnixEpoch);
        var existing = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Video, DurationMilliseconds = 1_000 };
        project.Assets.Add(existing);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = existing.Id,
            SourceOutMilliseconds = 1_000,
            DurationMilliseconds = 1_000
        });
        var video = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Video, DurationMilliseconds = 2_000 };
        var image = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        var added = viewModel.AddImportedAssetsToTimeline([video, image], [video.Id, image.Id]);

        CollectionAssert.AreEqual(new[] { true, true }, added.ToArray());
        Assert.AreEqual(1, committed);
        Assert.AreEqual(1L, viewModel.Revision);
        CollectionAssert.AreEqual(
            new[] { existing.Id, video.Id, image.Id },
            viewModel.Project.VideoItems.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(0L, viewModel.Project.VideoItems[1].SourceInMilliseconds);
        Assert.AreEqual(2_000L, viewModel.Project.VideoItems[1].SourceOutMilliseconds);
        Assert.AreEqual(0L, viewModel.Project.VideoItems[2].SourceInMilliseconds);
        Assert.AreEqual(5_000L, viewModel.Project.VideoItems[2].SourceOutMilliseconds);
        Assert.AreEqual(5_000L, viewModel.Project.VideoItems[2].DurationMilliseconds);

        viewModel.Undo();

        Assert.HasCount(1, viewModel.Project.Assets);
        Assert.HasCount(1, viewModel.Project.VideoItems);
        Assert.AreEqual(existing.Id, viewModel.Project.VideoItems.Single().AssetId);
        Assert.IsFalse(viewModel.CanUndo);
    }

    [TestMethod]
    public void AddAssetToTimeline_WhenV1HasLessThanMinimumCapacity_DoesNotCommit()
    {
        var project = ProjectDocument.CreateNew("Full V1", DateTimeOffset.UnixEpoch);
        var existing = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            DurationMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds - 50
        };
        var candidate = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        project.Assets.AddRange([existing, candidate]);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = existing.Id,
            SourceOutMilliseconds = existing.DurationMilliseconds,
            DurationMilliseconds = existing.DurationMilliseconds
        });
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsFalse(viewModel.AddAssetToTimeline(candidate.Id));

        Assert.AreEqual(0, committed);
        Assert.HasCount(1, viewModel.Project.VideoItems);
        Assert.IsFalse(viewModel.CanUndo);
    }

    [TestMethod]
    public void AddAssetToTimeline_PlacesAudioAtPlayheadAndRejectsMissingAsset()
    {
        var project = ProjectDocument.CreateNew("Audio", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), DurationMilliseconds = 2_000 });
        var audio = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Audio, DurationMilliseconds = 3_000 };
        var missing = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Video, DurationMilliseconds = 1_000, IsMissing = true };
        project.Assets.AddRange([audio, missing]);
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;
        viewModel.Seek(1_250);

        Assert.IsTrue(viewModel.AddAssetToTimeline(audio.Id));
        Assert.IsFalse(viewModel.AddAssetToTimeline(missing.Id));

        var item = viewModel.Project.AudioItems.Single();
        Assert.AreNotEqual(Guid.Empty, item.Id);
        Assert.AreNotEqual(audio.Id, item.Id);
        Assert.AreEqual(1_250L, item.StartMilliseconds);
        Assert.AreEqual(0L, item.SourceInMilliseconds);
        Assert.AreEqual(3_000L, item.SourceOutMilliseconds);
        Assert.AreEqual(1d, item.Volume);
        Assert.IsFalse(item.IsMuted);
        Assert.AreEqual(0L, item.FadeInMilliseconds);
        Assert.AreEqual(0L, item.FadeOutMilliseconds);
        Assert.AreEqual(EditorSelectionKind.AudioItem, viewModel.Selection.Kind);
        Assert.AreEqual(item.Id, viewModel.Selection.ItemId);
        Assert.AreEqual(1, committed);

        viewModel.Undo();

        Assert.HasCount(0, viewModel.Project.AudioItems);
        Assert.IsFalse(viewModel.CanUndo);
    }

    [TestMethod]
    public void AddAssetToTimeline_WhenAudioDoesNotFitAtPlayhead_RejectsWithoutTruncatingOrCommitting()
    {
        var project = ProjectDocument.CreateNew("Full A1", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem
        {
            Id = Guid.NewGuid(),
            DurationMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds - 500
        });
        var audio = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Audio,
            DurationMilliseconds = 1_000
        };
        project.Assets.Add(audio);
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;
        viewModel.Seek(ProjectDocument.MaximumTimelineDurationMilliseconds - 500);

        Assert.IsFalse(viewModel.AddAssetToTimeline(audio.Id));

        Assert.HasCount(0, viewModel.Project.AudioItems);
        Assert.AreEqual(0, committed);
        Assert.IsFalse(viewModel.CanUndo);
    }

    [TestMethod]
    public void DuplicateTimelineItem_WhenAudioCopyExceedsProjectEnd_PreservesState()
    {
        var project = ProjectDocument.CreateNew("Full A1", DateTimeOffset.UnixEpoch);
        var selected = new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            SourceOutMilliseconds = 1_000
        };
        var source = new AudioTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = Guid.NewGuid(),
            StartMilliseconds = ProjectDocument.MaximumTimelineDurationMilliseconds - 1_000,
            SourceOutMilliseconds = 1_000
        };
        project.AudioItems.AddRange([selected, source]);
        var viewModel = new EditorViewModel(project);
        Assert.IsTrue(viewModel.SetBackgroundColor("#FF102030"));
        viewModel.Undo();
        Assert.IsTrue(viewModel.CanRedo);
        viewModel.Select(new EditorSelection(EditorSelectionKind.AudioItem, selected.Id));
        viewModel.MarkSaved();
        var revision = viewModel.Revision;
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsFalse(viewModel.DuplicateTimelineItem(source.Id));

        Assert.HasCount(2, viewModel.Project.AudioItems);
        Assert.AreEqual(new EditorSelection(EditorSelectionKind.AudioItem, selected.Id), viewModel.Selection);
        Assert.IsTrue(viewModel.CanRedo);
        Assert.AreEqual(EditorViewModel.SavedStatus, viewModel.SaveStatus);
        Assert.AreEqual(revision, viewModel.Revision);
        Assert.AreEqual(0, committed);
    }

    [TestMethod]
    public void DeleteTimelineItem_UsesCurrentGuidWithoutFallingBackToSelection()
    {
        var project = TestProjects.WithVideo(1_000, 1_000);
        var targetId = project.VideoItems[0].Id;
        var selectedId = project.VideoItems[1].Id;
        var viewModel = new EditorViewModel(project);
        viewModel.Select(new EditorSelection(EditorSelectionKind.VideoItem, selectedId));

        Assert.IsFalse(viewModel.DeleteTimelineItem(Guid.NewGuid()));
        Assert.HasCount(2, viewModel.Project.VideoItems);
        Assert.AreEqual(new EditorSelection(EditorSelectionKind.VideoItem, selectedId), viewModel.Selection);

        Assert.IsTrue(viewModel.DeleteTimelineItem(targetId));
        Assert.HasCount(1, viewModel.Project.VideoItems);
        Assert.AreEqual(selectedId, viewModel.Project.VideoItems.Single().Id);
        Assert.AreEqual(EditorSelection.None, viewModel.Selection);
    }

    [TestMethod]
    public void RemoveAsset_UndoAndRedoKeepLibraryConsistent()
    {
        var project = ProjectDocument.CreateNew("Remove", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Image, DurationMilliseconds = 5_000 };
        project.Assets.Add(asset);
        var viewModel = new EditorViewModel(project);

        Assert.IsTrue(viewModel.RemoveAsset(asset.Id, out var error));
        Assert.IsNull(error);
        Assert.HasCount(0, viewModel.Project.Assets);

        viewModel.Undo();
        Assert.HasCount(1, viewModel.Project.Assets);

        viewModel.Redo();
        Assert.HasCount(0, viewModel.Project.Assets);
    }

    [TestMethod]
    public void RelinkAfterUndo_OnlyDocumentChangeInvalidatesRedo()
    {
        var project = ProjectDocument.CreateNew("Relink", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Image,
            SourcePath = @"C:\Media\still.png",
            FileName = "still.png",
            DurationMilliseconds = 5_000,
            Width = 1920,
            Height = 1080,
            FileSize = 1234,
            LastWriteUtc = DateTimeOffset.UnixEpoch,
            ThumbnailCachePath = ThumbnailService.CreateRelativeCachePath(new string('a', 64))
        };
        project.Assets.Add(asset);
        var viewModel = new EditorViewModel(project);
        ProjectAsset CopyAsset() => new()
        {
            Id = asset.Id,
            Kind = asset.Kind,
            SourcePath = asset.SourcePath,
            FileName = asset.FileName,
            DurationMilliseconds = asset.DurationMilliseconds,
            Width = asset.Width,
            Height = asset.Height,
            FileSize = asset.FileSize,
            LastWriteUtc = asset.LastWriteUtc,
            ThumbnailCachePath = asset.ThumbnailCachePath,
            IsMissing = asset.IsMissing
        };

        Assert.IsTrue(viewModel.SetBackgroundColor("#FF102030"));
        viewModel.Undo();
        Assert.IsTrue(viewModel.CanRedo);
        var revision = viewModel.Revision;

        Assert.IsTrue(viewModel.ApplyRelinkedAsset(CopyAsset()));

        Assert.AreEqual(revision, viewModel.Revision);
        Assert.IsTrue(viewModel.CanRedo);
        viewModel.Redo();
        Assert.AreEqual("#FF102030", viewModel.Project.Settings.BackgroundColor);

        viewModel.Undo();
        var changedAsset = CopyAsset();
        changedAsset.FileName = "replacement.png";
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsTrue(viewModel.ApplyRelinkedAsset(changedAsset));

        Assert.AreEqual(revision + 3, viewModel.Revision);
        Assert.AreEqual(1, committed);
        Assert.IsFalse(viewModel.CanRedo);
        Assert.AreEqual("replacement.png", viewModel.Project.Assets.Single().FileName);
    }

    [TestMethod]
    public void ApplyRelinkedAsset_WhenCanonicalPathAlreadyExists_RejectsWithoutMutation()
    {
        var relinked = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"D:\Media\old.mp4",
            FileName = "old.mp4",
            DurationMilliseconds = 1_000
        };
        var project = new ProjectDocument
        {
            Assets =
            [
                relinked,
                new ProjectAsset
                {
                    Id = Guid.NewGuid(),
                    Kind = ProjectAssetKind.Video,
                    SourcePath = @"C:\Media\Folder\..\CLIP.mp4",
                    FileName = "clip.mp4",
                    DurationMilliseconds = 1_000
                }
            ]
        };
        var candidate = new ProjectAsset
        {
            Id = relinked.Id,
            Kind = relinked.Kind,
            SourcePath = @"c:/media/./clip.mp4/",
            FileName = "clip.mp4",
            DurationMilliseconds = 1_000
        };
        var viewModel = new EditorViewModel(project);

        Assert.IsFalse(viewModel.ApplyRelinkedAsset(candidate));
        Assert.AreEqual(@"D:\Media\old.mp4", relinked.SourcePath);
        Assert.AreEqual("old.mp4", relinked.FileName);
    }

    [TestMethod]
    public void ApplyRelinkedAsset_WhenTimelineGrowsDuringMetadataAwait_RevalidatesCurrentProjectBeforeMutation()
    {
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            SourcePath = @"C:\Media\original.mp4",
            FileName = "original.mp4",
            DurationMilliseconds = 3_000
        };
        var item = new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceOutMilliseconds = 2_000,
            DurationMilliseconds = 2_000
        };
        var staleProject = new ProjectDocument { Assets = [asset], VideoItems = [item] };
        var viewModel = new EditorViewModel(staleProject);
        var candidate = new ProjectAsset
        {
            Id = asset.Id,
            Kind = asset.Kind,
            SourcePath = asset.SourcePath,
            FileName = asset.FileName,
            DurationMilliseconds = asset.DurationMilliseconds
        };
        var replacement = new MediaFileMetadata(
            @"D:\New\replacement.mp4", "replacement.mp4", ProjectAssetKind.Video,
            DurationMilliseconds: 2_500, Width: 1280, Height: 720,
            FileSize: 45_000, LastWriteUtc: DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsTrue(MediaImportService.TryApplyRelink(candidate, replacement, staleProject, out var preparationError));
        Assert.IsNull(preparationError);
        Assert.IsTrue(viewModel.TrimVideoEnd(item.Id, 2_800));
        var revision = viewModel.Revision;

        Assert.IsFalse(viewModel.ApplyRelinkedAsset(candidate));

        Assert.AreEqual(revision, viewModel.Revision);
        Assert.AreEqual(@"C:\Media\original.mp4", viewModel.Project.Assets.Single().SourcePath);
        Assert.AreEqual(3_000L, viewModel.Project.Assets.Single().DurationMilliseconds);
        Assert.AreEqual(2_800L, viewModel.Project.VideoItems.Single().SourceOutMilliseconds);
    }

    [TestMethod]
    public void AddDefaultText_CreatesSelectedThreeSecondItemAtPlayhead()
    {
        var project = ProjectDocument.CreateNew("Text", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), DurationMilliseconds = 2_000 });
        var viewModel = new EditorViewModel(project);
        viewModel.Seek(1_250);
        var eventRaised = false;
        viewModel.AddTextRequested += (_, _) => eventRaised = true;

        var item = viewModel.AddDefaultText();

        Assert.AreEqual(1_250L, item.StartMilliseconds);
        Assert.AreEqual(3_000L, item.DurationMilliseconds);
        Assert.AreEqual("Text", item.Text);
        Assert.AreEqual(item.Id, viewModel.Selection.ItemId);
        Assert.AreEqual(EditorSelectionKind.TextItem, viewModel.Selection.Kind);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, viewModel.SaveStatus);
        Assert.IsTrue(eventRaised);
    }

    [TestMethod]
    public void DurationShorteningEdit_ClampsPlayheadBeforeAddingText()
    {
        var project = CreateProjectWithVideo(durationMilliseconds: 10_000);
        var viewModel = new EditorViewModel(project);
        viewModel.Seek(9_000);

        Assert.IsTrue(viewModel.TrimVideoEnd(project.VideoItems[0].Id, 4_000));
        var item = viewModel.AddDefaultText();

        Assert.AreEqual(4_000L, viewModel.PlayheadMilliseconds);
        Assert.AreEqual(4_000L, item.StartMilliseconds);
    }

    [TestMethod]
    public void UndoAndRedo_ClampPlayheadWhenRestoredProjectIsShorter()
    {
        var undoProject = CreateProjectWithVideo(durationMilliseconds: 4_000);
        var undoViewModel = new EditorViewModel(undoProject);
        undoViewModel.TrimVideoEnd(undoProject.VideoItems[0].Id, 10_000);
        undoViewModel.Seek(9_000);

        undoViewModel.Undo();

        Assert.AreEqual(4_000L, undoViewModel.PlayheadMilliseconds);

        var redoProject = CreateProjectWithVideo(durationMilliseconds: 10_000);
        var redoViewModel = new EditorViewModel(redoProject);
        redoViewModel.TrimVideoEnd(redoProject.VideoItems[0].Id, 4_000);
        redoViewModel.Undo();
        redoViewModel.Seek(9_000);

        redoViewModel.Redo();

        Assert.AreEqual(4_000L, redoViewModel.PlayheadMilliseconds);
    }

    [TestMethod]
    public void Seek_ClampsToCurrentProjectDuration()
    {
        var viewModel = new EditorViewModel(CreateProjectWithVideo(durationMilliseconds: 2_000));

        viewModel.Seek(9_000);

        Assert.AreEqual(2_000L, viewModel.PlayheadMilliseconds);
    }

    [TestMethod]
    public void Undo_FirstEditBackToInitialBaseline_RemainsUnsavedUntilPersisted()
    {
        var project = ProjectDocument.CreateNew("Baseline", DateTimeOffset.UnixEpoch);
        var viewModel = new EditorViewModel(project);
        var committed = 0;
        viewModel.EditCommitted += (_, _) => committed++;

        Assert.IsTrue(viewModel.SetProjectAspectRatio(AspectRatioPreset.Square1By1));
        Assert.AreEqual(AspectRatioPreset.Square1By1, viewModel.Project.Settings.AspectRatio);
        Assert.AreEqual(1080, viewModel.Project.Settings.Width);
        Assert.AreEqual(1080, viewModel.Project.Settings.Height);
        Assert.AreEqual(1, committed);
        viewModel.MarkSaved();

        viewModel.Undo();

        Assert.AreEqual(AspectRatioPreset.Landscape16By9, viewModel.Project.Settings.AspectRatio);
        Assert.AreEqual(1920, viewModel.Project.Settings.Width);
        Assert.AreEqual(1080, viewModel.Project.Settings.Height);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, viewModel.SaveStatus);
        Assert.AreEqual(2L, viewModel.Revision);
        Assert.AreEqual(2, committed);
        Assert.IsFalse(viewModel.CanUndo);
        Assert.IsTrue(viewModel.CanRedo);
    }

    private static ProjectDocument CreateProjectWithVideo(long durationMilliseconds)
    {
        var project = ProjectDocument.CreateNew("Playhead", DateTimeOffset.UnixEpoch);
        var asset = new ProjectAsset
        {
            Id = Guid.NewGuid(),
            Kind = ProjectAssetKind.Video,
            DurationMilliseconds = 10_000
        };
        project.Assets.Add(asset);
        project.VideoItems.Add(new VideoTimelineItem
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            SourceOutMilliseconds = durationMilliseconds,
            DurationMilliseconds = durationMilliseconds
        });
        return project;
    }
}
