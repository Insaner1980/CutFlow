using System.Text.Json;
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
    public async Task ShowHomeAsync_WhenEditCommitsDuringImmediateSave_PersistsLatestRevisionBeforeNavigating()
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = await initialService.CreateAsync("Initial");
        var firstRevisionSerialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serializationCount = 0;
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var service = new ProjectService(directory.Path, document =>
        {
            var json = JsonSerializer.Serialize(document, jsonOptions);
            if (Interlocked.Increment(ref serializationCount) == 1)
            {
                firstRevisionSerialized.TrySetResult();
                releaseFirstSave.Task.GetAwaiter().GetResult();
            }

            return json;
        });
        var viewModel = new MainViewModel(service);
        await viewModel.OpenEditorAsync(project);
        var editor = viewModel.Editor!;
        editor.RenameProject("First revision");

        var navigationTask = Task.Run(() => viewModel.ShowHomeAsync());
        await firstRevisionSerialized.Task.WaitAsync(TimeSpan.FromSeconds(5));
        editor.RenameProject("Latest revision");
        releaseFirstSave.TrySetResult();

        var navigated = await navigationTask;

        Assert.IsTrue(navigated);
        Assert.IsFalse(viewModel.IsEditorOpen);
        Assert.AreEqual(2, serializationCount);
        Assert.AreEqual("Latest revision", (await service.LoadAsync(project.Id)).Name);
    }

    [TestMethod]
    public async Task ShowHomeAsync_WhenImmediateSaveIsCanceled_RemainsInEditorAndCanRetry()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(directory.Path);
        var project = await service.CreateAsync("Canceled navigation");
        var viewModel = new MainViewModel(service);
        await viewModel.OpenEditorAsync(project);
        var editor = viewModel.Editor!;
        editor.RenameProject("Unsaved revision");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => viewModel.ShowHomeAsync(cancellation.Token));

        Assert.AreSame(editor, viewModel.Editor);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, editor.SaveStatus);
        Assert.IsTrue(await viewModel.ShowHomeAsync());
        Assert.AreEqual("Unsaved revision", (await service.LoadAsync(project.Id)).Name);
    }

    [TestMethod]
    public async Task ShowHomeAsync_WhenSaveIsCanceledAfterTempFlush_RemainsUnsavedAndPreservesPriorJson()
    {
        using var directory = new TemporaryDirectory();
        var initialService = new ProjectService(directory.Path);
        var project = await initialService.CreateAsync("Persisted revision");
        var projectPath = Path.Combine(directory.Path, "Projects", project.Id.ToString("D"), "project.json");
        var originalJson = await File.ReadAllTextAsync(projectPath);
        using var cancellation = new CancellationTokenSource();
        var service = new ProjectService(
            directory.Path,
            writeAndFlushAsync: async (path, json, _) =>
            {
                await File.WriteAllTextAsync(path, json);
                cancellation.Cancel();
            });
        var viewModel = new MainViewModel(service);
        await viewModel.OpenEditorAsync(project);
        var editor = viewModel.Editor!;
        editor.RenameProject("Canceled revision");

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => viewModel.ShowHomeAsync(cancellation.Token));

        Assert.AreSame(editor, viewModel.Editor);
        Assert.AreEqual(EditorViewModel.UnsavedStatus, editor.SaveStatus);
        Assert.AreEqual(originalJson, await File.ReadAllTextAsync(projectPath));
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
    public async Task CurrentViewPropertyChanges_OnlyPublishCoherentStates()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Coherent transition", DateTimeOffset.UnixEpoch);
        var observedIncoherentState = false;
        viewModel.PropertyChanged += (_, _) =>
        {
            var currentProject = viewModel.CurrentProject;
            var editor = viewModel.Editor;
            observedIncoherentState |= (currentProject is null) != (editor is null) ||
                (editor is not null && !ReferenceEquals(currentProject, editor.Project));
        };

        await viewModel.OpenEditorAsync(project);
        await viewModel.ShowHomeAsync();

        Assert.IsFalse(observedIncoherentState);
    }

    [TestMethod]
    public async Task OpenEditorAsync_WhenPropertyObserverThrows_RestoresHomeState()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Failed property notification", DateTimeOffset.UnixEpoch);
        var throwOnNextChange = true;
        viewModel.PropertyChanged += (_, _) =>
        {
            if (throwOnNextChange)
            {
                throwOnNextChange = false;
                throw new InvalidOperationException("Property observer failed.");
            }
        };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.OpenEditorAsync(project));

        Assert.IsNull(viewModel.CurrentProject);
        Assert.IsNull(viewModel.Editor);
        Assert.IsFalse(viewModel.IsEditorOpen);
    }

    [TestMethod]
    public async Task OpenEditorAsync_ReentrantHomeRequestDoesNotInterruptTransition()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Reentrant transition", DateTimeOffset.UnixEpoch);
        bool? reentrantNavigationResult = null;
        var attemptReentrantNavigation = true;
        viewModel.CurrentViewChanged += (_, _) =>
        {
            if (!attemptReentrantNavigation)
            {
                return;
            }

            attemptReentrantNavigation = false;
            reentrantNavigationResult = viewModel.ShowHomeAsync().GetAwaiter().GetResult();
        };

        await viewModel.OpenEditorAsync(project);

        Assert.IsFalse(reentrantNavigationResult);
        Assert.AreSame(project, viewModel.CurrentProject);
        Assert.AreSame(project, viewModel.Editor?.Project);
        Assert.IsTrue(viewModel.IsEditorOpen);
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
    public async Task OpenEditorAsync_WhenLaterViewObserverFails_NotifiesEarlierObserverOfRollback()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var project = ProjectDocument.CreateNew("Failed later observer", DateTimeOffset.UnixEpoch);
        var observedEditorStates = new List<bool>();
        viewModel.CurrentViewChanged += (_, _) => observedEditorStates.Add(viewModel.IsEditorOpen);
        viewModel.CurrentViewChanged += (_, _) => throw new InvalidOperationException("Later observer failed.");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.OpenEditorAsync(project));

        CollectionAssert.AreEqual(new[] { true, false }, observedEditorStates);
        Assert.IsNull(viewModel.CurrentProject);
        Assert.IsNull(viewModel.Editor);
    }

    [TestMethod]
    public async Task OpenEditorAsync_WhenEditorIsAlreadyOpen_RejectsReplacementAndRetainsCurrentState()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var currentProject = ProjectDocument.CreateNew("Current editor", DateTimeOffset.UnixEpoch);
        var replacementProject = ProjectDocument.CreateNew("Replacement editor", DateTimeOffset.UnixEpoch);
        await viewModel.OpenEditorAsync(currentProject);
        var currentEditor = viewModel.Editor;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.OpenEditorAsync(replacementProject));

        Assert.AreSame(currentProject, viewModel.CurrentProject);
        Assert.AreSame(currentEditor, viewModel.Editor);
    }

    [TestMethod]
    public async Task ReplaceEditorAsync_PublishesOneDirectEditorTransition()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var currentProject = ProjectDocument.CreateNew("Current editor", DateTimeOffset.UnixEpoch);
        var replacementProject = ProjectDocument.CreateNew("Replacement editor", DateTimeOffset.UnixEpoch);
        await viewModel.OpenEditorAsync(currentProject);
        var observedProjects = new List<ProjectDocument?>();
        viewModel.CurrentViewChanged += (_, _) => observedProjects.Add(viewModel.CurrentProject);

        await viewModel.ReplaceEditorAsync(replacementProject);

        CollectionAssert.AreEqual(new[] { replacementProject }, observedProjects);
        Assert.AreSame(replacementProject, viewModel.CurrentProject);
        Assert.AreSame(replacementProject, viewModel.Editor?.Project);
    }

    [TestMethod]
    public async Task ReplaceEditorAsync_WhenCanceled_PreservesCurrentEditor()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new MainViewModel(new ProjectService(directory.Path));
        var currentProject = ProjectDocument.CreateNew("Current editor", DateTimeOffset.UnixEpoch);
        var replacementProject = ProjectDocument.CreateNew("Canceled replacement", DateTimeOffset.UnixEpoch);
        await viewModel.OpenEditorAsync(currentProject);
        var currentEditor = viewModel.Editor;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            viewModel.ReplaceEditorAsync(replacementProject, cancellation.Token));

        Assert.AreSame(currentProject, viewModel.CurrentProject);
        Assert.AreSame(currentEditor, viewModel.Editor);
    }

    [TestMethod]
    public async Task NewProjectCreationFailure_PreservesCurrentEditor()
    {
        using var directory = new TemporaryDirectory();
        var service = new ProjectService(
            directory.Path,
            _ => throw new InvalidOperationException("Deterministic project creation failure."));
        var viewModel = new MainViewModel(service);
        var currentProject = ProjectDocument.CreateNew("Current editor", DateTimeOffset.UnixEpoch);
        await viewModel.OpenEditorAsync(currentProject);
        var currentEditor = viewModel.Editor;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => viewModel.Home.CreateAsync());

        Assert.AreSame(currentProject, viewModel.CurrentProject);
        Assert.AreSame(currentEditor, viewModel.Editor);
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
