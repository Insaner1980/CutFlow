using CutFlow.Models;
using CutFlow.Services;
using CutFlow.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public async Task OpenEditorAsync_RevalidatesMissingMediaWithoutRemovingReferences()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "present.png");
        await File.WriteAllTextAsync(sourcePath, "fixture");
        var project = ProjectDocument.CreateNew("Missing check", DateTimeOffset.UnixEpoch);
        var present = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = sourcePath, IsMissing = true };
        var missing = new ProjectAsset { Id = Guid.NewGuid(), SourcePath = Path.Combine(directory.Path, "missing.png") };
        project.Assets.AddRange([present, missing]);
        var viewModel = new MainViewModel(new ProjectService(directory.Path));

        await viewModel.OpenEditorAsync(project);

        Assert.HasCount(2, viewModel.Editor!.Project.Assets);
        Assert.IsFalse(present.IsMissing);
        Assert.IsTrue(missing.IsMissing);
    }

    [TestMethod]
    public async Task ShowHomeAsync_PersistsEditorAspectAndTextBeforeNavigating()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Persisted edit");
        var viewModel = new MainViewModel(service);
        await viewModel.OpenEditorAsync(project);
        var textId = Guid.NewGuid();
        viewModel.Editor!.CommitEdit(document =>
        {
            document.Settings.ApplyAspectRatio(AspectRatioPreset.Portrait9By16);
            document.TextItems.Add(new TextTimelineItem
            {
                Id = textId,
                StartMilliseconds = 250,
                DurationMilliseconds = 3_000,
                Text = "Text"
            });
        });

        var navigated = await viewModel.ShowHomeAsync();

        Assert.IsTrue(navigated);
        Assert.IsFalse(viewModel.IsEditorOpen);
        var persisted = await service.LoadAsync(project.Id);
        Assert.AreEqual(AspectRatioPreset.Portrait9By16, persisted.Settings.AspectRatio);
        Assert.AreEqual(textId, persisted.TextItems.Single().Id);
    }

    [TestMethod]
    public async Task ShowHomeAsync_WhenSaveFails_RemainsInEditorAndShowsFailure()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Invalid edit");
        var viewModel = new MainViewModel(service);
        await viewModel.OpenEditorAsync(project);
        var editor = viewModel.Editor!;
        editor.CommitEdit(document => document.SchemaVersion = 99);

        var navigated = await viewModel.ShowHomeAsync();

        Assert.IsFalse(navigated);
        Assert.IsTrue(viewModel.IsEditorOpen);
        Assert.AreSame(editor, viewModel.Editor);
        Assert.AreEqual(EditorViewModel.SaveFailedStatus, editor.SaveStatus);
    }

    [TestMethod]
    public async Task ShowHomeAsync_RaisesViewChangeBeforeCompletingLoad()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        await service.CreateAsync("Visible home");
        var viewModel = new MainViewModel(service);
        var editorProject = await service.ListAsync();
        await viewModel.OpenEditorAsync(editorProject.Single());
        var editorWasClearedWhenRaised = false;
        viewModel.CurrentViewChanged += (_, _) => editorWasClearedWhenRaised = !viewModel.IsEditorOpen;

        await viewModel.ShowHomeAsync();

        Assert.IsTrue(editorWasClearedWhenRaised);
        Assert.HasCount(1, viewModel.Home.Projects);
    }

    [TestMethod]
    public async Task OpenEditorAsync_WhenViewChangeFails_RestoresHomeState()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Failed editor view", DateTimeOffset.UnixEpoch);
        viewModel.CurrentViewChanged += (_, _) => throw new InvalidOperationException("View construction failed.");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.OpenEditorAsync(project));

        Assert.IsNull(viewModel.CurrentProject);
        Assert.IsNull(viewModel.Editor);
        Assert.IsFalse(viewModel.IsEditorOpen);
    }

    [TestMethod]
    public async Task ShowHomeAsync_WhenViewChangeFails_RestoresEditorState()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Retained editor view", DateTimeOffset.UnixEpoch);
        await viewModel.OpenEditorAsync(project);
        var editor = viewModel.Editor;
        viewModel.CurrentViewChanged += (_, _) => throw new InvalidOperationException("Home view switch failed.");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.ShowHomeAsync());

        Assert.AreSame(project, viewModel.CurrentProject);
        Assert.AreSame(editor, viewModel.Editor);
        Assert.IsTrue(viewModel.IsEditorOpen);
    }

    [TestMethod]
    public async Task OpenEditorAsync_WhenCloseStartsDuringRefresh_DoesNotCommitEditorState()
    {
        using var directory = new TemporaryDirectory();
        var continuationChecks = 0;
        var viewModel = new MainViewModel(
            new ProjectService(directory.Path),
            canContinue: () => Interlocked.Increment(ref continuationChecks) == 1);
        var project = ProjectDocument.CreateNew("Suppressed editor", DateTimeOffset.UnixEpoch);
        var viewChanges = 0;
        viewModel.CurrentViewChanged += (_, _) => viewChanges++;

        await viewModel.OpenEditorAsync(project);

        Assert.IsNull(viewModel.CurrentProject);
        Assert.IsNull(viewModel.Editor);
        Assert.AreEqual(0, viewChanges);
    }

    [TestMethod]
    public async Task ShowHomeAsync_WhenCloseStartsDuringFailedSave_SuppressesExceptionalContinuation()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var suppressAfterFirstCheck = false;
        var continuationChecks = 0;
        var viewModel = new MainViewModel(
            service,
            canContinue: () => !suppressAfterFirstCheck || Interlocked.Increment(ref continuationChecks) == 1);
        var project = await service.CreateAsync("Failed close continuation");

        await viewModel.OpenEditorAsync(project);
        viewModel.Editor!.CommitEdit(document => document.SchemaVersion = 99);
        continuationChecks = 0;
        suppressAfterFirstCheck = true;

        var navigated = await viewModel.ShowHomeAsync();

        Assert.IsFalse(navigated);
        Assert.IsTrue(viewModel.IsEditorOpen);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, viewModel.Editor.SaveStatus);
        Assert.IsNull(viewModel.Home.ErrorMessage);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CutFlow.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
