using System.Diagnostics;
using System.Runtime.InteropServices;
using CutFlow.Controls;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;
using Windows.Storage;

namespace CutFlow.Views;

public sealed partial class EditorView : UserControl, IDisposable
{
    private const string CouldNotAddMediaTitle = "Could not add media";
    private const string ExportInProgressTitle = "Export in progress";
    private const double InspectorCollapseWidth = 1320;
    private readonly Dictionary<EditorTool, Button> _toolButtons;
    private readonly MediaImportService _mediaImportService;
    private readonly ProjectService _projectService;
    private readonly SimpleLogService _logService;
    private readonly ThumbnailService _thumbnailService;
    private readonly string _projectRootPath;
    private readonly string _projectsRootPath;
    private readonly EditorImportGate _importGate = new();
    private readonly CompositionService _compositionService = new();
    private readonly PreviewRebuildGate _previewRebuildGate = new();
    private PreviewCompositionKey? _publishedPreviewKey;
    private readonly TextOverlayRenderer _textOverlayRenderer;
    private readonly DebouncedSaveCoordinator _autosave;
    private readonly AppSettings _workspaceSettings;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _isNarrow;
    private bool _wideInspectorVisible = true;
    private bool _narrowInspectorVisible;
    private InspectorFocusSnapshot? _pendingInspectorFocus;
    private UIElement? _timelineResizeCaptureElement;
    private Pointer? _timelineResizePointer;
    private uint _timelineResizePointerId;
    private double _timelineResizeStartY;
    private double _timelineResizeStartHeight;
    private bool _projectSaveFailureInfoBarIsCurrent;
    private bool _previewInfoBarIsCurrent;
    private InspectorPanel? _committingInspector;
    private long _infoBarRevision;
    private InfoBarSeverity _infoBarSeverity;
    private InfoBarPublication? _autosaveInfoBarPublication;
    private bool _disposed;

    public EditorView(
        EditorViewModel viewModel,
        ProjectService projectService,
        MediaImportService mediaImportService,
        SimpleLogService logService,
        AppSettings? workspaceSettings = null)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _mediaImportService = mediaImportService ?? throw new ArgumentNullException(nameof(mediaImportService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _workspaceSettings = AppSettings.Normalize(workspaceSettings);
        _lifetimeToken = _lifetimeCts.Token;
        _projectRootPath = projectService.GetProjectPath(viewModel.Project.Id);
        _projectsRootPath = projectService.GetProjectsPath();
        _thumbnailService = new ThumbnailService(_projectRootPath);
        _autosave = new DebouncedSaveCoordinator(() => _projectService.SaveAsync(ViewModel.Project, _lifetimeToken));
        _autosave.StateChanged += Autosave_StateChanged;
        InitializeComponent();
        ProjectNameBox.MaxLength = ProjectDocument.MaximumNameLength;
        Timeline.SetLifetimeToken(_lifetimeToken);
        _textOverlayRenderer = new TextOverlayRenderer(_projectRootPath, Preview.RenderHost);
        _toolButtons = new Dictionary<EditorTool, Button>
        {
            [EditorTool.Media] = MediaToolButton,
            [EditorTool.Audio] = AudioToolButton,
            [EditorTool.Text] = TextToolButton,
            [EditorTool.Stickers] = StickersToolButton,
            [EditorTool.Effects] = EffectsToolButton,
            [EditorTool.Transitions] = TransitionsToolButton,
            [EditorTool.Captions] = CaptionsToolButton,
            [EditorTool.Filters] = FiltersToolButton,
            [EditorTool.Adjustment] = AdjustmentToolButton
        };
        ApplyWorkspaceSettings(_workspaceSettings);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.EditCommitted += ViewModel_EditCommitted;
        ViewModel.SelectionChanged += ViewModel_SelectionChanged;
        ToolPanel.SetTool(ViewModel.SelectedTool);
        ToolPanel.SetProject(ViewModel.Project, _thumbnailService);
        ToolPanel.AddAssetRequested += ToolPanel_AddAssetRequested;
        ToolPanel.RelinkAssetRequested += ToolPanel_RelinkAssetRequested;
        ToolPanel.ShowAssetInExplorerRequested += ToolPanel_ShowAssetInExplorerRequested;
        ToolPanel.RemoveAssetRequested += ToolPanel_RemoveAssetRequested;
        ToolPanel.MediaFilesDropped += ToolPanel_MediaFilesDropped;
        ToolPanel.MediaDropFailed += ToolPanel_MediaDropFailed;
        Preview.PlaybackPositionChanged += Preview_PlaybackPositionChanged;
        Preview.PlaybackFailed += Preview_PlaybackFailed;
        Preview.TextPositionCommitted += Preview_TextPositionCommitted;
        Preview.LoopChanged += Preview_LoopChanged;
        Timeline.ZoomChanged += Timeline_ZoomChanged;
        Preview.SetCanPlay(false);
        UpdateToolSelection();
        UpdateProjectPresentation();
        ShowInitialMissingStatus();
    }

    public event EventHandler? ReturnHomeRequested;
    public event EventHandler<MediaImportRequestedEventArgs>? ImportRequested;
    public event EventHandler<AssetActionEventArgs>? RelinkAssetRequested;
    public event EventHandler? AddTextRequested;
    public event EventHandler? ExportRequested;
    public event EventHandler? NewProjectRequested;
    public event EventHandler<WorkspaceSettingsChangedEventArgs>? WorkspaceSettingsChanged;

    public EditorViewModel ViewModel { get; }

    public UIElement TitleBarElement => TitleBarDragRegion;

    public bool FocusInitialControl() => BackButton.Focus(FocusState.Programmatic);

    public double TimelineHeight => TimelineRow.Height.Value;

    public void SetTimelineHeight(double height) => SetTimelineHeight(height, notify: true);

    public Task ImportFilesAsync(IReadOnlyList<StorageFile> files) =>
        ImportFilesAsync(files, new ImportResult?[files.Count]);

    private async Task ImportFilesAsync(
        IReadOnlyList<StorageFile> files,
        IReadOnlyList<ImportResult?> resultSlots)
    {
        EnsureActive();
        if (files.Count == 0)
        {
            var rejected = resultSlots.OfType<ImportResult>().ToList();
            if (rejected.Count > 0)
            {
                ShowImportResults(rejected);
            }

            return;
        }

        InfoBarPublication publication = default;
        await _importGate.ExecuteAsync(
            cancellationToken =>
            {
                publication = CaptureInfoBarPublication();
                return _mediaImportService.ImportAsync(files, ViewModel.Project, _projectsRootPath, cancellationToken);
            },
            results => CommitImportResults(MediaImportService.MergeImportResults(resultSlots, results), publication),
            _lifetimeToken);
    }

    private async Task ImportFilesToTimelineAsync(
        IReadOnlyList<StorageFile> files,
        TimelineTrackKind track,
        long positionMilliseconds,
        IReadOnlyList<string> preflightRejections)
    {
        EnsureActive();
        if (files.Count == 0)
        {
            return;
        }

        if (Timeline.IsTrackLocked(track))
        {
            ShowMessage(
                InfoBarSeverity.Warning,
                $"{TimelineDropPolicy.DisplayName(track)} is locked",
                "Unlock the track before adding media.");
            return;
        }

        var accepted = new List<StorageFile>(files.Count);
        var rejected = new List<string>(preflightRejections);
        foreach (var file in files)
        {
            if (!MediaImportService.TryResolveLocalSourcePath(file.Path, out var normalizedPath, out var pathError))
            {
                rejected.Add($"'{file.Name}': {pathError}");
                continue;
            }

            if (!MediaImportService.TryGetKind(normalizedPath, out var kind))
            {
                rejected.Add($"'{file.Name}' is not a supported media file.");
                continue;
            }

            var decision = TimelineDropPolicy.Evaluate(track, kind, isLocked: false);
            if (!decision.IsAllowed)
            {
                rejected.Add(IncompatibleDropMessage(file.Name, kind, track));
                continue;
            }

            accepted.Add(file);
        }

        if (accepted.Count == 0)
        {
            ShowMessage(
                InfoBarSeverity.Warning,
                "No files were added",
                string.Join(" ", rejected.Take(3)));
            return;
        }

        InfoBarPublication publication = default;
        await _importGate.ExecuteAsync(
            cancellationToken =>
            {
                publication = CaptureInfoBarPublication();
                return _mediaImportService.ImportAsync(accepted, ViewModel.Project, _projectsRootPath, cancellationToken);
            },
            results => CommitTimelineDropResults(accepted, results, track, positionMilliseconds, rejected, publication),
            _lifetimeToken);
    }

    private void CommitTimelineDropResults(
        List<StorageFile> files,
        IReadOnlyList<ImportResult> results,
        TimelineTrackKind track,
        long positionMilliseconds,
        IReadOnlyList<string> rejected,
        InfoBarPublication publication)
    {
        EnsureActive();
        if (Timeline.IsTrackLocked(track))
        {
            ShowMessage(
                InfoBarSeverity.Warning,
                $"{TimelineDropPolicy.DisplayName(track)} is locked",
                "Unlock the track before adding media.");
            return;
        }

        results = MediaImportService.RevalidateDuplicateResults(ViewModel.Project, results);
        var imported = results
            .Where(result => result.Asset is not null)
            .Select(result => result.Asset!)
            .ToList();

        var issues = new List<string>(rejected);
        var candidates = CollectDropCandidates(files, results, imported, track, issues);
        if (track == TimelineTrackKind.Audio)
        {
            SeekPreviewAndTimeline(positionMilliseconds);
        }

        var additions = ViewModel.AddImportedAssetsToTimeline(
            imported,
            candidates.Select(asset => asset.Id).ToList());
        AddTimelineInsertionIssues(candidates, additions, issues);
        ToolPanel.RefreshAssets();
        UpdateProjectPresentation();
        ShowTimelineDropSummary(track, additions.Count(wasAdded => wasAdded), issues, publication);
    }

    private List<ProjectAsset> CollectDropCandidates(
        List<StorageFile> files,
        IReadOnlyList<ImportResult> results,
        IReadOnlyList<ProjectAsset> imported,
        TimelineTrackKind track,
        List<string> issues)
    {
        var candidates = new List<ProjectAsset>();
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var asset = result.Asset ??
                (result.IsDuplicate && index < files.Count
                    ? FindAssetBySourcePath(files[index].Path, imported)
                    : null);
            if (asset is null)
            {
                if (!result.IsDuplicate)
                {
                    issues.Add($"'{result.FileName}': {result.ErrorMessage}");
                }
                else
                {
                    issues.Add($"'{result.FileName}' is already imported but could not be found.");
                }

                continue;
            }

            var decision = TimelineDropPolicy.Evaluate(track, asset.Kind, Timeline.IsTrackLocked(track));
            if (!decision.IsAllowed)
            {
                issues.Add(decision.Failure == TimelineDropFailure.LockedTrack
                    ? $"{TimelineDropPolicy.DisplayName(track)} is locked. Unlock the track before adding media."
                    : IncompatibleDropMessage(asset.FileName, asset.Kind, track));
                continue;
            }

            candidates.Add(asset);
        }

        return candidates;
    }

