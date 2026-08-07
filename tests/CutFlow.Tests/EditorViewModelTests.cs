using CutFlow.Models;
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
    public void AddAssetToTimeline_PlacesAudioAtPlayheadAndRejectsMissingAsset()
    {
        var project = ProjectDocument.CreateNew("Audio", DateTimeOffset.UnixEpoch);
        project.TextItems.Add(new TextTimelineItem { Id = Guid.NewGuid(), DurationMilliseconds = 2_000 });
        var audio = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Audio, DurationMilliseconds = 3_000 };
        var missing = new ProjectAsset { Id = Guid.NewGuid(), Kind = ProjectAssetKind.Video, DurationMilliseconds = 1_000, IsMissing = true };
        project.Assets.AddRange([audio, missing]);
        var viewModel = new EditorViewModel(project);
        viewModel.Seek(1_250);

        Assert.IsTrue(viewModel.AddAssetToTimeline(audio.Id));
        Assert.IsFalse(viewModel.AddAssetToTimeline(missing.Id));

        var item = viewModel.Project.AudioItems.Single();
        Assert.AreEqual(1_250L, item.StartMilliseconds);
        Assert.AreEqual(0L, item.SourceInMilliseconds);
        Assert.AreEqual(3_000L, item.SourceOutMilliseconds);
        Assert.AreEqual(EditorSelectionKind.AudioItem, viewModel.Selection.Kind);
        Assert.AreEqual(item.Id, viewModel.Selection.ItemId);
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
    public void Undo_FirstEditBackToPersistedBaseline_ShowsSaved()
    {
        var project = ProjectDocument.CreateNew("Baseline", DateTimeOffset.UnixEpoch);
        var viewModel = new EditorViewModel(project);
        viewModel.CommitEdit(document => document.Settings.ApplyAspectRatio(AspectRatioPreset.Square1By1));

        viewModel.Undo();

        Assert.AreEqual(AspectRatioPreset.Landscape16By9, viewModel.Project.Settings.AspectRatio);
        Assert.AreEqual(EditorViewModel.SavedStatus, viewModel.SaveStatus);
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
