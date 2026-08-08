using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CutFlow.Tests;

[TestClass]
public sealed class Task11LifecycleTests
{
    [TestMethod]
    public async Task CloseWaitsForTheSingleInitializationTaskAndBlocksItsUiContinuation()
    {
        var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle = new MainWindowLifecycle();
        var initializationCalls = 0;
        var initialization = lifecycle.StartInitialization(async () =>
        {
            initializationCalls++;
            await releaseInitialization.Task;
        });

        Assert.IsTrue(lifecycle.TryBeginClosing());
        var closeReady = lifecycle.WaitForInitializationBeforeCloseAsync(initialization);

        Assert.IsFalse(closeReady.IsCompleted);
        Assert.IsFalse(lifecycle.CanContinueInitialization);
        releaseInitialization.SetResult();
        Assert.IsTrue(await closeReady);
        Assert.AreEqual(1, initializationCalls);
        Assert.ThrowsExactly<InvalidOperationException>(() => lifecycle.StartInitialization(() => Task.CompletedTask));
    }

    [TestMethod]
    public async Task ClosedWindowStopsPostAwaitCloseContinuation()
    {
        var lifecycle = new MainWindowLifecycle();
        var initialization = lifecycle.StartInitialization(() => Task.CompletedTask);

        Assert.IsTrue(lifecycle.TryBeginClosing());
        lifecycle.MarkClosed();

        Assert.IsFalse(await lifecycle.WaitForInitializationBeforeCloseAsync(initialization));
        Assert.IsFalse(lifecycle.CanContinueInitialization);
    }

    [TestMethod]
    public void ApprovedCloseUsesTheWindowCloseCallback()
    {
        var lifecycle = new MainWindowLifecycle();
        var closeCalls = 0;

        Assert.IsFalse(lifecycle.TryCommitClose(() => closeCalls++));
        lifecycle.ApproveClose();

        Assert.IsTrue(lifecycle.TryCommitClose(() => closeCalls++));
        Assert.AreEqual(1, closeCalls);
    }

    [TestMethod]
    public void EditorEventGateRejectsStaleSourcesAndClosingWindow()
    {
        var lifecycle = new MainWindowLifecycle();
        var currentEditor = new object();
        var staleEditor = new object();

        Assert.IsTrue(lifecycle.CanHandleEditorEvent(currentEditor, currentEditor));
        Assert.IsFalse(lifecycle.CanHandleEditorEvent(currentEditor, staleEditor));

        Assert.IsTrue(lifecycle.TryBeginClosing());
        Assert.IsFalse(lifecycle.CanHandleEditorEvent(currentEditor, currentEditor));
    }

    [TestMethod]
    public void StartupAndHomeUseTheCloseStartedContinuationGuard()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var initializeStart = mainWindow.IndexOf("private async Task InitializeAsync()", StringComparison.Ordinal);
        var initializeEnd = mainWindow.IndexOf("private void MainWindow_Activated", initializeStart, StringComparison.Ordinal);
        var initialize = mainWindow[initializeStart..initializeEnd];
        var settingsAwait = initialize.IndexOf("loadedSettings = await _settingsService.LoadAsync();", StringComparison.Ordinal);
        var settingsCommit = initialize.IndexOf("_appSettings = loadedSettings;", settingsAwait, StringComparison.Ordinal);
        var settingsContinuation = initialize[settingsAwait..settingsCommit];
        var homeView = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "HomeView.xaml.cs"));

        Assert.IsTrue(settingsContinuation.Contains("CanContinueInitialization", StringComparison.Ordinal));
        Assert.IsTrue(initialize.Contains("await _viewModel.ShowHomeAsync();", StringComparison.Ordinal));
        Assert.Contains("new MainViewModel(_projectService, _mediaImportService, () => _lifecycle.CanContinueInitialization)", mainWindow);
        Assert.Contains("new HomeView(_viewModel.Home, () => _lifecycle.CanContinueInitialization)", mainWindow);
        Assert.Contains("private readonly Func<bool> _canContinue;", homeView);
        Assert.Contains("if (!_canContinue() || result != ContentDialogResult.Primary)", homeView);
        Assert.Contains("if (!_canContinue() || IsProjectOperationActive)", homeView);
    }

    [TestMethod]
    public void CurrentViewSwitch_ConstructsReplacementBeforeDetachingCurrentEditor()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var showCurrentViewStart = mainWindow.IndexOf("private void ShowCurrentView()", StringComparison.Ordinal);
        var showCurrentViewEnd = mainWindow.IndexOf("private void DetachEditorView()", showCurrentViewStart, StringComparison.Ordinal);
        var showCurrentView = mainWindow[showCurrentViewStart..showCurrentViewEnd];
        var construction = showCurrentView.IndexOf("new EditorView(editorViewModel", StringComparison.Ordinal);
        var detach = showCurrentView.IndexOf("DetachEditorView();", construction, StringComparison.Ordinal);

        Assert.IsTrue(construction >= 0);
        Assert.IsTrue(detach > construction);
        Assert.Contains("ReferenceEquals(_editorView.ViewModel, editorViewModel)", showCurrentView);
        Assert.Contains("ReferenceEquals(ContentHost.Content, _editorView)", showCurrentView);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CutFlow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
