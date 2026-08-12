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
        var windowReadyAwait = initialize.IndexOf("var context = await _windowReady.Task;", StringComparison.Ordinal);
        var settingsAwait = initialize.IndexOf("loadedSettings = await _settingsService.LoadAsync();", StringComparison.Ordinal);
        var settingsCommit = initialize.IndexOf("_appSettings = loadedSettings;", settingsAwait, StringComparison.Ordinal);
        var firstCloseGuard = initialize.IndexOf("if (!_lifecycle.CanContinueInitialization)", StringComparison.Ordinal);
        var homeView = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "HomeView.xaml.cs"));

        Assert.IsTrue(windowReadyAwait >= 0);
        Assert.IsTrue(settingsAwait > windowReadyAwait);
        Assert.IsTrue(settingsCommit > settingsAwait);
        Assert.IsTrue(firstCloseGuard > settingsCommit);
        Assert.IsTrue(initialize.Contains("await _viewModel.ShowHomeAsync();", StringComparison.Ordinal));
        Assert.Contains("Startup recovery failed:", initialize);
        Assert.Contains("_lifecycle.ApproveClose();", initialize);
        Assert.Contains("_lifecycle.TryCommitClose(Close);", initialize);
        Assert.Contains("new MainViewModel(_projectService, _mediaImportService, () => _lifecycle.CanContinueInitialization)", mainWindow);
        Assert.Contains("new HomeView(_viewModel.Home, () => _lifecycle.CanContinueInitialization)", mainWindow);
        Assert.Contains("private readonly Func<bool> _canContinue;", homeView);
        Assert.Contains("if (!_canContinue() || result != ContentDialogResult.Primary)", homeView);
        Assert.Contains("if (!_canContinue() || IsProjectOperationActive)", homeView);
    }

    [TestMethod]
    public void CurrentViewSwitch_DetachesAnExistingEditorBeforeConstructingItsReplacement()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var showCurrentViewStart = mainWindow.IndexOf("private void ShowCurrentView()", StringComparison.Ordinal);
        var showCurrentViewEnd = mainWindow.IndexOf("private void DetachEditorView()", showCurrentViewStart, StringComparison.Ordinal);
        var showCurrentView = mainWindow[showCurrentViewStart..showCurrentViewEnd];
        var replacementGuard = showCurrentView.IndexOf("if (_editorView is not null)", StringComparison.Ordinal);
        var detach = showCurrentView.IndexOf("DetachEditorView();", replacementGuard, StringComparison.Ordinal);
        var construction = showCurrentView.IndexOf("new EditorView(editorViewModel", detach, StringComparison.Ordinal);
        var exposure = showCurrentView.IndexOf("_editorView = editorView;", construction, StringComparison.Ordinal);

        Assert.IsTrue(replacementGuard >= 0);
        Assert.IsTrue(detach > replacementGuard);
        Assert.IsTrue(construction > detach);
        Assert.IsTrue(exposure > construction);
        Assert.Contains("ReferenceEquals(_editorView.ViewModel, editorViewModel)", showCurrentView);
        Assert.Contains("ReferenceEquals(ContentHost.Content, _editorView)", showCurrentView);
    }

    [TestMethod]
    public void EditorDetachment_ReleasesContentAndTitleBarBeforeDisposal()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var detachStart = mainWindow.IndexOf("private void DetachEditorView()", StringComparison.Ordinal);
        var detachEnd = mainWindow.IndexOf("private async Task InitializeAsync()", detachStart, StringComparison.Ordinal);
        var detach = mainWindow[detachStart..detachEnd];
        var clearTitleBar = detach.IndexOf("SetTitleBar(null);", StringComparison.Ordinal);
        var clearContent = detach.IndexOf("ContentHost.Content = null;", StringComparison.Ordinal);
        var dispose = detach.IndexOf("editor.Dispose();", StringComparison.Ordinal);

        Assert.IsTrue(clearTitleBar >= 0);
        Assert.IsTrue(clearContent > clearTitleBar);
        Assert.IsTrue(dispose > clearContent);
    }

    [TestMethod]
    public void TimelineAsyncWork_UsesEditorLifetimeBeforeCommittingState()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var timeline = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var dropStart = timeline.IndexOf("private async void TimelineContent_Drop", StringComparison.Ordinal);
        var dropEnd = timeline.IndexOf("private TimelineTrackKind HitTestTrack", dropStart, StringComparison.Ordinal);
        var drop = timeline[dropStart..dropEnd];

        Assert.Contains("Timeline.SetLifetimeToken(_lifetimeToken);", editor);
        Assert.Contains("var lifetimeToken = _lifetimeToken;", drop);
        Assert.Contains("lifetimeToken.ThrowIfCancellationRequested();", drop);
        Assert.Contains("MediaDropReader.ReadAsync", drop);
        Assert.Contains("lifetimeToken);", drop);
        Assert.AreEqual(
            2,
            timeline.Split("if (_lifetimeToken.IsCancellationRequested) return;", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void TimelineZoom_RefreshesTheScrollExtentBeforeApplyingTheAnchoredOffset()
    {
        var root = FindRepositoryRoot();
        var timeline = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var setZoomStart = timeline.IndexOf("private void SetZoom(double requestedPixelsPerSecond", StringComparison.Ordinal);
        var setZoomEnd = timeline.IndexOf("private void FitButton_Click", setZoomStart, StringComparison.Ordinal);
        var setZoom = timeline[setZoomStart..setZoomEnd];
        var updateWidth = setZoom.IndexOf("UpdateContentWidth();", StringComparison.Ordinal);
        var updateLayout = setZoom.IndexOf("TimelineScroller.UpdateLayout();", StringComparison.Ordinal);
        var changeView = setZoom.IndexOf("TimelineScroller.ChangeView(offset", StringComparison.Ordinal);

        Assert.IsTrue(updateWidth >= 0);
        Assert.IsTrue(updateLayout > updateWidth);
        Assert.IsTrue(changeView > updateLayout);
    }

    [TestMethod]
    public void ThumbnailWork_CancelsWhenCardsUnrealizeAndDisposalDoesNotBlockTheUiThread()
    {
        var root = FindRepositoryRoot();
        var mediaPanel = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "MediaPanel.xaml.cs"));
        var recycleStart = mediaPanel.IndexOf("private void AssetGrid_ContainerContentChanging", StringComparison.Ordinal);
        var recycleEnd = mediaPanel.IndexOf("private void AssetGrid_DoubleTapped", recycleStart, StringComparison.Ordinal);
        var recycle = mediaPanel[recycleStart..recycleEnd];
        var disposeStart = mediaPanel.IndexOf("public void Dispose()", StringComparison.Ordinal);
        var disposeEnd = mediaPanel.IndexOf("private void StopThumbnailWork", disposeStart, StringComparison.Ordinal);
        var dispose = mediaPanel[disposeStart..disposeEnd];

        Assert.Contains("if (args.InRecycleQueue)", recycle);
        Assert.Contains("card.CancelThumbnailRequest();", recycle);
        Assert.Contains("card.BeginThumbnailRequest", recycle);
        Assert.Contains("work.Generation", recycle);
        Assert.Contains("StopThumbnailWork();", dispose);
        Assert.IsFalse(dispose.Contains(".Wait(", StringComparison.Ordinal));
        Assert.IsFalse(dispose.Contains(".Result", StringComparison.Ordinal));
        Assert.IsFalse(dispose.Contains("GetAwaiter().GetResult", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TimelineThumbnailWork_CancelsOldRendersAndChecksGenerationBeforePublishing()
    {
        var root = FindRepositoryRoot();
        var timeline = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var renderStart = timeline.IndexOf("private void RenderClips()", StringComparison.Ordinal);
        var renderEnd = timeline.IndexOf("private Border CreateVideoCard", renderStart, StringComparison.Ordinal);
        var render = timeline[renderStart..renderEnd];
        var loadStart = timeline.IndexOf("private async Task LoadThumbnailSafelyAsync", StringComparison.Ordinal);
        var loadEnd = timeline.IndexOf("private static bool IsControlDown", loadStart, StringComparison.Ordinal);
        var load = timeline[loadStart..loadEnd];
        var publish = load.IndexOf("new Image { Source = bitmap", StringComparison.Ordinal);
        var finalGenerationCheck = load.LastIndexOf("IsCurrentThumbnail(", publish, StringComparison.Ordinal);

        Assert.Contains("RestartThumbnailWork()", render);
        Assert.Contains("thumbnailGeneration", render);
        Assert.IsTrue(finalGenerationCheck >= 0 && finalGenerationCheck < publish);
        Assert.Contains("generation != _thumbnailRenderGeneration", load);
        Assert.Contains("ThumbnailService.IsCurrentRequest(asset, request)", load);
        Assert.Contains("string.Equals(asset.ThumbnailCachePath, relativePath", load);
        Assert.Contains("Timeline.StopThumbnailWork", editor);
    }

    [TestMethod]
    public void EditorNewProject_CreatesBeforeDirectReplacementWithoutShowingHome()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var handlerStart = mainWindow.IndexOf("private async void Editor_NewProjectRequested", StringComparison.Ordinal);
        var handlerEnd = mainWindow.IndexOf("private async void Editor_ImportRequested", handlerStart, StringComparison.Ordinal);
        var handler = mainWindow[handlerStart..handlerEnd];
        var gate = handler.IndexOf("RunProjectOpenGateAsync", StringComparison.Ordinal);
        var creation = handler.IndexOf("_viewModel.Home.CreateAsync()", StringComparison.Ordinal);
        var replacement = handler.IndexOf("_viewModel.ReplaceEditorAsync(project)", StringComparison.Ordinal);

        Assert.IsTrue(gate >= 0 && creation > gate && replacement > creation);
        Assert.IsFalse(handler.Contains("ShowHomeAsync", StringComparison.Ordinal));
        Assert.Contains("catch (OperationCanceledException)", handler);
        Assert.Contains("editor.ReportError(\"Could not create project\"", handler);
        Assert.Contains("editor.IsEnabled = false;", handler);
        Assert.Contains("editor.IsEnabled = true;", handler);
        Assert.AreEqual(1, mainWindow.Split("NewProjectRequested += Editor_NewProjectRequested", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(1, mainWindow.Split("NewProjectRequested -= Editor_NewProjectRequested", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void WindowMinimumTracking_RespondsToPositionAndSizeChanges()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var handlerStart = mainWindow.IndexOf("private void AppWindow_Changed", StringComparison.Ordinal);
        var handlerEnd = mainWindow.IndexOf("private static void ApplyPreferredMinimum", handlerStart, StringComparison.Ordinal);
        var handler = mainWindow[handlerStart..handlerEnd];

        Assert.Contains("!args.DidPositionChange && !args.DidSizeChange", handler);
        Assert.Contains("ApplyPreferredMinimum", handler);
    }

    [TestMethod]
    public void FirstCanceledClose_CapturesBoundsAndFlushesProjectAgainBeforeApproval()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var handlerStart = mainWindow.IndexOf("private void AppWindow_Closing", StringComparison.Ordinal);
        var handlerEnd = mainWindow.IndexOf("private async Task SaveBeforeClosingAsync", handlerStart, StringComparison.Ordinal);
        var handler = mainWindow[handlerStart..handlerEnd];
        var closePreparationStart = mainWindow.IndexOf("private async Task SaveBeforeClosingAsync", handlerEnd, StringComparison.Ordinal);
        var closePreparationEnd = mainWindow.IndexOf("private WindowGeometry CaptureBoundsForPersistence", closePreparationStart, StringComparison.Ordinal);
        var closePreparation = mainWindow[closePreparationStart..closePreparationEnd];
        var editorPreparation = closePreparation.IndexOf("await _editorView.PrepareToCloseAsync()", StringComparison.Ordinal);
        var capture = closePreparation.IndexOf("CaptureBoundsForPersistence(appWindow)", StringComparison.Ordinal);
        var save = closePreparation.IndexOf("await SaveWindowSettingsAsync(closeBounds);", StringComparison.Ordinal);
        var finalEditorFlush = closePreparation.LastIndexOf("await _editorView.PrepareToCloseAsync()", StringComparison.Ordinal);
        var approval = closePreparation.IndexOf("_lifecycle.ApproveClose();", StringComparison.Ordinal);

        Assert.IsTrue(editorPreparation >= 0);
        Assert.IsTrue(capture > editorPreparation);
        Assert.IsTrue(save > capture);
        Assert.IsTrue(finalEditorFlush > save);
        Assert.IsTrue(approval > finalEditorFlush);
        Assert.Contains("args.Cancel = true;", handler);
        Assert.Contains("if (_lifecycle.CloseApproved)", handler);
        Assert.Contains("SaveBeforeClosingAsync(sender)", handler);
        Assert.Contains("private async Task SaveBeforeClosingAsync(AppWindow appWindow)", mainWindow);
    }

    [TestMethod]
    public void CloseSettings_RevalidateBoundsAgainstFreshDisplaySnapshot()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var saveStart = mainWindow.IndexOf("private async Task SaveWindowSettingsAsync", StringComparison.Ordinal);
        var saveEnd = mainWindow.IndexOf("private static double ToLogical", saveStart, StringComparison.Ordinal);
        var save = mainWindow[saveStart..saveEnd];
        var displayLookup = save.IndexOf("DisplayArea.GetFromRect", StringComparison.Ordinal);
        var clamp = save.IndexOf("WindowGeometry.ClampPhysicalToWorkArea", StringComparison.Ordinal);
        var persistence = save.IndexOf("_appSettings.WindowPixelX = bounds.X;", StringComparison.Ordinal);

        Assert.IsTrue(displayLookup >= 0);
        Assert.IsTrue(clamp > displayLookup);
        Assert.IsTrue(persistence > clamp);
        Assert.Contains("var workArea = displayArea.WorkArea;", save);
        Assert.Contains("var outerBounds = displayArea.OuterBounds;", save);
    }

    [TestMethod]
    public void CloseFailure_ConsumesQueuedWorkspaceFailureBeforeReporting()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var closePreparationStart = mainWindow.IndexOf("private async Task SaveBeforeClosingAsync", StringComparison.Ordinal);
        var closePreparationEnd = mainWindow.IndexOf("private WindowGeometry CaptureBoundsForPersistence", closePreparationStart, StringComparison.Ordinal);
        var closePreparation = mainWindow[closePreparationStart..closePreparationEnd];
        var failureCatch = closePreparation.IndexOf("catch (Exception exception)", StringComparison.Ordinal);
        var consumeQueuedNotice = closePreparation.IndexOf(
            "_workspaceSettingsSaveFailureNotice.Observe(_settingsSaveCoordinator.State);",
            failureCatch,
            StringComparison.Ordinal);
        var reportCloseFailure = closePreparation.IndexOf(
            "_editorView.ReportError(\"Could not close safely\"",
            failureCatch,
            StringComparison.Ordinal);

        Assert.IsTrue(failureCatch >= 0);
        Assert.IsTrue(consumeQueuedNotice > failureCatch);
        Assert.IsTrue(reportCloseFailure > consumeQueuedNotice);
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