    private static void AddTimelineInsertionIssues(
        List<ProjectAsset> candidates,
        IReadOnlyList<bool> additions,
        List<string> issues)
    {
        for (var index = 0; index < additions.Count; index++)
        {
            if (!additions[index])
            {
                var asset = candidates[index];
                issues.Add(asset.IsMissing
                    ? $"'{asset.FileName}' is missing. Relink it before adding it."
                    : $"'{asset.FileName}' is too short or invalid.");
            }
        }
    }

    private void ShowTimelineDropSummary(
        TimelineTrackKind track,
        int added,
        List<string> issues,
        InfoBarPublication publication)
    {
        var trackName = TimelineDropPolicy.DisplayName(track);
        if (issues.Count == 0)
        {
            TryShowMessage(
                publication,
                InfoBarSeverity.Success,
                $"Added to {trackName}",
                $"Added {added} file(s) to the timeline.");
        }
        else
        {
            TryShowMessage(
                publication,
                InfoBarSeverity.Warning,
                added > 0 ? $"Added {added} file(s) with issues" : "No files were added",
                string.Join(" ", issues.Take(3)) + (issues.Count > 3 ? $" {issues.Count - 3} more files were not added." : string.Empty));
        }
    }

    private bool TryAddAssetToTrack(
        ProjectAsset asset,
        TimelineTrackKind track,
        long positionMilliseconds,
        out string? error)
    {
        var decision = TimelineDropPolicy.Evaluate(track, asset.Kind, Timeline.IsTrackLocked(track));
        if (!decision.IsAllowed)
        {
            error = decision.Failure == TimelineDropFailure.LockedTrack
                ? $"{TimelineDropPolicy.DisplayName(track)} is locked. Unlock the track before adding media."
                : IncompatibleDropMessage(asset.FileName, asset.Kind, track);
            return false;
        }

        if (track == TimelineTrackKind.Audio)
        {
            SeekPreviewAndTimeline(positionMilliseconds);
        }

        if (!ViewModel.AddAssetToTimeline(asset.Id))
        {
            error = asset.IsMissing
                ? $"'{asset.FileName}' is missing. Relink it before adding it."
                : $"'{asset.FileName}' is too short or invalid.";
            return false;
        }

        UpdateProjectPresentation();
        error = null;
        return true;
    }

