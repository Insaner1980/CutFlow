using System.Diagnostics;
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
    private readonly TextOverlayRenderer _textOverlayRenderer;
    private readonly DebouncedSaveCoordinator _autosave;
    private readonly AppSettings _workspaceSettings;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly CancellationToken _lifetimeToken;
    private bool _isNarrow;
    private bool _wideInspectorVisible = true;
    private uint _timelineResizePointerId;
    private double _timelineResizeStartY;
    private double _timelineResizeStartHeight;
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
        _autosave = new DebouncedSaveCoordinator(() => _projectService.SaveAsync(ViewModel.Project));
        _autosave.StateChanged += Autosave_StateChanged;
        InitializeComponent();
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

    public double TimelineHeight => TimelineRow.Height.Value;

    public void SetTimelineHeight(double height) => SetTimelineHeight(height, notify: true);

    public async Task ImportFilesAsync(IReadOnlyList<StorageFile> files)
    {
        EnsureActive();
        if (files.Count == 0)
        {
            return;
        }

        await _importGate.ExecuteAsync(
            cancellationToken => _mediaImportService.ImportAsync(files, ViewModel.Project, _projectsRootPath, cancellationToken),
            CommitImportResults,
            _lifetimeToken);
    }

    private async Task ImportFilesToTimelineAsync(
        IReadOnlyList<StorageFile> files,
        TimelineTrackKind track,
        long positionMilliseconds)
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
        var rejected = new List<string>();
        foreach (var file in files)
        {
            if (!MediaImportService.TryGetKind(file.Path, out var kind))
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

        await _importGate.ExecuteAsync(
            cancellationToken => _mediaImportService.ImportAsync(accepted, ViewModel.Project, _projectsRootPath, cancellationToken),
            results => CommitTimelineDropResults(accepted, results, track, positionMilliseconds, rejected),
            _lifetimeToken);
    }

    private void CommitTimelineDropResults(
        IReadOnlyList<StorageFile> files,
        IReadOnlyList<ImportResult> results,
        TimelineTrackKind track,
        long positionMilliseconds,
        IReadOnlyList<string> rejected)
    {
        EnsureActive();
        var imported = results
            .Where(result => result.Asset is not null)
            .Select(result => result.Asset!)
            .ToList();
        if (imported.Count > 0)
        {
            ViewModel.AddImportedAssets(imported);
        }

        var issues = new List<string>(rejected);
        var added = 0;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var asset = result.Asset ??
                (result.IsDuplicate && index < files.Count ? FindAssetBySourcePath(files[index].Path) : null);
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

            if (TryAddAssetToTrack(asset, track, positionMilliseconds, out var error))
            {
                added++;
            }
            else
            {
                issues.Add(error!);
            }
        }

        ToolPanel.RefreshAssets();
        UpdateProjectPresentation();
        var trackName = TimelineDropPolicy.DisplayName(track);
        if (issues.Count == 0)
        {
            ShowMessage(
                InfoBarSeverity.Success,
                $"Added to {trackName}",
                $"Added {added} file(s) to the timeline.");
        }
        else
        {
            ShowMessage(
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
            Preview.Seek(positionMilliseconds);
            ViewModel.Seek(positionMilliseconds);
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

    private ProjectAsset? FindAssetBySourcePath(string path)
    {
        try
        {
            var normalized = MediaImportService.NormalizePath(path);
            return ViewModel.Project.Assets.FirstOrDefault(asset =>
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
        try
        {
            await _autosave.FlushAsync();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ReportError("Could not save project", "Your latest edit could not be saved. Try again before closing the editor.");
            _ = _logService.TryWriteAsync($"Project save failed: {exception.GetType().Name}");
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
            ShowMessage(InfoBarSeverity.Error, $"Could not relink '{replacement.Name}'", result.ErrorMessage ?? "The asset could not be updated.");
            return;
        }

        TryDeleteCache(oldCacheReference);
        ToolPanel.RefreshAssets();
        ShowMessage(InfoBarSeverity.Success, "Media relinked", $"'{candidate.FileName}' now replaces the missing or moved source while keeping timeline references.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _exportState.BeginClosing();
        _autosave.StateChanged -= Autosave_StateChanged;
        _autosave.Dispose();
        _lifetimeCts.Cancel();
        _previewRebuildGate.Dispose();
        Preview.Dispose();
        _lifetimeCts.Dispose();
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
        Preview.TextPositionCommitted -= Preview_TextPositionCommitted;
        Preview.LoopChanged -= Preview_LoopChanged;
        Timeline.ZoomChanged -= Timeline_ZoomChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.PlayheadMilliseconds))
        {
            Preview.SetPlayhead(ViewModel.PlayheadText);
            Preview.SetTextItems(ViewModel.Project, ViewModel.Selection, ViewModel.PlayheadMilliseconds);
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
                    break;
                case DebouncedSaveState.Saving:
                    ViewModel.MarkSaving();
                    break;
                case DebouncedSaveState.SaveFailed:
                    ViewModel.MarkSaveFailed();
                    ReportError("Could not save project", "Autosave failed. Try Ctrl+S before closing the editor.");
                    _ = _logService.TryWriteAsync("Project autosave failed.");
                    break;
            }
        });
    }

    private void ViewModel_SelectionChanged(object? sender, EditorSelectionChangedEventArgs e)
    {
        DesktopInspector.SetSelection(e.Selection);
        NarrowInspector.SetSelection(e.Selection);
        Timeline.SetSelection(e.Selection);
        Preview.SetTextItems(ViewModel.Project, e.Selection, ViewModel.PlayheadMilliseconds);
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
        DesktopInspector.SetProject(ViewModel.Project);
        NarrowInspector.SetProject(ViewModel.Project);
        Preview.SetPlayhead(ViewModel.PlayheadText);
        Preview.SetAspectRatio(ViewModel.Project.Settings.Width, ViewModel.Project.Settings.Height);
        Preview.SetBackgroundColor(ViewModel.Project.Settings.BackgroundColor);
        Preview.SetTextItems(ViewModel.Project, ViewModel.Selection, ViewModel.PlayheadMilliseconds);
        Timeline.SetTimeline(
            ViewModel.Project,
            ViewModel.Selection,
            ViewModel.PlayheadMilliseconds,
            ViewModel.PlayheadText,
            ViewModel.DurationText,
            ViewModel.CanUndo,
            ViewModel.CanRedo,
            _projectRootPath);
        RefreshExportAvailability();
    }

    private async void EditorView_Loaded(object sender, RoutedEventArgs e) => await RebuildPreviewAsync(debounce: false);

    private async Task RebuildPreviewAsync(bool debounce)
    {
        PreviewRebuildLease? lease = null;
        try
        {
            lease = _previewRebuildGate.Begin(_lifetimeToken);
            if (debounce) await Task.Delay(140, lease.Token);
            var result = await _compositionService.BuildPreviewAsync(ViewModel.Project, lease.Token);
            if (_disposed || lease.Token.IsCancellationRequested) return;
            if (!lease.TryCommit(() =>
                {
                    var currentPosition = Preview.PositionMilliseconds;
                    var currentPlayIntent = Preview.PlayIntent;
                    Preview.ReplaceComposition(
                        result.HasContent ? result.Composition : null,
                        currentPosition,
                        currentPlayIntent,
                        result.HasVisualContent);
                })) return;
            if (result.Errors.Count > 0)
            {
                ShowMessage(InfoBarSeverity.Warning, "Preview contains unavailable items", string.Join(" ", result.Errors.Take(3)));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            if (!_disposed && lease?.IsCurrent == true)
            {
                ShowMessage(InfoBarSeverity.Error, "Could not rebuild preview", exception.Message);
            }
        }
        finally
        {
            lease?.Dispose();
        }
    }

    private void EditorView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width <= InspectorCollapseWidth;
        if (narrow == _isNarrow && ActualWidth > 0)
        {
            return;
        }

        _isNarrow = narrow;
        InspectorOverlay.Visibility = Visibility.Collapsed;
        InspectorColumn.Width = new GridLength(!_isNarrow && _wideInspectorVisible ? 324 : 0);
        InspectorSeparatorColumn.Width = new GridLength(!_isNarrow && _wideInspectorVisible ? 1 : 0);
        DesktopInspector.Visibility = !_isNarrow && _wideInspectorVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorSeparator.Visibility = DesktopInspector.Visibility;
    }

    private void InspectorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isNarrow)
        {
            InspectorOverlay.Visibility = InspectorOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        _wideInspectorVisible = !_wideInspectorVisible;
        InspectorColumn.Width = new GridLength(_wideInspectorVisible ? 324 : 0);
        InspectorSeparatorColumn.Width = new GridLength(_wideInspectorVisible ? 1 : 0);
        DesktopInspector.Visibility = _wideInspectorVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorSeparator.Visibility = DesktopInspector.Visibility;
    }

    private void CloseOverlayInspector_Click(object sender, RoutedEventArgs e) => InspectorOverlay.Visibility = Visibility.Collapsed;

    private void Inspector_EditCommitted(object sender, InspectorEditCommittedEventArgs e)
    {
        if (e.ItemId is Guid itemId && Timeline.IsItemLocked(itemId))
        {
            ShowLockedTrackMessage(itemId);
            UpdateProjectPresentation();
            return;
        }

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

    private void Timeline_PlayheadChanged(object sender, PlayheadChangedEventArgs e)
    {
        Preview.Seek(e.PositionMilliseconds);
        ViewModel.Seek(e.PositionMilliseconds);
    }

    private void Timeline_SelectionChanged(object sender, EditorSelectionChangedEventArgs e) => ViewModel.Select(e.Selection);

    private void Timeline_TrackStateChanged(object sender, TimelineTrackStateChangedEventArgs e)
    {
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
            ShowMessage(InfoBarSeverity.Error, "Could not add media", "The dragged asset is no longer in this project.");
            return;
        }

        if (!TryAddAssetToTrack(asset, e.Track, e.PositionMilliseconds, out var error))
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not add media", error!);
        }
    }

    private async void Timeline_MediaFilesDropped(object sender, TimelineMediaFilesDroppedEventArgs e)
    {
        try
        {
            await ImportFilesToTimelineAsync(e.Files, e.Track, e.PositionMilliseconds);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Timeline_DropRejected(object sender, TimelineDropRejectedEventArgs e) =>
        ShowMessage(InfoBarSeverity.Warning, "Could not add dropped media", e.Message);

    private void Timeline_EditRequested(object sender, TimelineEditRequestedEventArgs e)
    {
        if (e.ItemId is Guid itemId)
        {
            SelectTimelineItem(itemId);
        }

        switch (e.Kind)
        {
            case TimelineEditKind.ReorderVideo when e.ItemId is Guid id:
                ViewModel.ReorderVideoItem(id, e.IntValue);
                break;
            case TimelineEditKind.SplitVideo when e.ItemId is Guid id:
                ViewModel.SplitVideoItem(id, e.LongValue);
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
                ViewModel.DeleteSelection();
                break;
            case TimelineEditKind.Duplicate:
                ViewModel.DuplicateSelection();
                break;
            case TimelineEditKind.Undo:
                ViewModel.Undo();
                break;
            case TimelineEditKind.Redo:
                ViewModel.Redo();
                break;
            case TimelineEditKind.ShowSourceFile:
                ShowTimelineSourceInExplorer();
                break;
        }
    }

    private void Preview_SeekRequested(object? sender, PlayheadChangedEventArgs e)
    {
        Preview.Seek(e.PositionMilliseconds);
        ViewModel.Seek(e.PositionMilliseconds);
    }

    private void Preview_PlaybackPositionChanged(object? sender, PlayheadChangedEventArgs e) =>
        ViewModel.Seek(e.PositionMilliseconds);

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
        var normalized = AppSettings.Normalize(new AppSettings { TimelineHeight = height }).TimelineHeight;
        if (Math.Abs(TimelineRow.Height.Value - normalized) < 0.01)
        {
            return;
        }

        TimelineRow.Height = new GridLength(normalized);
        if (notify)
        {
            _workspaceSettings.TimelineHeight = normalized;
            RaiseWorkspaceSettingsChanged();
        }
    }

    private void RaiseWorkspaceSettingsChanged() =>
        WorkspaceSettingsChanged?.Invoke(this, new WorkspaceSettingsChangedEventArgs(
            Timeline.Zoom,
            TimelineHeight,
            Preview.IsLooping,
            _workspaceSettings.LastExportFolder));

    private void TimelineResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement handle || !handle.CapturePointer(e.Pointer))
        {
            return;
        }

        _timelineResizePointerId = e.Pointer.PointerId;
        _timelineResizeStartY = e.GetCurrentPoint(EditorRoot).Position.Y;
        _timelineResizeStartHeight = TimelineHeight;
        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_timelineResizePointerId != e.Pointer.PointerId)
        {
            return;
        }

        SetTimelineHeight(_timelineResizeStartHeight - (e.GetCurrentPoint(EditorRoot).Position.Y - _timelineResizeStartY));
        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_timelineResizePointerId != e.Pointer.PointerId)
        {
            return;
        }

        if (sender is UIElement handle)
        {
            handle.ReleasePointerCapture(e.Pointer);
        }

        _timelineResizePointerId = 0;
        e.Handled = true;
    }

    private void TimelineResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _timelineResizePointerId = 0;

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
            ShowMessage(InfoBarSeverity.Error, "Could not add media", "The asset is no longer in this project.");
            return;
        }

        var track = asset.Kind == ProjectAssetKind.Audio
            ? TimelineTrackKind.Audio
            : TimelineTrackKind.Video;
        if (!TryAddAssetToTrack(asset, track, ViewModel.PlayheadMilliseconds, out var error))
        {
            ShowMessage(InfoBarSeverity.Warning, "Could not add media", error!);
            return;
        }
    }

    private void ToolPanel_RelinkAssetRequested(object? sender, AssetActionEventArgs e) =>
        RelinkAssetRequested?.Invoke(this, e);

    private void ToolPanel_ShowAssetInExplorerRequested(object? sender, AssetActionEventArgs e)
    {
        var asset = FindAsset(e.AssetId);
        if (asset is null || string.IsNullOrWhiteSpace(asset.SourcePath))
        {
            ShowMessage(InfoBarSeverity.Error, "Could not open Explorer", "The source path is unavailable.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            startInfo.ArgumentList.Add($"/select,{asset.SourcePath}");
            Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowMessage(InfoBarSeverity.Error, $"Could not show '{asset.FileName}'", "File Explorer could not be opened for this source path.");
        }
    }

    private void SelectTimelineItem(Guid itemId)
    {
        var selection = ViewModel.Project.VideoItems.Any(item => item.Id == itemId)
            ? new EditorSelection(EditorSelectionKind.VideoItem, itemId)
            : ViewModel.Project.AudioItems.Any(item => item.Id == itemId)
                ? new EditorSelection(EditorSelectionKind.AudioItem, itemId)
                : ViewModel.Project.TextItems.Any(item => item.Id == itemId)
                    ? new EditorSelection(EditorSelectionKind.TextItem, itemId)
                    : EditorSelection.None;
        if (selection != EditorSelection.None)
        {
            ViewModel.Select(selection);
        }
    }

    private void ShowTimelineSourceInExplorer()
    {
        var assetId = ViewModel.Selection switch
        {
            { Kind: EditorSelectionKind.VideoItem, ItemId: Guid videoId } => ViewModel.Project.VideoItems.FirstOrDefault(item => item.Id == videoId)?.AssetId,
            { Kind: EditorSelectionKind.AudioItem, ItemId: Guid audioId } => ViewModel.Project.AudioItems.FirstOrDefault(item => item.Id == audioId)?.AssetId,
            _ => null
        };
        if (assetId is Guid foundAssetId)
        {
            ToolPanel_ShowAssetInExplorerRequested(this, new AssetActionEventArgs(foundAssetId));
        }
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
            await ImportFilesAsync(e.Files);
        }
        catch (OperationCanceledException)
        {
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
                InspectorOverlay.Visibility = Visibility.Visible;
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
            ShowMessage(InfoBarSeverity.Warning, "Export in progress", "Cancel the export or wait for it to finish before returning Home.");
            return;
        }

        if (!await SaveAsync())
        {
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
        var name = ProjectNameBox.Text.Trim();
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

        if (controlDown)
        {
            switch (e.Key)
            {
                case VirtualKey.S:
                    await SaveAsync();
                    e.Handled = true;
                    return;
                case VirtualKey.N:
                    e.Handled = true;
                    if (!shouldCreateNewProject)
                    {
                        return;
                    }

                    if (_isRendering)
                    {
                        ShowMessage(InfoBarSeverity.Warning, "Export in progress", "Cancel the export or wait for it to finish before creating another project.");
                        return;
                    }
                    if (await SaveAsync())
                    {
                        NewProjectRequested?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                case VirtualKey.Z:
                    ViewModel.Undo();
                    e.Handled = true;
                    return;
                case VirtualKey.Y:
                    ViewModel.Redo();
                    e.Handled = true;
                    return;
                case VirtualKey.B:
                    if (!TryRejectLockedSelectionEdit())
                    {
                        ViewModel.SplitSelection();
                    }
                    e.Handled = true;
                    return;
                case VirtualKey.D:
                    if (!TryRejectLockedSelectionEdit())
                    {
                        ViewModel.DuplicateSelection();
                    }
                    e.Handled = true;
                    return;
            }
        }

        switch (e.Key)
        {
            case VirtualKey.Delete:
                if (!TryRejectLockedSelectionEdit())
                {
                    ViewModel.DeleteSelection();
                }
                e.Handled = true;
                break;
            case VirtualKey.Space:
                Preview.PlayPause();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                if (InspectorOverlay.Visibility == Visibility.Visible)
                {
                    InspectorOverlay.Visibility = Visibility.Collapsed;
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

    private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

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

    private void ShowImportResults(IReadOnlyList<ImportResult> results)
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

            ShowMessage(
                issues.All(result => result.IsDuplicate) ? InfoBarSeverity.Informational : InfoBarSeverity.Error,
                imported > 0 ? $"Imported {imported} file(s) with issues" : "No files were imported",
                details);
        }
        else
        {
            ShowMessage(InfoBarSeverity.Success, "Import complete", $"Imported {imported} file(s) into this project.");
        }
    }

    private void ShowMessage(InfoBarSeverity severity, string title, string message)
    {
        EditorInfoBar.Severity = severity;
        EditorInfoBar.Title = title;
        EditorInfoBar.Message = message;
        EditorInfoBar.IsOpen = true;
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(InfoBarSeverity.Warning, "Media updated", "The project reference was updated, but its old thumbnail cache could not be removed.");
        }
    }

    private void CommitImportResults(IReadOnlyList<ImportResult> results)
    {
        EnsureActive();
        var imported = results.Where(result => result.Asset is not null).Select(result => result.Asset!).ToList();
        if (imported.Count > 0)
        {
            ViewModel.AddImportedAssets(imported);
        }

        ToolPanel.RefreshAssets();
        ShowImportResults(results);
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
