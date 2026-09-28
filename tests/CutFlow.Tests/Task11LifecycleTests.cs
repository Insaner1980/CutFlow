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
    public void CanceledCloseDoesNotCommitTheWindowCloseCallback()
    {
        var lifecycle = new MainWindowLifecycle();
        var closeCalls = 0;

        Assert.IsTrue(lifecycle.TryBeginClosing());
        lifecycle.ApproveClose();
        lifecycle.CancelClosing();

        Assert.IsFalse(lifecycle.TryCommitClose(() => closeCalls++));
        Assert.AreEqual(0, closeCalls);
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

        Assert.IsGreaterThanOrEqualTo(0, windowReadyAwait);
        Assert.IsGreaterThan(windowReadyAwait, settingsAwait);
        Assert.IsGreaterThan(settingsAwait, settingsCommit);
        Assert.IsGreaterThan(settingsCommit, firstCloseGuard);
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
    public void UiExceptionBoundaries_FilterLocalFailuresAndLogBroadStartupRecovery()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var initializeStart = mainWindow.IndexOf("private async Task InitializeAsync()", StringComparison.Ordinal);
        var initializeEnd = mainWindow.IndexOf("private void MainWindow_Activated", initializeStart, StringComparison.Ordinal);
        var initialize = mainWindow[initializeStart..initializeEnd];
        var homeView = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "HomeView.xaml.cs"));

        Assert.AreEqual(
            5,
            homeView.Split(
                "catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))",
                StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)",
            initialize);
        Assert.Contains(
            "catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or COMException or OverflowException)",
            initialize);
        Assert.Contains("Workspace settings restore failed:", initialize);
        Assert.Contains("Window geometry restore failed:", initialize);
        Assert.Contains("Startup failed:", initialize);
    }

    [TestMethod]
    public void UiCancellationHandlers_RequireTheirOwningLifetimeToBeCanceled()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var mediaPanel = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "MediaPanel.xaml.cs"));
        var timeline = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));

        Assert.AreEqual(
            3,
            mainWindow.Split(
                "catch (OperationCanceledException) when (!_lifecycle.CanHandleEditorEvent(_editorView, editor))",
                StringSplitOptions.None).Length - 1);
        Assert.AreEqual(
            2,
            mediaPanel.Split(
                "catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)", timeline);
        Assert.Contains("catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)", timeline);
        Assert.AreEqual(
            2,
            editor.Split(
                "catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("lease?.Token.IsCancellationRequested == true", editor);
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

        Assert.IsGreaterThanOrEqualTo(0, replacementGuard);
        Assert.IsGreaterThan(replacementGuard, detach);
        Assert.IsGreaterThan(detach, construction);
        Assert.IsGreaterThan(construction, exposure);
        Assert.Contains("ReferenceEquals(_editorView.ViewModel, editorViewModel)", showCurrentView);
        Assert.Contains("ReferenceEquals(ContentHost.Content, _editorView)", showCurrentView);
    }

    [TestMethod]
    public void CurrentViewSwitch_QueuesFocusForTheLiveReplacement()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));
        var homeView = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "HomeView.xaml.cs"));
        var editorView = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var queueStart = mainWindow.IndexOf("private void QueueInitialFocus", StringComparison.Ordinal);
        var queueEnd = mainWindow.IndexOf("private void DetachEditorView", queueStart, StringComparison.Ordinal);
        var queue = mainWindow[queueStart..queueEnd];

        Assert.Contains("QueueInitialFocus(_editorView, _editorView.FocusInitialControl);", mainWindow);
        Assert.Contains("QueueInitialFocus(_homeView, _homeView.FocusInitialControl);", mainWindow);
        Assert.Contains("ReferenceEquals(ContentHost.Content, content)", queue);
        Assert.Contains("_lifecycle.CanContinueInitialization", queue);
        Assert.Contains("HomeItem.Focus(FocusState.Programmatic)", homeView);
        Assert.Contains("BackButton.Focus(FocusState.Programmatic)", editorView);
    }

    [TestMethod]
    public void ProjectDialogs_RestoreLiveFocusAndDestructiveDeleteDefaultsToCancel()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Views",
            "HomeView.xaml.cs"));
        var renameStart = source.IndexOf("private async void RenameProject_Click", StringComparison.Ordinal);
        var deleteStart = source.IndexOf("private async void DeleteProject_Click", StringComparison.Ordinal);
        var rename = source[renameStart..deleteStart];
        var deleteEnd = source.IndexOf("private void HomeNavigation_SelectionChanged", deleteStart, StringComparison.Ordinal);
        var delete = source[deleteStart..deleteEnd];

        Assert.Contains("dialog.Opened +=", rename);
        Assert.Contains("nameBox.Focus(FocusState.Programmatic);", rename);
        Assert.Contains("nameBox.SelectAll();", rename);
        Assert.Contains("ContentDialogButton.Close", delete);
        Assert.Contains("finally", delete);
        Assert.Contains("RestoreProjectFocus(project.Id);", delete);
        Assert.Contains("FindProjectActionsButton", source);
        Assert.Contains("HomeItem.Focus(FocusState.Programmatic)", source);
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

        Assert.IsGreaterThanOrEqualTo(0, clearTitleBar);
        Assert.IsGreaterThan(clearTitleBar, clearContent);
        Assert.IsGreaterThan(clearContent, dispose);
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
    public void TimelineFileDrop_RechecksTrackLockBeforeAnyPreparedImportCommit()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var commitStart = editor.IndexOf("private void CommitTimelineDropResults", StringComparison.Ordinal);
        var commitEnd = editor.IndexOf("private bool TryAddAssetToTrack", commitStart, StringComparison.Ordinal);
        var commit = editor[commitStart..commitEnd];
        var lockGate = commit.IndexOf("if (Timeline.IsTrackLocked(track))", StringComparison.Ordinal);
        var revalidate = commit.IndexOf("MediaImportService.RevalidateDuplicateResults", StringComparison.Ordinal);
        var seek = commit.IndexOf("SeekPreviewAndTimeline(positionMilliseconds);", StringComparison.Ordinal);
        var modelCommit = commit.IndexOf("ViewModel.AddImportedAssetsToTimeline", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, lockGate);
        Assert.IsGreaterThan(lockGate, revalidate);
        Assert.IsGreaterThan(lockGate, seek);
        Assert.IsGreaterThan(lockGate, modelCommit);
    }

    [TestMethod]
    public void TimelineTrackState_RejectsLockedTrackBeforeModelCommitAndRestoresCanonicalUi()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var handlerStart = editor.IndexOf("private void Timeline_TrackStateChanged", StringComparison.Ordinal);
        var handlerEnd = editor.IndexOf("private void Timeline_AssetDropped", handlerStart, StringComparison.Ordinal);
        var handler = editor[handlerStart..handlerEnd];
        var lockGate = handler.IndexOf("if (Timeline.IsTrackLocked(track))", StringComparison.Ordinal);
        var restore = handler.IndexOf("UpdateProjectPresentation();", lockGate, StringComparison.Ordinal);

        Assert.Contains("TimelineTrackState.VideoVisibility => TimelineTrackKind.Video", handler);
        Assert.Contains("TimelineTrackState.TextVisibility => TimelineTrackKind.Text", handler);
        Assert.Contains("TimelineTrackState.AudioMute => TimelineTrackKind.Audio", handler);
        Assert.IsGreaterThanOrEqualTo(0, lockGate);
        Assert.IsGreaterThan(lockGate, restore);
        Assert.IsGreaterThan(restore, handler.IndexOf("ViewModel.SetVideoTrackVisible", StringComparison.Ordinal));
        Assert.IsGreaterThan(restore, handler.IndexOf("ViewModel.SetTextTrackVisible", StringComparison.Ordinal));
        Assert.IsGreaterThan(restore, handler.IndexOf("ViewModel.SetAudioTrackMuted", StringComparison.Ordinal));
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

        Assert.IsGreaterThanOrEqualTo(0, updateWidth);
        Assert.IsGreaterThan(updateWidth, updateLayout);
        Assert.IsGreaterThan(updateLayout, changeView);
    }

    [TestMethod]
    public void ExplicitSeeks_EnsureThePlayheadIsVisibleEvenWhenPlaybackIsPaused()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var helperStart = editor.IndexOf("private void SeekPreviewAndTimeline", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, helperStart);
        var helperEnd = editor.IndexOf("private void Preview_PlaybackPositionChanged", helperStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(helperStart, helperEnd);
        var helper = editor[helperStart..helperEnd];
        Assert.Contains("Preview.Seek(positionMilliseconds);", helper);
        Assert.Contains("ViewModel.Seek(positionMilliseconds);", helper);
        Assert.Contains("ensureVisible: true", helper);
        Assert.Contains("SeekPreviewAndTimeline(e.PositionMilliseconds);", editor);
    }

    [TestMethod]
    public void PreviewRebuild_UsesTheModelPlayheadWhenNoNativeSourceExistsYet()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var rebuildStart = editor.IndexOf("private async Task RebuildPreviewAsync", StringComparison.Ordinal);
        var rebuildEnd = editor.IndexOf("private void EditorView_SizeChanged", rebuildStart, StringComparison.Ordinal);
        var rebuild = editor[rebuildStart..rebuildEnd];

        var modelPosition = rebuild.IndexOf("var requestedPosition = ViewModel.PlayheadMilliseconds;", StringComparison.Ordinal);
        var replace = rebuild.IndexOf("Preview.ReplaceComposition", StringComparison.Ordinal);
        var positionArgument = rebuild.IndexOf("requestedPosition,", replace, StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, modelPosition);
        Assert.IsGreaterThan(modelPosition, replace);
        Assert.IsGreaterThan(replace, positionArgument);
    }

    [TestMethod]
    public void PreviewRebuild_UpdatesOnlyItsOwnCurrentInfoBarMessage()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var rebuildStart = editor.IndexOf("private async Task RebuildPreviewAsync", StringComparison.Ordinal);
        var rebuildEnd = editor.IndexOf("private void EditorView_SizeChanged", rebuildStart, StringComparison.Ordinal);
        var rebuild = editor[rebuildStart..rebuildEnd];
        var showMessage = editor.IndexOf("private bool TryShowPreviewMessage", StringComparison.Ordinal);
        var clearMessage = editor.IndexOf("private void ClearPreviewMessage", StringComparison.Ordinal);

        Assert.Contains("CompositionBuildResult.SelectPreviewErrors(result.Errors)", rebuild);
        Assert.Contains("if (!TryShowPreviewMessage(", rebuild);
        Assert.Contains("_publishedPreviewKey = null;", rebuild);
        Assert.Contains("InfoBarSeverity.Warning", rebuild);
        Assert.Contains("ClearPreviewMessage();", rebuild);
        Assert.Contains("TryShowPreviewMessage(publication, InfoBarSeverity.Error", rebuild);
        Assert.IsGreaterThanOrEqualTo(0, showMessage);
        Assert.IsGreaterThan(showMessage, clearMessage);
        Assert.Contains("_previewInfoBarIsCurrent = true;", editor[showMessage..clearMessage]);
        Assert.Contains("if (!_previewInfoBarIsCurrent)", editor[clearMessage..]);
    }

    [TestMethod]
    public void AsyncInfoBarPublication_RejectsDisposedStaleOrLessRelevantUpdates()
    {
        var publication = new CutFlow.Views.InfoBarPublication(7);

        Assert.IsTrue(CutFlow.Views.EditorView.CanPublishInfoBar(
            disposed: false,
            lifetimeCanceled: false,
            currentRevision: 7,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
            publication,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success));
        Assert.IsFalse(CutFlow.Views.EditorView.CanPublishInfoBar(
            disposed: true,
            lifetimeCanceled: false,
            currentRevision: 7,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success,
            publication,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error));
        Assert.IsFalse(CutFlow.Views.EditorView.CanPublishInfoBar(
            disposed: false,
            lifetimeCanceled: true,
            currentRevision: 7,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success,
            publication,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error));
        Assert.IsFalse(CutFlow.Views.EditorView.CanPublishInfoBar(
            disposed: false,
            lifetimeCanceled: false,
            currentRevision: 8,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
            publication,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success));
        Assert.IsTrue(CutFlow.Views.EditorView.CanPublishInfoBar(
            disposed: false,
            lifetimeCanceled: false,
            currentRevision: 8,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
            publication,
            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error));
    }

    [TestMethod]
    public void AsyncUserVisibleResults_UseTheInfoBarPublicationGuard()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var export = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.Export.cs"));
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "MainWindow.xaml.cs"));

        Assert.Contains("publication = CaptureInfoBarPublication();", editor);
        Assert.Contains("TryShowProjectSaveFailure(", editor);
        Assert.Contains("TryShowPreviewMessage(", editor);
        Assert.Contains("TryShowMessage(publication, InfoBarSeverity.Error", editor);
        Assert.Contains("TryShowMessage(publication, InfoBarSeverity.Success", editor);
        Assert.Contains("TryShowMessage(publication, InfoBarSeverity.Error", export);
        Assert.Contains("TryShowMessage(publication, InfoBarSeverity.Informational", export);
        Assert.Contains("_workspaceSettingsSaveEditor = _editorView;", mainWindow);
        Assert.Contains("state != DebouncedSaveState.SaveFailed || noticeTargetsCurrentView", mainWindow);
        Assert.Contains("ReferenceEquals(editor, _workspaceSettingsSaveEditor)", mainWindow);
        Assert.Contains("editor.ReportError(_workspaceSettingsSavePublication", mainWindow);
        Assert.AreEqual(
            2,
            mainWindow.Split("_workspaceSettingsSaveEditor = null;", StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public void TimelineWheelZoom_IsScopedInsideTheScrollerBeforeBuiltInWheelHandling()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));

        Assert.Contains("PointerWheelChanged=\"TimelineContent_PointerWheelChanged\"", xaml);
        Assert.DoesNotContain("UIElement.PointerWheelChangedEvent", source);
    }

    [TestMethod]
    public void TimelineSnappingToggle_UpdatesItsAccessibleActionName()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml"));
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var changedStart = source.IndexOf("private void SnappingButton_Changed", StringComparison.Ordinal);
        var changedEnd = source.IndexOf("private void SelectionToolButton_Click", changedStart, StringComparison.Ordinal);
        var changed = source[changedStart..changedEnd];

        Assert.Contains("AutomationProperties.Name=\"Disable timeline snapping\"", xaml);
        Assert.Contains("\"Disable timeline snapping\"", changed);
        Assert.Contains("\"Enable timeline snapping\"", changed);
        Assert.Contains("AutomationProperties.SetName(SnappingButton, name);", changed);
    }

    [TestMethod]
    public void TimelineSelection_UpdatesVisualAndAutomationStateForDynamicCards()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "TimelineControl.xaml.cs"));
        var addStart = source.IndexOf("private void AddClip", StringComparison.Ordinal);
        var addEnd = source.IndexOf("private void Clip_GotFocus", addStart, StringComparison.Ordinal);
        var updateStart = source.IndexOf("private void ApplySelectionStyle", StringComparison.Ordinal);
        var updateEnd = source.IndexOf("private bool IsSelected", updateStart, StringComparison.Ordinal);
        var add = source[addStart..addEnd];
        var update = source[updateStart..updateEnd];

        Assert.Contains("BorderBrush = Brush(selected ? \"AccentBrush\" : \"BorderBrush\")", source);
        Assert.Contains("BorderThickness = new Thickness(selected ? 2 : 1)", source);
        Assert.Contains("Background = Brush(selected ? \"SurfaceHoverBrush\" : \"SurfaceElevatedBrush\")", source);
        Assert.Contains("AutomationProperties.SetItemStatus(hitTarget, IsSelected(bound) ? \"Selected\" : string.Empty);", add);
        Assert.Contains("AutomationProperties.SetItemStatus(hitTarget, selected ? \"Selected\" : string.Empty);", update);
    }

    [TestMethod]
    public void PreviewMuteToggle_UpdatesItsAccessibleActionName()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "PreviewPane.xaml.cs"));
        var muteStart = source.IndexOf("public void Mute(bool isMuted)", StringComparison.Ordinal);
        var muteEnd = source.IndexOf("public void Loop(bool loop)", muteStart, StringComparison.Ordinal);
        var mute = source[muteStart..muteEnd];

        Assert.Contains("\"Unmute preview\"", mute);
        Assert.Contains("\"Mute preview\"", mute);
        Assert.Contains("AutomationProperties.SetName(MuteButton, name);", mute);
        Assert.Contains("ToolTipService.SetToolTip(MuteButton, name);", mute);
    }

    [TestMethod]
    public void InspectorItemMuteToggles_UpdateTheirActionNamesAfterRebindingAndClicks()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "InspectorPanel.xaml.cs"));
        var videoStart = source.IndexOf("private bool TryShowVideoSelection", StringComparison.Ordinal);
        var audioStart = source.IndexOf("private bool TryShowAudioSelection", videoStart, StringComparison.Ordinal);
        var textStart = source.IndexOf("private bool TryShowTextSelection", audioStart, StringComparison.Ordinal);
        var clickStart = source.IndexOf("private void MuteBox_Click", StringComparison.Ordinal);
        var presentationStart = source.IndexOf("private static void UpdateMuteBoxPresentation", clickStart, StringComparison.Ordinal);
        var presentationEnd = source.IndexOf("private void TextFontFamilyBox_SelectionChanged", presentationStart, StringComparison.Ordinal);
        var videoRefresh = source[videoStart..audioStart];
        var audioRefresh = source[audioStart..textStart];
        var click = source[clickStart..presentationStart];
        var presentation = source[presentationStart..presentationEnd];

        Assert.Contains("UpdateMuteBoxPresentation(VideoMuteBox, video.IsMuted, \"video\");", videoRefresh);
        Assert.Contains("UpdateMuteBoxPresentation(AudioMuteBox, audio.IsMuted, \"audio\");", audioRefresh);
        Assert.Contains("UpdateMuteBoxPresentation(checkBox, isMuted", click);
        Assert.Contains("isMuted ? \"Unmute\" : \"Mute\"", presentation);
        Assert.Contains("checkBox.Content = action;", presentation);
        Assert.Contains("AutomationProperties.SetName(checkBox, name);", presentation);
        Assert.Contains("ToolTipService.SetToolTip(checkBox, name);", presentation);
    }

    [TestMethod]
    public void PreviewLoopToggle_UpdatesItsAccessibleActionName()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "PreviewPane.xaml.cs"));
        var loopStart = source.IndexOf("public void Loop(bool loop)", StringComparison.Ordinal);
        var loopEnd = source.IndexOf("public void SetCanPlay(bool canPlay)", loopStart, StringComparison.Ordinal);
        var loop = source[loopStart..loopEnd];
        var clickStart = source.IndexOf("private void LoopButton_Click", StringComparison.Ordinal);
        var clickEnd = source.IndexOf("private void FitButton_Click", clickStart, StringComparison.Ordinal);

        Assert.Contains("\"Disable preview looping\"", loop);
        Assert.Contains("\"Loop preview\"", loop);
        Assert.Contains("AutomationProperties.SetName(LoopButton, name);", loop);
        Assert.Contains("ToolTipService.SetToolTip(LoopButton, name);", loop);
        Assert.Contains("UpdateLoopButtonPresentation(LoopButton.IsChecked == true);", source[clickStart..clickEnd]);
    }

    [TestMethod]
    public void PreviewPane_OwnsOnePlayerAndOneQueueTimerForItsLifetime()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "PreviewPane.xaml.cs"));
        var constructorStart = source.IndexOf("public PreviewPane()", StringComparison.Ordinal);
        var constructorEnd = source.IndexOf("public event EventHandler<PlaybackChangedEventArgs>", constructorStart, StringComparison.Ordinal);
        var replaceStart = source.IndexOf("public void ReplaceComposition", StringComparison.Ordinal);
        var replaceEnd = source.IndexOf("internal static void RunSuccessfulSourceSwap", replaceStart, StringComparison.Ordinal);
        var playbackStateStart = source.IndexOf("private void ApplyActualPlaybackState", StringComparison.Ordinal);
        var playbackStateEnd = source.IndexOf("private void PositionTimer_Tick", playbackStateStart, StringComparison.Ordinal);
        var disposeStart = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
        var disposeEnd = source.IndexOf("private void AttachPlayerEvents", disposeStart, StringComparison.Ordinal);

        Assert.IsTrue(constructorStart >= 0 && constructorEnd > constructorStart);
        Assert.IsTrue(replaceStart >= 0 && replaceEnd > replaceStart);
        Assert.IsTrue(playbackStateStart >= 0 && playbackStateEnd > playbackStateStart);
        Assert.IsTrue(disposeStart >= 0 && disposeEnd > disposeStart);

        var constructor = source[constructorStart..constructorEnd];
        var replace = source[replaceStart..replaceEnd];
        var playbackState = source[playbackStateStart..playbackStateEnd];
        var dispose = source[disposeStart..disposeEnd];

        Assert.AreEqual(1, source.Split("private readonly MediaPlayer _player = new();", StringSplitOptions.None).Length - 1);
        Assert.Contains("private readonly DispatcherQueueTimer _positionTimer;", source);
        Assert.AreEqual(1, source.Split("DispatcherQueue.CreateTimer();", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("DispatcherTimer", source);
        Assert.IsLessThan(constructor.IndexOf("DispatcherQueue.CreateTimer();", StringComparison.Ordinal), constructor.IndexOf("InitializeComponent();", StringComparison.Ordinal));
        Assert.Contains("Interval = TimeSpan.FromMilliseconds(33);", constructor);
        Assert.Contains("_positionTimer.Tick += PositionTimer_Tick;", constructor);
        Assert.DoesNotContain("new MediaPlayer", replace);
        Assert.DoesNotContain("CreateTimer", replace);
        var reportActual = playbackState.IndexOf("_playbackState.ReportActual(", StringComparison.Ordinal);
        var updateTimer = playbackState.IndexOf("if (playing) _positionTimer.Start(); else _positionTimer.Stop();", StringComparison.Ordinal);
        var updateIcon = playbackState.IndexOf("SetPlaying(playing);", StringComparison.Ordinal);
        var reportViewModel = playbackState.IndexOf("PlayPauseRequested?.Invoke", StringComparison.Ordinal);
        Assert.IsTrue(reportActual >= 0 && updateTimer > reportActual && updateIcon > updateTimer && reportViewModel > updateIcon);
        Assert.Contains("_positionTimer.Stop();", dispose);
        Assert.Contains("_positionTimer.Tick -= PositionTimer_Tick;", dispose);
        Assert.Contains("DetachPlayerEvents();", dispose);
        var markDisposed = dispose.IndexOf("_disposed = true;", StringComparison.Ordinal);
        var invalidateGeneration = dispose.IndexOf("_playerEventGeneration.Advance();", StringComparison.Ordinal);
        var detachEvents = dispose.IndexOf("DetachPlayerEvents();", StringComparison.Ordinal);
        Assert.IsTrue(markDisposed >= 0 && invalidateGeneration > markDisposed && detachEvents > invalidateGeneration);
        Assert.Contains("() => PlayerElement.SetMediaPlayer(null)", dispose);
        Assert.Contains("() => _source?.Dispose()", dispose);
        Assert.Contains("() => _player.Dispose()", dispose);
    }

    [TestMethod]
    public void PreviewMediaEnded_ManualLoopHonorsCurrentIntentAndPublishesCoherentPosition()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "PreviewPane.xaml.cs"));
        var handlerStart = source.IndexOf("private void Player_MediaEnded", StringComparison.Ordinal);
        var handlerEnd = source.IndexOf("private void ApplyActualPlaybackState", handlerStart, StringComparison.Ordinal);

        Assert.IsTrue(handlerStart >= 0 && handlerEnd > handlerStart);
        var handler = source[handlerStart..handlerEnd];
        var generationCheck = handler.IndexOf("_playerEventGeneration.IsCurrent(generation)", StringComparison.Ordinal);
        var generationRun = handler.IndexOf("_playerEventGeneration.TryRun(generation", StringComparison.Ordinal);
        var restartCheck = handler.IndexOf("PreviewPlaybackPolicy.ShouldRestartAtEnd", StringComparison.Ordinal);
        var seekToStart = handler.IndexOf("sender.PlaybackSession.Position = TimeSpan.Zero;", StringComparison.Ordinal);
        var publishStart = handler.IndexOf("new PlayheadChangedEventArgs(0)", StringComparison.Ordinal);
        var resume = handler.IndexOf("sender.Play();", StringComparison.Ordinal);
        var stopTimer = handler.IndexOf("_positionTimer.Stop();", StringComparison.Ordinal);
        var clearIntent = handler.IndexOf("_playbackState.SetIntent(false);", StringComparison.Ordinal);
        var pause = handler.IndexOf("sender.Pause();", StringComparison.Ordinal);
        var publishEnd = handler.IndexOf("new PlayheadChangedEventArgs(PositionMilliseconds)", StringComparison.Ordinal);

        Assert.IsTrue(generationCheck >= 0 && generationRun > generationCheck && restartCheck > generationRun);
        Assert.Contains("_playbackState.PlayIntent", handler[restartCheck..seekToStart]);
        Assert.DoesNotContain("_playbackState.SetIntent(true);", handler);
        Assert.IsTrue(seekToStart > restartCheck && publishStart > seekToStart && resume > publishStart);
        Assert.IsTrue(stopTimer > resume && clearIntent > stopTimer && pause > clearIntent && publishEnd > pause);
        Assert.IsGreaterThanOrEqualTo(2, source.Split("_player.IsLoopingEnabled = false;", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("_player.IsLoopingEnabled = true;", source);
    }

    [TestMethod]
    public void PreviewMediaFailure_IsGenerationGatedStopsIntentAndSurfacesAReadableError()
    {
        var root = FindRepositoryRoot();
        var preview = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Controls", "PreviewPane.xaml.cs"));
        var editor = File.ReadAllText(Path.Combine(root, "src", "CutFlow", "Views", "EditorView.xaml.cs"));
        var handlerStart = preview.IndexOf("private void Player_MediaFailed", StringComparison.Ordinal);
        var handlerEnd = preview.IndexOf("private void ApplyActualPlaybackState", handlerStart, StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, handlerStart);
        Assert.IsGreaterThan(handlerStart, handlerEnd);
        var handler = preview[handlerStart..handlerEnd];
        Assert.Contains("_playerEventGeneration.IsCurrent(generation)", handler);
        Assert.Contains("_playerEventGeneration.TryRun(generation", handler);
        Assert.Contains("_positionTimer.Stop();", handler);
        Assert.Contains("_playbackState.SetIntent(false);", handler);
        Assert.Contains("ApplyActualPlaybackState(isPlaying: false);", handler);
        Assert.Contains("PlaybackFailed?.Invoke", handler);
        Assert.Contains("_player.MediaFailed += _mediaFailedHandler;", preview);
        Assert.Contains("_player.MediaFailed -= _mediaFailedHandler;", preview);
        Assert.Contains("Preview.PlaybackFailed += Preview_PlaybackFailed;", editor);
        Assert.Contains("Preview.PlaybackFailed -= Preview_PlaybackFailed;", editor);
        Assert.Contains("require a codec that is not installed", editor);
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
    public void AssetSearchAndRealization_PublishOneVirtualizedListAndUseGuidLookup()
    {
        var mediaPanel = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "MediaPanel.xaml.cs"));
        var refreshStart = mediaPanel.IndexOf("public void RefreshAssets()", StringComparison.Ordinal);
        var refreshEnd = mediaPanel.IndexOf("public void SetTool", refreshStart, StringComparison.Ordinal);
        var refresh = mediaPanel[refreshStart..refreshEnd];
        var realizationStart = mediaPanel.IndexOf("private void AssetGrid_ContainerContentChanging", StringComparison.Ordinal);
        var realizationEnd = mediaPanel.IndexOf("private MenuFlyout CreateAssetContextMenu", realizationStart, StringComparison.Ordinal);
        var realization = mediaPanel[realizationStart..realizationEnd];

        Assert.Contains("AssetGrid.ItemsSource = _cards;", refresh);
        Assert.DoesNotContain("_cards.Add", refresh);
        Assert.Contains("_project.Assets.ToDictionary", refresh);
        Assert.Contains("_assetsById.TryGetValue", realization);
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
        var loadEnd = timeline.IndexOf("private static string FormatDuration", loadStart, StringComparison.Ordinal);
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
    public void TimelineRendering_UsesBufferedViewportVirtualizationAndIndexedVideoLookup()
    {
        var timeline = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CutFlow",
            "Controls",
            "TimelineControl.xaml.cs"));
        var renderStart = timeline.IndexOf("private void RenderClips()", StringComparison.Ordinal);
        var renderEnd = timeline.IndexOf("private Border CreateVideoCard", renderStart, StringComparison.Ordinal);
        var render = timeline[renderStart..renderEnd];

        Assert.Contains("_project.VideoItems.ToDictionary", render);
        Assert.Contains("IsClipVisible(bound)", render);
        Assert.Contains("ClipWindowContainsViewport()", timeline);
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

        Assert.IsGreaterThanOrEqualTo(0, editorPreparation);
        Assert.IsGreaterThan(editorPreparation, capture);
        Assert.IsGreaterThan(capture, save);
        Assert.IsGreaterThan(save, finalEditorFlush);
        Assert.IsGreaterThan(finalEditorFlush, approval);
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

        Assert.IsGreaterThanOrEqualTo(0, displayLookup);
        Assert.IsGreaterThan(displayLookup, clamp);
        Assert.IsGreaterThan(clamp, persistence);
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

        Assert.IsGreaterThanOrEqualTo(0, failureCatch);
        Assert.IsGreaterThan(failureCatch, consumeQueuedNotice);
        Assert.IsGreaterThan(consumeQueuedNotice, reportCloseFailure);
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