    private ProjectAsset? FindAssetBySourcePath(string path, IEnumerable<ProjectAsset>? additionalAssets = null)
    {
        try
        {
            var normalized = MediaImportService.NormalizePath(path);
            return ViewModel.Project.Assets.Concat(additionalAssets ?? []).FirstOrDefault(asset =>
                !string.IsNullOrWhiteSpace(asset.SourcePath) &&
                string.Equals(
                    MediaImportService.NormalizePath(asset.SourcePath),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string IncompatibleDropMessage(
        string fileName,
        ProjectAssetKind assetKind,
        TimelineTrackKind track) => track switch
        {
            TimelineTrackKind.Video when assetKind == ProjectAssetKind.Audio =>
                $"'{fileName}' is audio. Drop it on A1.",
            TimelineTrackKind.Audio =>
                $"'{fileName}' is visual media. Drop it on V1.",
            TimelineTrackKind.Text =>
                "Files cannot be dropped on T1. Add text from the Text panel.",
            _ => $"'{fileName}' cannot be added to this track."
        };

    public ProjectAsset? FindAsset(Guid assetId) => ViewModel.Project.Assets.FirstOrDefault(asset => asset.Id == assetId);

    public async Task<bool> SaveAsync()
    {
        var publication = CaptureInfoBarPublication();
        try
        {
            await _autosave.FlushAsync();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            TryShowProjectSaveFailure(publication, "Your latest edit could not be saved. Try again before closing the editor.");
            _ = _logService.TryWriteAsync($"Project save failed: {exception.GetType().Name}", CancellationToken.None);
            return false;
        }
    }

    public void ReportError(string title, string message)
    {
        if (!_disposed && !_lifetimeToken.IsCancellationRequested)
        {
            ShowMessage(InfoBarSeverity.Error, title, message);
        }
    }

    public async Task RelinkAssetAsync(Guid assetId, StorageFile replacement)
    {
        EnsureActive();
        var existing = FindAsset(assetId);
        if (existing is null)
        {
            ShowMessage(InfoBarSeverity.Error, "Relink failed", "The asset is no longer in this project.");
            return;
        }

        var candidate = CopyAsset(existing);
        var publication = CaptureInfoBarPublication();
        var result = await _mediaImportService.RelinkAsync(
            replacement,
            candidate,
            ViewModel.Project,
            _projectsRootPath,
            _lifetimeToken);
        EnsureActive();
        var oldCacheReference = FindAsset(assetId) is { } current ? CopyAsset(current) : CopyAsset(existing);
        if (!result.IsSuccess || !ViewModel.ApplyRelinkedAsset(candidate))
        {
            TryShowMessage(publication, InfoBarSeverity.Error, $"Could not relink '{replacement.Name}'", result.ErrorMessage ?? "The asset could not be updated.");
            return;
        }

        TryDeleteCache(oldCacheReference);
        ToolPanel.RefreshAssets();
        TryShowMessage(publication, InfoBarSeverity.Success, "Media relinked", $"'{candidate.FileName}' now replaces the missing or moved source while keeping timeline references.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelTimelineResize();
        _exportState.BeginClosing();
        _autosave.StateChanged -= Autosave_StateChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.EditCommitted -= ViewModel_EditCommitted;
        ViewModel.SelectionChanged -= ViewModel_SelectionChanged;
        ToolPanel.AddAssetRequested -= ToolPanel_AddAssetRequested;
        ToolPanel.RelinkAssetRequested -= ToolPanel_RelinkAssetRequested;
        ToolPanel.ShowAssetInExplorerRequested -= ToolPanel_ShowAssetInExplorerRequested;
        ToolPanel.RemoveAssetRequested -= ToolPanel_RemoveAssetRequested;
        ToolPanel.MediaFilesDropped -= ToolPanel_MediaFilesDropped;
        ToolPanel.MediaDropFailed -= ToolPanel_MediaDropFailed;
        Preview.PlaybackPositionChanged -= Preview_PlaybackPositionChanged;
        Preview.PlaybackFailed -= Preview_PlaybackFailed;
        Preview.TextPositionCommitted -= Preview_TextPositionCommitted;
        Preview.LoopChanged -= Preview_LoopChanged;
        Timeline.ZoomChanged -= Timeline_ZoomChanged;
        CleanupEditorResource(_autosave.Dispose, "autosave coordinator");
        CleanupEditorResource(_lifetimeCts.Cancel, "editor lifetime cancellation");
        CleanupEditorResource(_previewRebuildGate.Dispose, "preview rebuild cancellation");
        CleanupEditorResource(ToolPanel.Dispose, "thumbnail work");
        CleanupEditorResource(Timeline.CancelPointerInteraction, "timeline pointer capture");
        CleanupEditorResource(Timeline.StopThumbnailWork, "timeline thumbnail work");
        _textOverlayRenderer.RenderHost = null;
        CleanupEditorResource(Preview.Dispose, "native preview resources");
        _lifetimeCts.Dispose();
    }

    private void CleanupEditorResource(Action cleanup, string resource)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception) when (exception is AggregateException or COMException or InvalidOperationException or ObjectDisposedException)
        {
            _ = _logService.TryWriteAsync($"Editor {resource} cleanup failed: {exception.GetType().Name}", CancellationToken.None);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.PlayheadMilliseconds))
        {
            Preview.SetPlayhead(ViewModel.PlayheadText);
            Preview.SetTextItems(ViewModel.Project, ViewModel.Selection, ViewModel.PlayheadMilliseconds, ViewModel.Revision);
            Timeline.UpdatePlaybackPosition(ViewModel.PlayheadMilliseconds, ViewModel.PlayheadText, ViewModel.IsPlaying);
        }
        else if (e.PropertyName == nameof(EditorViewModel.Project))
        {
            UpdateProjectPresentation();
        }
        else if (e.PropertyName is nameof(EditorViewModel.CanUndo) or nameof(EditorViewModel.CanRedo))
        {
            UndoButton.IsEnabled = ViewModel.CanUndo;
            RedoButton.IsEnabled = ViewModel.CanRedo;
        }
    }

    private void ViewModel_EditCommitted(object? sender, EventArgs e)
    {
        ToolPanel.SetProject(ViewModel.Project, _thumbnailService);
        UpdateProjectPresentation();
        _ = RebuildPreviewAsync(debounce: true);
        _autosave.NotifyEdited();
    }

    private void Autosave_StateChanged(object? sender, EventArgs e)
    {
        var state = _autosave.State;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            switch (state)
            {
                case DebouncedSaveState.Saved:
                    ViewModel.MarkSaved();
                    ClearProjectSaveFailure();
                    _autosaveInfoBarPublication = null;
                    break;
                case DebouncedSaveState.Saving:
                    ViewModel.MarkSaving();
                    _autosaveInfoBarPublication = CaptureInfoBarPublication();
                    break;
                case DebouncedSaveState.SaveFailed:
                    ViewModel.MarkSaveFailed();
                    TryShowProjectSaveFailure(
                        _autosaveInfoBarPublication ?? CaptureInfoBarPublication(),
                        "Autosave failed. Try Ctrl+S before closing the editor.");
                    _ = _logService.TryWriteAsync("Project autosave failed.", CancellationToken.None);
                    break;
            }
        });
    }

    private void ViewModel_SelectionChanged(object? sender, EditorSelectionChangedEventArgs e)
    {
        _pendingInspectorFocus = null;
        DesktopInspector.SetSelection(e.Selection);
        NarrowInspector.SetSelection(e.Selection);
        Timeline.SetSelection(e.Selection);
        Preview.SetTextItems(ViewModel.Project, e.Selection, ViewModel.PlayheadMilliseconds, ViewModel.Revision);
    }

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || !Enum.TryParse<EditorTool>(element.Tag?.ToString(), out var tool))
        {
            return;
        }

        ViewModel.SelectedTool = tool;
        ToolPanel.SetTool(tool);
        UpdateToolSelection();
    }

    private void UpdateToolSelection()
    {
        foreach (var (tool, button) in _toolButtons)
        {
            var selected = tool == ViewModel.SelectedTool;
            button.Background = selected ? (Brush)Application.Current.Resources["SelectionBrush"] : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = (Brush)Application.Current.Resources[selected ? "AccentBrush" : "TextSecondaryBrush"];
            AutomationProperties.SetHelpText(button, selected ? "Selected" : string.Empty);
        }
    }

    private void UpdateProjectPresentation()
    {
        _pendingInspectorFocus = _pendingInspectorFocus?.FocusOnly();
        if (!ReferenceEquals(_committingInspector, DesktopInspector))
        {
            DesktopInspector.SetProject(ViewModel.Project);
        }

        if (!ReferenceEquals(_committingInspector, NarrowInspector))
        {
            NarrowInspector.SetProject(ViewModel.Project);
        }
        Preview.SetPlayhead(ViewModel.PlayheadText);
        Preview.SetAspectRatio(ViewModel.Project.Settings.Width, ViewModel.Project.Settings.Height);
        Preview.SetBackgroundColor(ViewModel.Project.Settings.BackgroundColor);
        Preview.SetTextItems(ViewModel.Project, ViewModel.Selection, ViewModel.PlayheadMilliseconds, ViewModel.Revision);
        Timeline.SetTimeline(
            ViewModel.Project,
            new TimelinePresentation(
                ViewModel.Selection,
                ViewModel.PlayheadMilliseconds,
                ViewModel.PlayheadText,
                ViewModel.DurationText,
                ViewModel.CanUndo,
                ViewModel.CanRedo,
                _projectRootPath));
        RefreshExportAvailability();
    }

    private async void EditorView_Loaded(object sender, RoutedEventArgs e) => await RebuildPreviewAsync(debounce: false);

    private async Task RebuildPreviewAsync(bool debounce)
    {
        PreviewRebuildLease? lease = null;
        var publication = CaptureInfoBarPublication();
        try
        {
            lease = _previewRebuildGate.Begin(_lifetimeToken);
            if (debounce) await Task.Delay(140, lease.Token);
            var previewKey = PreviewCompositionKey.Create(ViewModel.Project);
            if (previewKey.Equals(_publishedPreviewKey)) return;
            var result = await _compositionService.BuildPreviewAsync(ViewModel.Project, lease.Token);
            if (_disposed || lease.Token.IsCancellationRequested) return;
            if (!lease.TryCommit(() =>
                {
                    var requestedPosition = ViewModel.PlayheadMilliseconds;
                    var currentPlayIntent = Preview.PlayIntent;
                    Preview.ReplaceComposition(
                        result.HasContent ? result.Composition : null,
                        requestedPosition,
                        currentPlayIntent,
                        result.HasVisualContent);
                    _publishedPreviewKey = previewKey;
                })) return;
            var previewErrors = CompositionBuildResult.SelectPreviewErrors(result.Errors);
            if (previewErrors.Count > 0)
            {
                _publishedPreviewKey = null;
                TryShowPreviewMessage(
                    publication,
                    InfoBarSeverity.Warning,
                    "Preview contains unavailable items",
                    string.Join(" ", previewErrors));
            }
            else
            {
                ClearPreviewMessage();
            }
        }
        catch (OperationCanceledException) when (
            _lifetimeToken.IsCancellationRequested ||
            lease?.Token.IsCancellationRequested == true)
        {
            // A newer preview request or editor disposal superseded this rebuild.
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception) || exception is InvalidOperationException)
        {
            if (!_disposed && lease?.IsCurrent == true)
            {
                TryShowPreviewMessage(publication, InfoBarSeverity.Error,
                    "Could not rebuild preview",
                    "Windows could not prepare the project preview. Check the source files and try again.");
                _ = _logService.TryWriteAsync(
                    $"Preview rebuild failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}",
                    CancellationToken.None);
            }
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private void EditorView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CancelTimelineResize();
        var narrow = UseOverlayInspector(e.NewSize.Width);
        if (narrow != _isNarrow || ActualWidth <= 0)
        {
            _pendingInspectorFocus ??= (_isNarrow ? NarrowInspector : DesktopInspector).CaptureFocus();
            _isNarrow = narrow;
            _narrowInspectorVisible = false;
            if (_pendingInspectorFocus is not null)
            {
                if (_isNarrow)
                {
                    _narrowInspectorVisible = true;
                }
                else
                {
                    _wideInspectorVisible = true;
                }

                DispatcherQueue.TryEnqueue(RestorePendingInspectorFocus);
            }

            ApplyInspectorLayout();
        }

        SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);
    }

    internal static bool UseOverlayInspector(double logicalWidth) => logicalWidth <= InspectorCollapseWidth;

    internal static (bool ShowDesktop, bool ShowOverlay) ResolveInspectorVisibility(
        bool isNarrow,
        bool wideInspectorVisible,
        bool narrowInspectorVisible) =>
        (!isNarrow && wideInspectorVisible, isNarrow && narrowInspectorVisible);

    private void ApplyInspectorLayout()
    {
        var (showDesktop, showOverlay) = ResolveInspectorVisibility(
            _isNarrow,
            _wideInspectorVisible,
            _narrowInspectorVisible);
        InspectorColumn.Width = new GridLength(showDesktop ? 324 : 0);
        InspectorSeparatorColumn.Width = new GridLength(showDesktop ? 1 : 0);
        DesktopInspector.Visibility = showDesktop ? Visibility.Visible : Visibility.Collapsed;
        InspectorSeparator.Visibility = DesktopInspector.Visibility;
        InspectorOverlay.Visibility = showOverlay ? Visibility.Visible : Visibility.Collapsed;

        var action = showDesktop || showOverlay ? "Hide inspector" : "Show inspector";
        InspectorToggleButton.IsChecked = showDesktop || showOverlay;
        AutomationProperties.SetName(InspectorToggleButton, action);
        ToolTipService.SetToolTip(InspectorToggleButton, action);
    }

    private void RestorePendingInspectorFocus()
    {
        if (_disposed || _pendingInspectorFocus is null)
        {
            return;
        }

        var inspector = _isNarrow ? NarrowInspector : DesktopInspector;
        if (inspector.RestoreFocus(_pendingInspectorFocus))
        {
            _pendingInspectorFocus = null;
        }
    }

    private void InspectorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isNarrow)
        {
            _narrowInspectorVisible = !_narrowInspectorVisible;
        }
        else
        {
            _wideInspectorVisible = !_wideInspectorVisible;
        }

        ApplyInspectorLayout();
    }

    private void CloseOverlayInspector_Click(object sender, RoutedEventArgs e)
    {
        DismissNarrowInspector();
    }

    private void DismissNarrowInspector()
    {
        DismissTransientSurface(
            () => ContainsFocus(InspectorOverlay),
            () =>
            {
                _narrowInspectorVisible = false;
                ApplyInspectorLayout();
            },
            () => InspectorToggleButton.Focus(FocusState.Programmatic));
    }

    private bool ContainsFocus(DependencyObject surface)
    {
        if (XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focused)
        {
            return false;
        }

        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, surface))
            {
                return true;
            }
        }

        return false;
    }

    internal static void DismissTransientSurface(Func<bool> containsFocus, Action dismiss, Action restoreFocus)
    {
        var restore = containsFocus();
        dismiss();
        if (restore)
        {
            restoreFocus();
        }
    }

    private void Inspector_EditCommitted(object sender, InspectorEditCommittedEventArgs e)
    {
        _pendingInspectorFocus = _pendingInspectorFocus?.FocusOnly();
        var inspector = sender as InspectorPanel;
        var revision = ViewModel.Revision;
        if (e.ItemId is Guid itemId && Timeline.IsItemLocked(itemId))
        {
            ShowLockedTrackMessage(itemId);
            UpdateProjectPresentation();
            return;
        }

        _committingInspector = inspector;
        try
        {
            switch (e.Kind)
            {
                case InspectorEditKind.SetAspectRatio:
                    ViewModel.SetProjectAspectRatio(e.AspectRatio);
                    break;
                case InspectorEditKind.SetBackgroundColor when e.TextValue is not null:
                    ViewModel.SetBackgroundColor(e.TextValue);
                    break;
                case InspectorEditKind.TrimVideoStart when e.ItemId is Guid id:
                    ViewModel.TrimVideoStart(id, e.LongValue);
                    break;
                case InspectorEditKind.TrimVideoEnd when e.ItemId is Guid id:
                    ViewModel.TrimVideoEnd(id, e.LongValue);
                    break;
                case InspectorEditKind.SetImageDuration when e.ItemId is Guid id:
                    ViewModel.SetImageDuration(id, e.LongValue);
                    break;
                case InspectorEditKind.ResetImageDuration when e.ItemId is Guid id:
                    ViewModel.ResetImageDuration(id);
                    break;
                case InspectorEditKind.SetVideoVolume when e.ItemId is Guid id:
                    ViewModel.SetVideoVolume(id, e.DoubleValue);
                    break;
                case InspectorEditKind.SetVideoMuted when e.ItemId is Guid id:
                    ViewModel.SetVideoMuted(id, e.BoolValue);
                    break;
                case InspectorEditKind.MoveAudio when e.ItemId is Guid id:
                    ViewModel.MoveAudioItem(id, e.LongValue);
                    break;
                case InspectorEditKind.TrimAudioStart when e.ItemId is Guid id:
                    ViewModel.TrimAudioStart(id, e.LongValue);
                    break;
                case InspectorEditKind.TrimAudioEnd when e.ItemId is Guid id:
                    ViewModel.TrimAudioEnd(id, e.LongValue);
                    break;
                case InspectorEditKind.SetAudioVolume when e.ItemId is Guid id:
                    ViewModel.SetAudioVolume(id, e.DoubleValue);
                    break;
                case InspectorEditKind.SetAudioMuted when e.ItemId is Guid id:
                    ViewModel.SetAudioMuted(id, e.BoolValue);
                    break;
                case InspectorEditKind.SetTextContent when e.ItemId is Guid id && e.TextValue is not null:
                    ViewModel.SetTextContent(id, e.TextValue);
                    break;
                case InspectorEditKind.SetTextFontFamily when e.ItemId is Guid id && e.TextValue is not null:
                    ViewModel.SetTextFontFamily(id, e.TextValue);
                    break;
                case InspectorEditKind.SetTextFontSize when e.ItemId is Guid id:
                    ViewModel.SetTextFontSize(id, e.DoubleValue);
                    break;
                case InspectorEditKind.SetTextFontWeight when e.ItemId is Guid id:
                    ViewModel.SetTextFontWeight(id, checked((int)e.LongValue));
                    break;
                case InspectorEditKind.SetTextBold when e.ItemId is Guid id:
                    ViewModel.SetTextBold(id, e.BoolValue);
                    break;
                case InspectorEditKind.SetTextItalic when e.ItemId is Guid id:
                    ViewModel.SetTextItalic(id, e.BoolValue);
                    break;
                case InspectorEditKind.SetTextColor when e.ItemId is Guid id && e.TextValue is not null:
                    ViewModel.SetTextColor(id, e.TextValue, background: false);
                    break;
                case InspectorEditKind.SetTextBackground when e.ItemId is Guid id && e.TextValue is not null:
                    ViewModel.SetTextColor(id, e.TextValue, background: true);
                    break;
                case InspectorEditKind.SetTextBackgroundEnabled when e.ItemId is Guid id:
                    ViewModel.SetTextBackgroundEnabled(id, e.BoolValue);
                    break;
                case InspectorEditKind.SetTextOpacity when e.ItemId is Guid id:
                    ViewModel.SetTextOpacity(id, e.DoubleValue);
                    break;
                case InspectorEditKind.SetTextAlignment when e.ItemId is Guid id:
                    ViewModel.SetTextAlignment(id, e.TextAlignment);
                    break;
                case InspectorEditKind.SetTextHorizontalPosition when e.ItemId is Guid id:
                    ViewModel.SetTextHorizontalPosition(id, e.DoubleValue);
                    break;
                case InspectorEditKind.SetTextVerticalPosition when e.ItemId is Guid id:
                    ViewModel.SetTextVerticalPosition(id, e.DoubleValue);
                    break;
                case InspectorEditKind.MoveText when e.ItemId is Guid id:
                    ViewModel.MoveTextItem(id, e.LongValue);
                    break;
                case InspectorEditKind.SetTextDuration when e.ItemId is Guid id:
                    ViewModel.SetTextDuration(id, e.LongValue);
                    break;
                case InspectorEditKind.ResetTextStyle when e.ItemId is Guid id:
                    ViewModel.ResetTextStyle(id);
                    break;
            }
        }
        finally
        {
            _committingInspector = null;
        }

        if (ViewModel.Revision == revision)
        {
            if (!ReferenceEquals(inspector, DesktopInspector)) DesktopInspector.SetProject(ViewModel.Project);
            if (!ReferenceEquals(inspector, NarrowInspector)) NarrowInspector.SetProject(ViewModel.Project);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed) inspector?.SetProject(ViewModel.Project);
        });
    }

    private void Timeline_PlayheadChanged(object sender, PlayheadChangedEventArgs e)
    {
        SeekPreviewAndTimeline(e.PositionMilliseconds);
    }

    private void Timeline_SelectionChanged(object sender, EditorSelectionChangedEventArgs e) => ViewModel.Select(e.Selection);

    private void Timeline_TrackStateChanged(object sender, TimelineTrackStateChangedEventArgs e)
    {
        var track = e.State switch
        {
            TimelineTrackState.VideoVisibility => TimelineTrackKind.Video,
            TimelineTrackState.TextVisibility => TimelineTrackKind.Text,
            TimelineTrackState.AudioMute => TimelineTrackKind.Audio,
            _ => TimelineTrackKind.None
        };
        if (Timeline.IsTrackLocked(track))
        {
            ShowMessage(
                InfoBarSeverity.Warning,
                $"{TimelineDropPolicy.DisplayName(track)} is locked",
                "Unlock the track before changing its state.");
            UpdateProjectPresentation();
            return;
        }

        _ = e.State switch
        {
            TimelineTrackState.VideoVisibility => ViewModel.SetVideoTrackVisible(e.Value),
            TimelineTrackState.TextVisibility => ViewModel.SetTextTrackVisible(e.Value),
            TimelineTrackState.AudioMute => ViewModel.SetAudioTrackMuted(e.Value),
            _ => false
        };
    }

    private void Timeline_AssetDropped(object sender, TimelineAssetDroppedEventArgs e)
    {
        var asset = FindAsset(e.AssetId);
        if (asset is null)
        {
            ShowMessage(InfoBarSeverity.Error, CouldNotAddMediaTitle, "The dragged asset is no longer in this project.");
            return;
        }

        if (!TryAddAssetToTrack(asset, e.Track, e.PositionMilliseconds, out var error))
        {
            ShowMessage(InfoBarSeverity.Warning, CouldNotAddMediaTitle, error!);
        }
    }

    private async void Timeline_MediaFilesDropped(object sender, TimelineMediaFilesDroppedEventArgs e)
    {
        try
        {
            await ImportFilesToTimelineAsync(e.Files, e.Track, e.PositionMilliseconds, e.RejectedItems);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // The editor was closed while the drop import was running.
        }
    }

    private void Timeline_DropRejected(object sender, TimelineDropRejectedEventArgs e) =>
        ShowMessage(InfoBarSeverity.Warning, "Could not add dropped media", e.Message);

    private void Timeline_EditRequested(object sender, TimelineEditRequestedEventArgs e)
    {
        if (e.ItemId is Guid commandItemId)
        {
            switch (e.Kind)
            {
                case TimelineEditKind.SplitVideo:
                    SplitTimelineItemWithGuidance(commandItemId, e.LongValue);
                    return;
                case TimelineEditKind.Duplicate:
                    DuplicateTimelineItemWithGuidance(commandItemId);
                    return;
                case TimelineEditKind.Delete:
                    DeleteTimelineItemWithGuidance(commandItemId);
                    return;
                case TimelineEditKind.ShowSourceFile:
                    ShowTimelineSourceInExplorer(commandItemId);
                    return;
                default:
                    SelectTimelineItem(commandItemId);
                    break;
            }
        }

        switch (e.Kind)
        {
            case TimelineEditKind.ReorderVideo when e.ItemId is Guid id:
                ViewModel.ReorderVideoItem(id, e.IntValue);
                break;
            case TimelineEditKind.TrimVideoStart when e.ItemId is Guid id:
                ViewModel.TrimVideoStart(id, e.LongValue);
                break;
            case TimelineEditKind.TrimVideoEnd when e.ItemId is Guid id:
                ViewModel.TrimVideoEnd(id, e.LongValue);
                break;
            case TimelineEditKind.SetImageDuration when e.ItemId is Guid id:
                ViewModel.SetImageDuration(id, e.LongValue);
                break;
            case TimelineEditKind.MoveAudio when e.ItemId is Guid id:
                ViewModel.MoveAudioItem(id, e.LongValue);
                break;
            case TimelineEditKind.TrimAudioStart when e.ItemId is Guid id:
                ViewModel.TrimAudioStart(id, e.LongValue);
                break;
            case TimelineEditKind.TrimAudioEnd when e.ItemId is Guid id:
                ViewModel.TrimAudioEnd(id, e.LongValue);
                break;
            case TimelineEditKind.MoveText when e.ItemId is Guid id:
                ViewModel.MoveTextItem(id, e.LongValue);
                break;
            case TimelineEditKind.TrimTextStart when e.ItemId is Guid id:
                ViewModel.TrimTextStart(id, e.LongValue);
                break;
            case TimelineEditKind.TrimTextEnd when e.ItemId is Guid id:
                ViewModel.TrimTextEnd(id, e.LongValue);
                break;
            case TimelineEditKind.Delete:
                DeleteSelectionWithGuidance();
                break;
            case TimelineEditKind.Duplicate:
                DuplicateSelectionWithGuidance();
                break;
            case TimelineEditKind.Undo:
                UndoWithGuidance();
                break;
            case TimelineEditKind.Redo:
                RedoWithGuidance();
                break;
        }
    }

    private void Preview_SeekRequested(object? sender, PlayheadChangedEventArgs e)
    {
        SeekPreviewAndTimeline(e.PositionMilliseconds);
    }

    private void SeekPreviewAndTimeline(long positionMilliseconds)
    {
        Preview.Seek(positionMilliseconds);
        ViewModel.Seek(positionMilliseconds);
        Timeline.UpdatePlaybackPosition(
            ViewModel.PlayheadMilliseconds,
            ViewModel.PlayheadText,
            ensureVisible: true);
    }

    private void Preview_PlaybackPositionChanged(object? sender, PlayheadChangedEventArgs e) =>
        ViewModel.Seek(e.PositionMilliseconds);

    private void Preview_PlaybackFailed(object? sender, PreviewPlaybackFailedEventArgs e)
    {
        TryShowPreviewMessage(
            CaptureInfoBarPublication(),
            InfoBarSeverity.Error,
            "Preview playback failed",
            "Windows could not decode the preview. A source file may be damaged or require a codec that is not installed.");
        _ = _logService.TryWriteAsync(
            $"Preview playback failed: {e.Error}",
            CancellationToken.None);
    }

    private void Preview_TextPositionCommitted(object? sender, TextPositionCommittedEventArgs e)
    {
        if (Timeline.IsItemLocked(e.ItemId))
        {
            ShowLockedTrackMessage(e.ItemId);
            UpdateProjectPresentation();
            return;
        }

        ViewModel.SetTextPosition(e.ItemId, e.NormalizedX, e.NormalizedY);
    }

    private void Preview_PlayPauseRequested(object? sender, PlaybackChangedEventArgs e)
    {
        if (ViewModel.IsPlaying != e.IsPlaying)
        {
            ViewModel.TogglePlayback();
        }
    }

    private void Timeline_ZoomChanged(object? sender, EventArgs e)
    {
        _workspaceSettings.TimelineZoom = Timeline.Zoom;
        RaiseWorkspaceSettingsChanged();
    }

    private void Preview_LoopChanged(object? sender, EventArgs e)
    {
        _workspaceSettings.LoopPlayback = Preview.IsLooping;
        RaiseWorkspaceSettingsChanged();
    }

    private void ApplyWorkspaceSettings(AppSettings settings)
    {
        Timeline.SetZoom(settings.TimelineZoom);
        SetTimelineHeight(settings.TimelineHeight, notify: false);
        Preview.Loop(settings.LoopPlayback);
    }

    private void SetTimelineHeight(double height, bool notify)
    {
        var normalized = AppSettings.NormalizeTimelineHeightForWindow(height, EditorRoot.ActualHeight);
        if (Math.Abs(TimelineRow.Height.Value - normalized) >= 0.01)
        {
            TimelineRow.Height = new GridLength(normalized);
        }

        if (notify && Math.Abs(_workspaceSettings.TimelineHeight - normalized) >= 0.01)
        {
            _workspaceSettings.TimelineHeight = normalized;
            RaiseWorkspaceSettingsChanged();
        }
    }

    private void RaiseWorkspaceSettingsChanged() =>
        WorkspaceSettingsChanged?.Invoke(this, new WorkspaceSettingsChangedEventArgs(
            Timeline.Zoom,
            _workspaceSettings.TimelineHeight,
            Preview.IsLooping,
            _workspaceSettings.LastExportFolder));

    private void TimelineResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement handle ||
            _timelineResizePointer is not null ||
            !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed ||
            !handle.CapturePointer(e.Pointer))
        {
            return;
        }

        _timelineResizeCaptureElement = handle;
        _timelineResizePointer = e.Pointer;
        _timelineResizePointerId = e.Pointer.PointerId;
        _timelineResizeStartY = e.GetCurrentPoint(EditorRoot).Position.Y;
        _timelineResizeStartHeight = TimelineHeight;
        e.Handled = true;
    }

    private void TimelineResizeHandle_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var delta = TimelineResizeDelta(e.Key);
        if (delta == 0)
        {
            return;
        }

        SetTimelineHeight(TimelineHeight + delta, notify: true);
        e.Handled = true;
    }

    internal static double TimelineResizeDelta(VirtualKey key) => key switch
    {
        VirtualKey.Up => 20,
        VirtualKey.Down => -20,
        _ => 0
    };

    private void TimelineResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_timelineResizePointerId != e.Pointer.PointerId)
        {
            return;
        }

        SetTimelineHeight(
            _timelineResizeStartHeight - (e.GetCurrentPoint(EditorRoot).Position.Y - _timelineResizeStartY),
            notify: false);
        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_timelineResizePointerId != e.Pointer.PointerId)
        {
            return;
        }

        ReleaseTimelineResizeCapture();

        SetTimelineHeight(TimelineHeight, notify: true);
        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!CancelTimelineResize(e.Pointer.PointerId))
        {
            return;
        }

        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (CancelTimelineResize(e.Pointer.PointerId, releaseCapture: false))
        {
            e.Handled = true;
        }
    }

    private void CancelTimelineResize() => CancelTimelineResize(_timelineResizePointerId);

    private bool CancelTimelineResize(uint pointerId, bool releaseCapture = true)
    {
        if (_timelineResizePointer is null || _timelineResizePointerId != pointerId)
        {
            return false;
        }

        SetTimelineHeight(_workspaceSettings.TimelineHeight, notify: false);
        if (releaseCapture)
        {
            ReleaseTimelineResizeCapture();
        }
        else
        {
            ClearTimelineResizeCapture();
        }

        return true;
    }

    private void ReleaseTimelineResizeCapture()
    {
        var element = _timelineResizeCaptureElement;
        var pointer = _timelineResizePointer;
        ClearTimelineResizeCapture();
        if (element is not null && pointer is not null)
        {
            element.ReleasePointerCapture(pointer);
        }
    }

    private void ClearTimelineResizeCapture()
    {
        _timelineResizeCaptureElement = null;
        _timelineResizePointer = null;
        _timelineResizePointerId = 0;
    }

    private void ToolPanel_ImportRequested(object? sender, MediaImportRequestedEventArgs e)
    {
        ViewModel.RequestImport();
        ImportRequested?.Invoke(this, e);
    }

    private void Preview_ImportRequested(object? sender, EventArgs e)
    {
        ViewModel.RequestImport();
        ImportRequested?.Invoke(this, new MediaImportRequestedEventArgs(MediaImportScope.All));
    }

    private void ToolPanel_AddAssetRequested(object? sender, AssetActionEventArgs e)
    {
        var asset = FindAsset(e.AssetId);
        if (asset is null)
        {
            ShowMessage(InfoBarSeverity.Error, CouldNotAddMediaTitle, "The asset is no longer in this project.");
            return;
        }

        var track = asset.Kind == ProjectAssetKind.Audio
            ? TimelineTrackKind.Audio
            : TimelineTrackKind.Video;
        if (!TryAddAssetToTrack(asset, track, ViewModel.PlayheadMilliseconds, out var error))
        {
            ShowMessage(InfoBarSeverity.Warning, CouldNotAddMediaTitle, error!);
            return;
        }
    }

    private void ToolPanel_RelinkAssetRequested(object? sender, AssetActionEventArgs e) =>
        RelinkAssetRequested?.Invoke(this, e);

    private void ToolPanel_ShowAssetInExplorerRequested(object? sender, AssetActionEventArgs e)
    {
        var asset = FindAsset(e.AssetId);
        if (asset is null || !SourceFileReveal.TryCreateStartInfo(asset.SourcePath, out var startInfo))
        {
            ShowMessage(InfoBarSeverity.Error, "Could not open Explorer", "The source file is missing or inaccessible.");
            return;
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowMessage(InfoBarSeverity.Error, $"Could not show '{asset.FileName}'", "File Explorer could not be opened for this source path.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowMessage(InfoBarSeverity.Error, $"Could not show '{asset.FileName}'", "File Explorer could not be opened for this source path.");
        }
    }

    private void SelectTimelineItem(Guid itemId)
    {
        var selection = EditorSelection.None;
        if (ViewModel.Project.VideoItems.Any(item => item.Id == itemId))
        {
            selection = new EditorSelection(EditorSelectionKind.VideoItem, itemId);
        }
        else if (ViewModel.Project.AudioItems.Any(item => item.Id == itemId))
        {
            selection = new EditorSelection(EditorSelectionKind.AudioItem, itemId);
        }
        else if (ViewModel.Project.TextItems.Any(item => item.Id == itemId))
        {
            selection = new EditorSelection(EditorSelectionKind.TextItem, itemId);
        }
        if (selection != EditorSelection.None)
        {
            ViewModel.Select(selection);
        }
    }

    private void ShowTimelineSourceInExplorer(Guid itemId)
    {
        var assetId = ViewModel.Project.VideoItems.FirstOrDefault(item => item.Id == itemId)?.AssetId ??
            ViewModel.Project.AudioItems.FirstOrDefault(item => item.Id == itemId)?.AssetId;
        if (assetId is Guid foundAssetId)
        {
            ToolPanel_ShowAssetInExplorerRequested(this, new AssetActionEventArgs(foundAssetId));
            return;
        }

        ShowMessage(InfoBarSeverity.Error, "Could not open Explorer", "The timeline item is no longer available.");
    }

    private void ToolPanel_RemoveAssetRequested(object? sender, AssetActionEventArgs e)
    {
        var asset = FindAsset(e.AssetId);
        if (asset is null)
        {
            ShowMessage(InfoBarSeverity.Error, "Could not remove media", "The asset is no longer in this project.");
            return;
        }

        var cacheReference = CopyAsset(asset);
        if (!ViewModel.RemoveAsset(e.AssetId, out var error))
        {
            ShowMessage(InfoBarSeverity.Warning, $"Could not remove '{asset.FileName}'", error!);
            return;
        }

        TryDeleteCache(cacheReference);
        ToolPanel.RefreshAssets();
        ShowMessage(InfoBarSeverity.Informational, "Removed from project", $"'{asset.FileName}' was removed from this project. The source file was not deleted.");
    }

    private async void ToolPanel_MediaFilesDropped(object? sender, MediaFilesDroppedEventArgs e)
    {
        try
        {
            await ImportFilesAsync(e.Files, e.ResultSlots);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // The editor was closed while the drop import was running.
        }
    }

    private void ToolPanel_MediaDropFailed(object? sender, MediaDropFailedEventArgs e) =>
        ReportError("Could not import dropped media", e.Message);

    private void ToolPanel_AddTextRequested(object? sender, TextPresetRequestedEventArgs e)
    {
        if (Timeline.IsTrackLocked(TimelineTrackKind.Text))
        {
            ShowMessage(InfoBarSeverity.Warning, "T1 is locked", "Unlock T1 before adding text.");
            return;
        }

        ViewModel.AddText(e.Preset);
        ViewModel.SelectedTool = EditorTool.Text;
        ToolPanel.SetTool(EditorTool.Text);
        UpdateToolSelection();
        UpdateProjectPresentation();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            if (_isNarrow)
            {
                _narrowInspectorVisible = true;
                ApplyInspectorLayout();
            }
            else if (!_wideInspectorVisible)
            {
                InspectorToggle_Click(this, new RoutedEventArgs());
            }
            (_isNarrow ? NarrowInspector : DesktopInspector).FocusTextContent();
        });
        AddTextRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_isRendering)
        {
            ShowMessage(InfoBarSeverity.Warning, ExportInProgressTitle, "Cancel the export or wait for it to finish before returning Home.");
            return;
        }

        if (!await SaveAsync())
        {
            return;
        }

        if (_isRendering)
        {
            ShowMessage(InfoBarSeverity.Warning, ExportInProgressTitle, "Cancel the export or wait for it to finish before returning Home.");
            return;
        }

        ViewModel.RequestReturnHome();
        ReturnHomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProjectNameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitProjectName();
            EditorRoot.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ProjectNameBox.Text = ViewModel.ProjectName;
            EditorRoot.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void ProjectNameBox_LostFocus(object sender, RoutedEventArgs e) => CommitProjectName();

    private void CommitProjectName()
    {
        var enteredName = ProjectNameBox.Text;
        if (string.Equals(enteredName, ViewModel.ProjectName, StringComparison.Ordinal))
        {
            return;
        }

        var name = enteredName.Trim();
        if (name.Length == 0)
        {
            ProjectNameBox.Text = ViewModel.ProjectName;
            ShowMessage(InfoBarSeverity.Warning, "Project name is required", "Enter a name before leaving the field.");
            return;
        }

        if (!string.Equals(name, ViewModel.ProjectName, StringComparison.Ordinal))
        {
            ViewModel.RenameProject(name);
        }

        ProjectNameBox.Text = ViewModel.ProjectName;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!ExportPresentation.CanStartExport(ViewModel.Project, _isExporting))
        {
            return;
        }

        ViewModel.RequestExport();
        ExportRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void EditorRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var editableControlFocused = IsEditableControlFocused();
        var controlDown = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
        var shouldCreateNewProject = GlobalShortcutRouter.ShouldCreateNewProject(
            e.Handled,
            editableControlFocused,
            controlDown,
            e.Key,
            e.KeyStatus.WasKeyDown);
        if (e.Handled || editableControlFocused)
        {
            return;
        }

        if (controlDown && e.Key == VirtualKey.N)
        {
            e.Handled = true;
            await HandleNewProjectShortcutAsync(shouldCreateNewProject);
            return;
        }

        if (IsSingleActionShortcut(controlDown, e.Key))
        {
            e.Handled = true;
            await HandleSingleActionShortcutAsync(controlDown, e.Key, e.KeyStatus.WasKeyDown);
            return;
        }

        HandlePlaybackOrEscapeShortcut(e);
    }

    private async Task HandleNewProjectShortcutAsync(bool shouldCreateNewProject)
    {
        if (!shouldCreateNewProject || RejectNewProjectDuringExport())
        {
            return;
        }

        if (await SaveAsync() && !RejectNewProjectDuringExport())
        {
            NewProjectRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool RejectNewProjectDuringExport()
    {
        if (!_isExporting)
        {
            return false;
        }

        ShowMessage(
            InfoBarSeverity.Warning,
            ExportInProgressTitle,
            "Cancel the export or wait for it to finish before creating another project.");
        return true;
    }

    private static bool IsSingleActionShortcut(bool controlDown, VirtualKey key) =>
        controlDown && key is VirtualKey.S or VirtualKey.Z or VirtualKey.Y or VirtualKey.B or VirtualKey.D ||
        key == VirtualKey.Delete;

    private async Task HandleSingleActionShortcutAsync(bool controlDown, VirtualKey key, bool wasKeyDown)
    {
        if (wasKeyDown)
        {
            return;
        }

        switch (key)
        {
            case VirtualKey.S when controlDown:
                await SaveAsync();
                break;
            case VirtualKey.Z when controlDown:
                UndoWithGuidance();
                break;
            case VirtualKey.Y when controlDown:
                RedoWithGuidance();
                break;
            case VirtualKey.B when controlDown:
                SplitSelectionWithGuidance();
                break;
            case VirtualKey.D when controlDown:
                DuplicateSelectionWithGuidance();
                break;
            case VirtualKey.Delete:
                DeleteSelectionWithGuidance();
                break;
        }
    }

    private void HandlePlaybackOrEscapeShortcut(KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Space:
                e.Handled = true;
                if (!e.KeyStatus.WasKeyDown)
                {
                    Preview.PlayPause();
                }
                break;
            case VirtualKey.Escape:
                if (_isNarrow && _narrowInspectorVisible)
                {
                    DismissNarrowInspector();
                }
                else
                {
                    ViewModel.Select(EditorSelection.None);
                }
                e.Handled = true;
                break;
        }
    }

    private bool IsEditableControlFocused() =>
        FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox or RichEditBox;

    private bool TryRejectLockedSelectionEdit()
    {
        if (!Timeline.IsSelectionLocked(ViewModel.Selection))
        {
            return false;
        }

        if (ViewModel.Selection.ItemId is Guid itemId)
        {
            ShowLockedTrackMessage(itemId);
        }

        return true;
    }

    private void UndoWithGuidance()
    {
        if (!ViewModel.CanUndo)
        {
            ShowMessage(InfoBarSeverity.Informational, "Nothing to undo", "Make an edit first.");
            return;
        }

        ViewModel.Undo();
    }

    private void RedoWithGuidance()
    {
        if (!ViewModel.CanRedo)
        {
            ShowMessage(InfoBarSeverity.Informational, "Nothing to redo", "Undo an edit first.");
            return;
        }

        ViewModel.Redo();
    }

    private void SplitSelectionWithGuidance()
    {
        if (ViewModel.Selection is not { Kind: EditorSelectionKind.VideoItem, ItemId: Guid })
        {
            ShowMessage(InfoBarSeverity.Informational, "Select a V1 item", "Select a V1 item to split.");
            return;
        }

        if (TryRejectLockedSelectionEdit())
        {
            return;
        }

        if (!ViewModel.SplitSelection())
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not split item", "Move the playhead at least 0.1 seconds from either edge.");
        }
    }

    private void SplitTimelineItemWithGuidance(Guid itemId, long positionMilliseconds)
    {
        if (Timeline.IsItemLocked(itemId))
        {
            ShowLockedTrackMessage(itemId);
            return;
        }

        if (!ViewModel.SplitVideoItem(itemId, positionMilliseconds))
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not split item", "Move the playhead at least 0.1 seconds from either edge.");
        }
    }

    private void DuplicateSelectionWithGuidance()
    {
        if (ViewModel.Selection is not
            {
                Kind: EditorSelectionKind.VideoItem or EditorSelectionKind.TextItem or EditorSelectionKind.AudioItem,
                ItemId: Guid
            })
        {
            ShowMessage(InfoBarSeverity.Informational, "Select a timeline item", "Select a V1, T1, or A1 item to duplicate.");
            return;
        }

        if (TryRejectLockedSelectionEdit())
        {
            return;
        }

        if (!ViewModel.DuplicateSelection())
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not duplicate item", "The copy must fit within the 24-hour timeline.");
        }
    }

    private void DuplicateTimelineItemWithGuidance(Guid itemId)
    {
        if (Timeline.IsItemLocked(itemId))
        {
            ShowLockedTrackMessage(itemId);
            return;
        }

        if (!ViewModel.DuplicateTimelineItem(itemId))
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not duplicate item", "The copy must fit within the 24-hour timeline.");
        }
    }

    private void DeleteTimelineItemWithGuidance(Guid itemId)
    {
        if (Timeline.IsItemLocked(itemId))
        {
            ShowLockedTrackMessage(itemId);
            return;
        }

        if (!ViewModel.DeleteTimelineItem(itemId))
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not delete item", "The timeline item is no longer available.");
        }
    }

    private void DeleteSelectionWithGuidance()
    {
        if (ViewModel.Selection is not
            {
                Kind: EditorSelectionKind.VideoItem or EditorSelectionKind.TextItem or EditorSelectionKind.AudioItem,
                ItemId: Guid
            })
        {
            return;
        }

        if (TryRejectLockedSelectionEdit())
        {
            return;
        }

        if (!ViewModel.DeleteSelection())
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not delete item", "The selected timeline item is no longer available.");
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoWithGuidance();

    private void Redo_Click(object sender, RoutedEventArgs e) => RedoWithGuidance();

    private void EditorView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Parent is null)
        {
            Dispose();
        }
    }

    private void ShowInitialMissingStatus()
    {
        var missing = ViewModel.Project.Assets.Where(asset => asset.IsMissing).Select(asset => asset.FileName).ToList();
        if (missing.Count > 0)
        {
            ShowMessage(
                InfoBarSeverity.Warning,
                "Some media is missing",
                string.Join(" ", missing.Take(3).Select(name => $"'{name}'")) + (missing.Count > 3 ? $" and {missing.Count - 3} more." : ". Relink missing files before adding them again."));
        }
    }

    private void ShowImportResults(
        IReadOnlyList<ImportResult> results,
        InfoBarPublication? publication = null)
    {
        var imported = results.Count(result => result.IsSuccess);
        var issues = results.Where(result => !result.IsSuccess).ToList();
        if (issues.Count > 0)
        {
            var details = string.Join(" ", issues.Take(3).Select(result => $"'{result.FileName}': {result.ErrorMessage}"));
            if (issues.Count > 3)
            {
                details += $" {issues.Count - 3} more files were not imported.";
            }

            ShowImportMessage(
                publication,
                issues.All(result => result.IsDuplicate) ? InfoBarSeverity.Informational : InfoBarSeverity.Error,
                imported > 0 ? $"Imported {imported} file(s) with issues" : "No files were imported",
                details);
        }
        else
        {
            ShowImportMessage(
                publication,
                InfoBarSeverity.Success,
                "Import complete",
                $"Imported {imported} file(s) into this project.");
        }
    }

    private void ShowImportMessage(
        InfoBarPublication? publication,
        InfoBarSeverity severity,
        string title,
        string message)
    {
        if (publication is { } captured)
        {
            TryShowMessage(captured, severity, title, message);
        }
        else
        {
            ShowMessage(severity, title, message);
        }
    }

    private void ShowMessage(InfoBarSeverity severity, string title, string message)
    {
        if (_disposed || _lifetimeToken.IsCancellationRequested)
        {
            return;
        }

        _projectSaveFailureInfoBarIsCurrent = false;
        _previewInfoBarIsCurrent = false;
        _infoBarRevision++;
        _infoBarSeverity = severity;
        EditorInfoBar.Severity = severity;
        EditorInfoBar.Title = title;
        EditorInfoBar.Message = message;
        EditorInfoBar.IsOpen = true;
    }

    internal InfoBarPublication CaptureInfoBarPublication() => new(_infoBarRevision);

    internal void ReportError(InfoBarPublication publication, string title, string message) =>
        TryShowMessage(publication, InfoBarSeverity.Error, title, message);

    private bool TryShowMessage(
        InfoBarPublication publication,
        InfoBarSeverity severity,
        string title,
        string message)
    {
        if (!CanPublishInfoBar(
                _disposed,
                _lifetimeToken.IsCancellationRequested,
                _infoBarRevision,
                _infoBarSeverity,
                publication,
                severity))
        {
            return false;
        }

        ShowMessage(severity, title, message);
        return true;
    }

    internal static bool CanPublishInfoBar(
        bool disposed,
        bool lifetimeCanceled,
        long currentRevision,
        InfoBarSeverity currentSeverity,
        InfoBarPublication publication,
        InfoBarSeverity proposedSeverity) =>
        !disposed &&
        !lifetimeCanceled &&
        (publication.Revision == currentRevision || MessagePriority(proposedSeverity) > MessagePriority(currentSeverity));

    private static int MessagePriority(InfoBarSeverity severity) => severity switch
    {
        InfoBarSeverity.Error => 4,
        InfoBarSeverity.Warning => 3,
        InfoBarSeverity.Informational => 2,
        _ => 1
    };

    private bool TryShowPreviewMessage(
        InfoBarPublication publication,
        InfoBarSeverity severity,
        string title,
        string message)
    {
        if (TryShowMessage(publication, severity, title, message))
        {
            _previewInfoBarIsCurrent = true;
            return true;
        }
        return false;
    }

    private void ClearPreviewMessage()
    {
        if (!_previewInfoBarIsCurrent)
        {
            return;
        }

        _previewInfoBarIsCurrent = false;
        EditorInfoBar.IsOpen = false;
    }

    private void TryShowProjectSaveFailure(InfoBarPublication publication, string message)
    {
        if (TryShowMessage(publication, InfoBarSeverity.Error, "Could not save project", message))
        {
            _projectSaveFailureInfoBarIsCurrent = true;
        }
    }

    private void ClearProjectSaveFailure()
    {
        if (!_projectSaveFailureInfoBarIsCurrent)
        {
            return;
        }

        _projectSaveFailureInfoBarIsCurrent = false;
        EditorInfoBar.IsOpen = false;
    }

    private void ShowLockedTrackMessage(Guid itemId)
    {
        var trackName = TimelineDropPolicy.DisplayName(Timeline.TrackForItem(itemId));
        ShowMessage(
            InfoBarSeverity.Warning,
            $"{trackName} is locked",
            $"Unlock {trackName} before editing this item.");
    }

    private void TryDeleteCache(ProjectAsset asset)
    {
        try
        {
            _thumbnailService.TryDeleteCachedThumbnail(asset);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or
                                           ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            _ = _logService.TryWriteAsync($"Thumbnail cache cleanup failed: {exception.GetType().Name}", CancellationToken.None);
            ShowMessage(InfoBarSeverity.Warning, "Media updated", "The project reference was updated, but its old thumbnail cache could not be removed.");
        }
    }

    private void CommitImportResults(IReadOnlyList<ImportResult> results, InfoBarPublication publication)
    {
        EnsureActive();
        results = MediaImportService.RevalidateDuplicateResults(ViewModel.Project, results);
        var imported = results.Where(result => result.Asset is not null).Select(result => result.Asset!).ToList();
        if (imported.Count > 0)
        {
            ViewModel.AddImportedAssets(imported);
        }

        ToolPanel.RefreshAssets();
        ShowImportResults(results, publication);
    }

    private void EnsureActive()
    {
        _lifetimeToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static ProjectAsset CopyAsset(ProjectAsset asset)
    {
        lock (asset)
        {
            return new ProjectAsset
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
        }
    }
}

internal readonly record struct InfoBarPublication(long Revision);

public sealed class WorkspaceSettingsChangedEventArgs(
    double timelineZoom,
    double timelineHeight,
    bool loopPlayback,
    string lastExportFolder) : EventArgs
{
    public double TimelineZoom { get; } = timelineZoom;
    public double TimelineHeight { get; } = timelineHeight;
    public bool LoopPlayback { get; } = loopPlayback;
    public string LastExportFolder { get; } = lastExportFolder;
}
