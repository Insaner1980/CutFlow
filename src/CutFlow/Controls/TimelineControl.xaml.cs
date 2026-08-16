using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace CutFlow.Controls;

public sealed partial class TimelineControl : UserControl
{
    private const double ClipMargin = 3;
    private const double MinimumClipVisualWidth = 18;
    private const double TrimHitWidth = 8;
    private const double ContentEndPadding = 48;
    private ProjectDocument? _project;
    private EditorSelection _selection = EditorSelection.None;
    private EditorSelection _selectionBeforePointerPress = EditorSelection.None;
    private TimelineScale _scale = new(80);
    private IReadOnlyList<TimelineItemBounds> _videoBounds = [];
    private string _projectRootPath = string.Empty;
    private long _durationMilliseconds;
    private long _playheadMilliseconds;
    private string _durationText = "00:00:00:00";
    private readonly TimelineTrackLocks _trackLocks = new();
    private CancellationToken _lifetimeToken;
    private CancellationTokenSource? _thumbnailRenderCts;
    private long _thumbnailRenderGeneration;
    private bool _updatingTrackState;
    private bool _updatingZoom;
    private bool _snappingEnabled = true;
    private TimelineDragOperation _dragOperation;
    private FrameworkElement? _dragElement;
    private Pointer? _dragPointer;
    private uint _dragPointerId;
    private double _dragInitialPointerX;
    private ClipTag? _dragTag;
    private long _previewValue;
    private int _previewIndex;

    public TimelineControl()
    {
        InitializeComponent();
    }

    public event EventHandler<PlayheadChangedEventArgs>? PlayheadChanged;
    public event EventHandler<EditorSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TimelineEditRequestedEventArgs>? EditRequested;
    public event EventHandler<TimelineAssetDroppedEventArgs>? AssetDropped;
    public event EventHandler<TimelineMediaFilesDroppedEventArgs>? MediaFilesDropped;
    public event EventHandler<TimelineDropRejectedEventArgs>? DropRejected;
    public event EventHandler<TimelineTrackStateChangedEventArgs>? TrackStateChanged;
    public event EventHandler? ZoomChanged;
    public double Zoom => _scale.PixelsPerSecond;

    internal void SetLifetimeToken(CancellationToken lifetimeToken) => _lifetimeToken = lifetimeToken;

    internal void StopThumbnailWork()
    {
        _thumbnailRenderCts?.Cancel();
        _thumbnailRenderCts?.Dispose();
        _thumbnailRenderCts = null;
        _thumbnailRenderGeneration++;
    }

    public void SetZoom(double zoom) => SetZoom(zoom, TimelineScroller.ViewportWidth / 2);

    public void SetTimeline(
        ProjectDocument project,
        EditorSelection selection,
        long playheadMilliseconds,
        string playheadText,
        string durationText,
        bool canUndo,
        bool canRedo,
        string projectRootPath)
    {
        CancelPointerInteraction();
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _selection = selection;
        _projectRootPath = projectRootPath ?? string.Empty;
        _durationMilliseconds = TimelineEditingService.CalculateProjectDuration(project);
        _playheadMilliseconds = Math.Clamp(playheadMilliseconds, 0, _durationMilliseconds);
        _durationText = durationText;
        _videoBounds = TimelineLayoutProjection.GetVideoBounds(project);
        TimelineTimecode.Text = $"{playheadText} / {durationText}";
        UndoButton.IsEnabled = canUndo;
        RedoButton.IsEnabled = canRedo;
        _updatingTrackState = true;
        try
        {
            VideoVisibilityButton.IsChecked = project.Settings.VideoTrackVisible;
            TextVisibilityButton.IsChecked = project.Settings.TextTrackVisible;
            AudioMuteButton.IsChecked = project.Settings.AudioTrackMuted;
            UpdateTrackStateButtonPresentation(VideoVisibilityButton, project.Settings.VideoTrackVisible);
            UpdateTrackStateButtonPresentation(TextVisibilityButton, project.Settings.TextTrackVisible);
            UpdateTrackStateButtonPresentation(AudioMuteButton, project.Settings.AudioTrackMuted);
        }
        finally
        {
            _updatingTrackState = false;
        }
        UpdateCommandStates();
        UpdateContentWidth();
        RenderRuler();
        RenderClips();
        UpdatePlayhead();
    }

    public void UpdatePlaybackPosition(long playheadMilliseconds, string playheadText, bool ensureVisible)
    {
        _playheadMilliseconds = TimelinePlaybackMath.ClampPosition(playheadMilliseconds, _durationMilliseconds);
        TimelineTimecode.Text = $"{playheadText} / {_durationText}";
        UpdatePlayhead();
        if (!ensureVisible) return;
        var offset = TimelinePlaybackMath.EnsureVisibleOffset(
            _scale.TimeToPixels(_playheadMilliseconds),
            TimelineScroller.HorizontalOffset,
            TimelineScroller.ViewportWidth,
            TimelineContent.Width);
        if (Math.Abs(offset - TimelineScroller.HorizontalOffset) > 0.5)
        {
            TimelineScroller.ChangeView(offset, null, null, disableAnimation: true);
        }
    }

    public void SetSelection(EditorSelection selection)
    {
        CancelPointerInteraction(render: true);
        _selection = selection;
        UpdateCommandStates();
        UpdateSelectionStyles();
    }

    public bool IsSelectionLocked(EditorSelection selection) => !_trackLocks.CanEdit(selection.Kind);

    public bool IsTrackLocked(TimelineTrackKind track) => _trackLocks.IsLocked(track);

    public bool IsItemLocked(Guid itemId)
    {
        return !_trackLocks.CanEdit(SelectionKindForItem(itemId));
    }

    public TimelineTrackKind TrackForItem(Guid itemId) =>
        TimelineDropPolicy.TrackForSelection(SelectionKindForItem(itemId));

    private EditorSelectionKind SelectionKindForItem(Guid itemId)
    {
        if (_project is null)
        {
            return EditorSelectionKind.None;
        }

        return _project.VideoItems.Any(item => item.Id == itemId)
            ? EditorSelectionKind.VideoItem
            : _project.TextItems.Any(item => item.Id == itemId)
                ? EditorSelectionKind.TextItem
                : _project.AudioItems.Any(item => item.Id == itemId)
                    ? EditorSelectionKind.AudioItem
                    : EditorSelectionKind.None;
    }

    private void UpdateContentWidth()
    {
        var viewport = Math.Max(1, TimelineScroller.ViewportWidth);
        var durationWidth = _scale.TimeToPixels(Math.Max(1_000, _durationMilliseconds)) + ContentEndPadding;
        TimelineContent.Width = Math.Max(viewport, durationWidth);
    }

    private void RenderRuler()
    {
        RulerCanvas.Children.Clear();
        var interval = _scale.GetRulerIntervalMilliseconds();
        var ticks = _scale.GetVisibleRulerTicks(
            TimelineScroller.HorizontalOffset,
            Math.Max(1, TimelineScroller.ViewportWidth),
            Math.Max(_durationMilliseconds, _scale.PixelsToTime(TimelineContent.Width)));
        foreach (var tick in ticks)
        {
            var line = new Line
            {
                X1 = tick.Pixel,
                X2 = tick.Pixel,
                Y1 = 18,
                Y2 = 30,
                Stroke = Brush("BorderBrush"),
                StrokeThickness = 1
            };
            RulerCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = TimelineScale.FormatRulerLabel(tick.Milliseconds, interval),
                FontSize = 10,
                Foreground = Brush("TextTertiaryBrush")
            };
            Canvas.SetLeft(label, tick.Pixel + 4);
            Canvas.SetTop(label, 2);
            RulerCanvas.Children.Add(label);
        }
    }

    private void RenderClips()
    {
        CancelPointerInteraction();
        var thumbnailToken = RestartThumbnailWork();
        var thumbnailGeneration = ++_thumbnailRenderGeneration;
        VideoCanvas.Children.Clear();
        TextCanvas.Children.Clear();
        AudioCanvas.Children.Clear();
        if (_project is null)
        {
            return;
        }

        var assets = _project.Assets.ToDictionary(asset => asset.Id);
        foreach (var bound in _videoBounds)
        {
            var item = _project.VideoItems.First(candidate => candidate.Id == bound.ItemId);
            assets.TryGetValue(item.AssetId, out var asset);
            AddClip(VideoCanvas, CreateVideoCard(item, asset, bound, thumbnailGeneration, thumbnailToken), bound);
        }

        foreach (var item in _project.TextItems)
        {
            var bound = new TimelineItemBounds(item.Id, EditorSelectionKind.TextItem, Math.Max(0, item.StartMilliseconds), Math.Max(0, item.DurationMilliseconds));
            AddClip(TextCanvas, CreateTextCard(item, bound), bound);
        }

        foreach (var item in _project.AudioItems)
        {
            assets.TryGetValue(item.AssetId, out var asset);
            var bound = new TimelineItemBounds(item.Id, EditorSelectionKind.AudioItem, Math.Max(0, item.StartMilliseconds), Math.Max(0, item.DurationMilliseconds));
            AddClip(AudioCanvas, CreateAudioCard(item, asset, bound), bound);
        }
    }

    private Border CreateVideoCard(
        VideoTimelineItem item,
        ProjectAsset? asset,
        TimelineItemBounds bound,
        long thumbnailGeneration,
        CancellationToken thumbnailToken)
    {
        var isImage = asset?.Kind == ProjectAssetKind.Image;
        var badges = new List<string>();
        if (isImage) badges.Add("IMAGE");
        if (asset?.IsMissing == true) badges.Add("MISSING");
        if (item.IsMuted) badges.Add("MUTED");
        var title = asset?.FileName ?? "Unknown media";
        var content = CreateCardContent(title, badges, bound.DurationMilliseconds, null);
        _ = LoadThumbnailSafelyAsync(asset, content, thumbnailGeneration, thumbnailToken);
        return CreateCard(content, new ClipTag(bound, asset, item.SourceInMilliseconds, item.SourceOutMilliseconds, isImage));
    }

    private Border CreateTextCard(TextTimelineItem item, TimelineItemBounds bound)
    {
        var title = string.IsNullOrWhiteSpace(item.Text) ? "Empty text" : TimelineCardText.ToSingleLine(item.Text);
        var content = CreateCardContent(title, ["TEXT"], bound.DurationMilliseconds, null);
        return CreateCard(content, new ClipTag(bound, null, 0, 0, false));
    }

    private Border CreateAudioCard(AudioTimelineItem item, ProjectAsset? asset, TimelineItemBounds bound)
    {
        var badges = new List<string> { "AUDIO" };
        if (asset?.IsMissing == true) badges.Add("MISSING");
        if (item.IsMuted) badges.Add("MUTED");
        var content = CreateCardContent(asset?.FileName ?? "Unknown audio", badges, bound.DurationMilliseconds, null);
        return CreateCard(content, new ClipTag(bound, asset, item.SourceInMilliseconds, item.SourceOutMilliseconds, false));
    }

    private Grid CreateCardContent(string title, IReadOnlyList<string> badges, long durationMilliseconds, ImageSource? thumbnail)
    {
        var horizontalPadding = ResourceDouble("TimelineCardHorizontalPadding");
        var verticalPadding = ResourceDouble("TimelineCardVerticalPadding");
        var grid = new Grid { ColumnSpacing = 6, Padding = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = thumbnail is null ? new GridLength(0) : new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (thumbnail is not null)
        {
            grid.Children.Add(new Image { Source = thumbnail, Stretch = Stretch.UniformToFill });
        }

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        Grid.SetColumn(stack, 1);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = ResourceDouble("TimelineCardTitleFontSize"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsTextSelectionEnabled = false
        });
        var detail = string.Join("  ", badges.Append(FormatDuration(durationMilliseconds)));
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = ResourceDouble("TimelineCardDetailFontSize"),
            Foreground = badges.Contains("MISSING") ? Brush("WarningBrush") : Brush("TextSecondaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsTextSelectionEnabled = false
        });
        grid.Children.Add(stack);
        return grid;
    }

    private Border CreateCard(Grid content, ClipTag tag)
    {
        var selected = _selection.ItemId == tag.Bounds.ItemId && _selection.Kind == tag.Bounds.Kind;
        var canEdit = _trackLocks.CanEdit(tag.Bounds.Kind);
        content.Children.Add(CreateTrimHandle(HorizontalAlignment.Left, selected && canEdit));
        content.Children.Add(CreateTrimHandle(HorizontalAlignment.Right, selected && canEdit));
        var card = new Border
        {
            Tag = tag,
            Background = Brush(selected ? "SurfaceHoverBrush" : "SurfaceElevatedBrush"),
            BorderBrush = Brush(selected ? "AccentBrush" : "BorderBrush"),
            BorderThickness = new Thickness(selected ? 2 : 1),
            CornerRadius = new CornerRadius(ResourceDouble("TimelineCardCornerRadius")),
            Child = content,
            Opacity = canEdit ? 1 : 0.68
        };
        AutomationProperties.SetName(card, $"{tag.Bounds.Kind} clip");
        return card;
    }

    private Border CreateTrimHandle(HorizontalAlignment alignment, bool selected) => new()
    {
        Tag = "TrimHandle",
        Width = 3,
        Margin = new Thickness(1, 4, 1, 4),
        HorizontalAlignment = alignment,
        Background = Brush("AccentBrush"),
        CornerRadius = new CornerRadius(2),
        IsHitTestVisible = false,
        Visibility = selected ? Visibility.Visible : Visibility.Collapsed
    };

    private MenuFlyout CreateContextMenu(Guid itemId, EditorSelectionKind kind)
    {
        var menu = new MenuFlyout();
        MenuFlyoutItem? split = null;
        if (kind == EditorSelectionKind.VideoItem)
        {
            split = new MenuFlyoutItem { Text = "Split" };
            split.Click += (_, _) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.SplitVideo, itemId, _playheadMilliseconds));
            menu.Items.Add(split);
        }

        MenuFlyoutItem? duplicate = null;
        if (TimelineContextCommands.SupportsDuplicate(kind))
        {
            duplicate = new MenuFlyoutItem { Text = "Duplicate" };
            duplicate.Click += (_, _) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.Duplicate, itemId));
            menu.Items.Add(duplicate);
        }

        var delete = new MenuFlyoutItem { Text = "Delete" };
        delete.Click += (_, _) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.Delete, itemId));
        menu.Items.Add(delete);
        MenuFlyoutItem? show = null;
        if (kind is EditorSelectionKind.VideoItem or EditorSelectionKind.AudioItem)
        {
            show = new MenuFlyoutItem { Text = "Show source file" };
            show.Click += (_, _) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.ShowSourceFile, itemId));
            menu.Items.Add(show);
        }

        menu.Opening += (_, _) =>
        {
            var state = TimelineContextCommands.Resolve(_project, itemId, _trackLocks, _playheadMilliseconds);
            if (split is not null) split.IsEnabled = state.CanSplit;
            if (duplicate is not null) duplicate.IsEnabled = state.CanDuplicate;
            delete.IsEnabled = state.CanDelete;
            if (show is not null) show.IsEnabled = state.CanShowSourceFile;
        };

        return menu;
    }

    private void AddClip(Canvas canvas, Border card, TimelineItemBounds bound)
    {
        var geometry = _scale.GetCardGeometry(bound, MinimumClipVisualWidth, ClipMargin);
        var height = Math.Max(22, canvas.ActualHeight > 0 ? canvas.ActualHeight - 8 : 36);
        var hitTarget = new ContentControl
        {
            Tag = card.Tag,
            Width = geometry.HitWidth,
            Height = height,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsTabStop = true,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = card,
            ContextFlyout = card.Tag is ClipTag tag ? CreateContextMenu(tag.Bounds.ItemId, tag.Bounds.Kind) : null
        };
        card.Width = geometry.VisualWidth;
        card.Height = height;
        card.Margin = new Thickness(ClipMargin / 2, 0, 0, 0);
        card.HorizontalAlignment = HorizontalAlignment.Left;
        card.IsHitTestVisible = false;
        if (card.Tag is ClipTag clipTag)
        {
            AutomationProperties.SetName(hitTarget, $"{clipTag.Bounds.Kind} clip");
            ToolTipService.SetToolTip(hitTarget, clipTag.Asset?.SourcePath ?? "Text item");
        }

        hitTarget.GotFocus += Clip_GotFocus;
        hitTarget.PointerPressed += Clip_PointerPressed;
        hitTarget.PointerMoved += Drag_PointerMoved;
        hitTarget.PointerReleased += Drag_PointerReleased;
        hitTarget.PointerCanceled += Drag_PointerCanceled;
        hitTarget.PointerCaptureLost += Drag_PointerCaptureLost;
        Canvas.SetLeft(hitTarget, geometry.HitLeft);
        Canvas.SetTop(hitTarget, 4);
        Canvas.SetZIndex(hitTarget, IsSelected(bound) ? 1 : 0);
        canvas.Children.Add(hitTarget);
    }

    private void Clip_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ClipTag tag })
        {
            return;
        }

        var selection = new EditorSelection(tag.Bounds.Kind, tag.Bounds.ItemId);
        if (_selection == selection)
        {
            return;
        }

        _selection = selection;
        SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs(selection));
        UpdateCommandStates();
        UpdateSelectionStyles();
    }

    private void Clip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement hitTarget ||
            hitTarget.Tag is not ClipTag tag ||
            !e.GetCurrentPoint(hitTarget).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_dragPointer is not null)
        {
            e.Handled = true;
            return;
        }

        Focus(FocusState.Pointer);
        _selectionBeforePointerPress = _selection;
        _selection = new EditorSelection(tag.Bounds.Kind, tag.Bounds.ItemId);
        SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs(_selection));
        UpdateCommandStates();
        UpdateSelectionStyles();

        if (!_trackLocks.CanEdit(tag.Bounds.Kind))
        {
            CycleOverlappingAudioSelection(tag, e.GetCurrentPoint(TimelineContent).Position.X);
            e.Handled = true;
            return;
        }

        var localX = e.GetCurrentPoint(hitTarget).Position.X;
        var hit = TimelineCardHitTest.Resolve(localX, hitTarget.ActualWidth, TrimHitWidth);
        var operation = hit == TimelineCardHit.Start
            ? tag.Bounds.Kind switch
            {
                EditorSelectionKind.VideoItem => TimelineDragOperation.VideoTrimStart,
                EditorSelectionKind.AudioItem => TimelineDragOperation.AudioTrimStart,
                EditorSelectionKind.TextItem => TimelineDragOperation.TextTrimStart,
                _ => TimelineDragOperation.None
            }
            : hit == TimelineCardHit.End
                ? tag.Bounds.Kind switch
                {
                    EditorSelectionKind.VideoItem => TimelineDragOperation.VideoTrimEnd,
                    EditorSelectionKind.AudioItem => TimelineDragOperation.AudioTrimEnd,
                    EditorSelectionKind.TextItem => TimelineDragOperation.TextTrimEnd,
                    _ => TimelineDragOperation.None
                }
                : tag.Bounds.Kind switch
                {
                    EditorSelectionKind.VideoItem => TimelineDragOperation.VideoReorder,
                    EditorSelectionKind.AudioItem => TimelineDragOperation.AudioMove,
                    EditorSelectionKind.TextItem => TimelineDragOperation.TextMove,
                    _ => TimelineDragOperation.None
                };
        BeginDrag(hitTarget, tag, operation, e);
        e.Handled = true;
    }

    private void BeginDrag(
        FrameworkElement element,
        ClipTag tag,
        TimelineDragOperation operation,
        PointerRoutedEventArgs e)
    {
        if (operation == TimelineDragOperation.None || !element.CapturePointer(e.Pointer))
        {
            return;
        }

        _dragOperation = operation;
        _dragElement = element;
        _dragTag = tag;
        _dragPointer = e.Pointer;
        _dragPointerId = e.Pointer.PointerId;
        _dragInitialPointerX = e.GetCurrentPoint(TimelineContent).Position.X;
        _previewValue = TimelineDragPreview.InitialValue(
            _dragOperation,
            tag.Bounds,
            tag.SourceInMilliseconds,
            tag.SourceOutMilliseconds,
            tag.IsImage);
        _previewIndex = _project?.VideoItems.FindIndex(item => item.Id == tag.Bounds.ItemId) ?? 0;
    }

    private void SeekSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement surface ||
            _dragPointer is not null ||
            !e.GetCurrentPoint(surface).Properties.IsLeftButtonPressed ||
            !surface.CapturePointer(e.Pointer))
        {
            return;
        }

        Focus(FocusState.Pointer);
        _dragOperation = TimelineDragOperation.Seek;
        _dragElement = surface;
        _dragPointer = e.Pointer;
        _dragPointerId = e.Pointer.PointerId;
        SeekFromPointer(e);
        e.Handled = true;
    }

    private void Playhead_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element ||
            _dragPointer is not null ||
            !e.GetCurrentPoint(element).Properties.IsLeftButtonPressed ||
            !element.CapturePointer(e.Pointer))
        {
            return;
        }

        Focus(FocusState.Pointer);
        _dragOperation = TimelineDragOperation.Playhead;
        _dragElement = element;
        _dragPointer = e.Pointer;
        _dragPointerId = e.Pointer.PointerId;
        SeekFromPointer(e);
        e.Handled = true;
    }

    private void Drag_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragOperation == TimelineDragOperation.None || e.Pointer.PointerId != _dragPointerId)
        {
            return;
        }

        if (_dragOperation is TimelineDragOperation.Seek or TimelineDragOperation.Playhead)
        {
            SeekFromPointer(e);
            e.Handled = true;
            return;
        }

        if (_dragElement is null || _dragTag is not ClipTag tag)
        {
            return;
        }

        var pointerX = e.GetCurrentPoint(TimelineContent).Position.X;
        var deltaMilliseconds = PixelsToSignedTime(pointerX - _dragInitialPointerX);
        var minimum = ProjectDocument.MinimumItemDurationMilliseconds;
        var startPixels = _scale.TimeToPixels(tag.Bounds.StartMilliseconds);
        var initialWidth = _scale.TimeToPixels(tag.Bounds.DurationMilliseconds);
        switch (_dragOperation)
        {
            case TimelineDragOperation.VideoReorder:
                Canvas.SetLeft(_dragElement, Math.Max(0, startPixels + pointerX - _dragInitialPointerX));
                _previewIndex = CalculateVideoTargetIndex(pointerX);
                break;
            case TimelineDragOperation.VideoTrimStart:
                {
                    var preview = TimelineDragPreview.CalculateVideoTrimStart(
                        tag.Bounds,
                        tag.SourceInMilliseconds,
                        deltaMilliseconds,
                        _snappingEnabled,
                        GetSnapEdges());
                    var acceptedDelta = preview.BoundaryMilliseconds - tag.Bounds.StartMilliseconds;
                    Canvas.SetLeft(_dragElement, Math.Max(0, startPixels + _scale.TimeToPixels(Math.Max(0, acceptedDelta))));
                    SetClipPreviewDuration(_dragElement, preview.DurationMilliseconds);
                    _previewValue = tag.IsImage ? preview.DurationMilliseconds : preview.SourceMilliseconds;
                    break;
                }
            case TimelineDragOperation.VideoTrimEnd:
                {
                    var preview = TimelineDragPreview.CalculateVideoTrimEnd(
                        tag.Bounds,
                        tag.SourceOutMilliseconds,
                        deltaMilliseconds,
                        _snappingEnabled,
                        GetSnapEdges());
                    SetClipPreviewDuration(_dragElement, preview.DurationMilliseconds);
                    _previewValue = tag.IsImage ? preview.DurationMilliseconds : preview.SourceMilliseconds;
                    break;
                }
            case TimelineDragOperation.AudioMove:
            case TimelineDragOperation.TextMove:
                {
                    var start = Snap(Math.Max(0, TimelineMath.SaturatingAdd(tag.Bounds.StartMilliseconds, deltaMilliseconds)));
                    Canvas.SetLeft(_dragElement, _scale.TimeToPixels(start));
                    _previewValue = start;
                    break;
                }
            case TimelineDragOperation.AudioTrimStart:
            case TimelineDragOperation.TextTrimStart:
                {
                    var desiredStart = Snap(Math.Max(0, TimelineMath.SaturatingAdd(tag.Bounds.StartMilliseconds, deltaMilliseconds)));
                    var maximumStart = tag.Bounds.EndMilliseconds - minimum;
                    var start = Math.Min(maximumStart, desiredStart);
                    Canvas.SetLeft(_dragElement, _scale.TimeToPixels(start));
                    SetClipPreviewDuration(_dragElement, tag.Bounds.EndMilliseconds - start);
                    _previewValue = _dragOperation == TimelineDragOperation.AudioTrimStart
                        ? tag.SourceInMilliseconds + start - tag.Bounds.StartMilliseconds
                        : start;
                    break;
                }
            case TimelineDragOperation.AudioTrimEnd:
            case TimelineDragOperation.TextTrimEnd:
                {
                    var minimumEnd = TimelineMath.SaturatingAdd(tag.Bounds.StartMilliseconds, minimum);
                    var end = Snap(Math.Max(minimumEnd, TimelineMath.SaturatingAdd(tag.Bounds.EndMilliseconds, deltaMilliseconds)));
                    SetClipPreviewDuration(_dragElement, end - tag.Bounds.StartMilliseconds);
                    _previewValue = _dragOperation == TimelineDragOperation.AudioTrimEnd
                        ? TimelineDragPreview.CalculateAudioTrimEndSource(
                            tag.SourceOutMilliseconds,
                            end,
                            tag.Bounds.EndMilliseconds)
                        : end;
                    break;
                }
        }

        e.Handled = true;
    }

    private void Drag_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragOperation == TimelineDragOperation.None || e.Pointer.PointerId != _dragPointerId)
        {
            return;
        }

        var request = CreateDragRequest();
        var clickedTag = _dragTag;
        var pointerX = e.GetCurrentPoint(TimelineContent).Position.X;
        var wasClick = Math.Abs(pointerX - _dragInitialPointerX) < 1;
        var capturedElement = _dragElement;
        var capturedPointer = _dragPointer;
        ClearDrag(render: false);
        if (capturedElement is not null && capturedPointer is not null)
        {
            capturedElement.ReleasePointerCapture(capturedPointer);
        }
        if (request is not null)
        {
            RaiseEdit(request);
        }

        if (wasClick && clickedTag is not null)
        {
            CycleOverlappingAudioSelection(clickedTag, pointerX);
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_lifetimeToken.IsCancellationRequested) return;
            RenderClips();
            UpdatePlayhead();
        });
        e.Handled = true;
    }

    private void Drag_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId == _dragPointerId)
        {
            CancelPointerInteraction(render: true);
        }
    }

    private void Drag_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragOperation != TimelineDragOperation.None && e.Pointer.PointerId == _dragPointerId)
        {
            ClearDrag(render: true);
        }
    }

    private TimelineEditRequestedEventArgs? CreateDragRequest()
    {
        if (_dragTag is not ClipTag tag)
        {
            return null;
        }

        return _dragOperation switch
        {
            TimelineDragOperation.VideoReorder => new(TimelineEditKind.ReorderVideo, tag.Bounds.ItemId, intValue: _previewIndex),
            TimelineDragOperation.VideoTrimStart when tag.IsImage => new(TimelineEditKind.SetImageDuration, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.VideoTrimEnd when tag.IsImage => new(TimelineEditKind.SetImageDuration, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.VideoTrimStart => new(TimelineEditKind.TrimVideoStart, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.VideoTrimEnd => new(TimelineEditKind.TrimVideoEnd, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.AudioMove => new(TimelineEditKind.MoveAudio, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.AudioTrimStart => new(TimelineEditKind.TrimAudioStart, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.AudioTrimEnd => new(TimelineEditKind.TrimAudioEnd, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.TextMove => new(TimelineEditKind.MoveText, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.TextTrimStart => new(TimelineEditKind.TrimTextStart, tag.Bounds.ItemId, _previewValue),
            TimelineDragOperation.TextTrimEnd => new(TimelineEditKind.TrimTextEnd, tag.Bounds.ItemId, _previewValue),
            _ => null
        };
    }

    private void ClearDrag(bool render)
    {
        _dragOperation = TimelineDragOperation.None;
        _dragElement = null;
        _dragTag = null;
        _dragPointer = null;
        _dragPointerId = 0;
        if (render)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_lifetimeToken.IsCancellationRequested) return;
                RenderClips();
                UpdatePlayhead();
            });
        }
    }

    internal void CancelPointerInteraction() => CancelPointerInteraction(render: false);

    private void CancelPointerInteraction(bool render)
    {
        var capturedElement = _dragElement;
        var capturedPointer = _dragPointer;
        var shouldRender = render && _dragOperation != TimelineDragOperation.None;
        ClearDrag(shouldRender);
        if (capturedElement is not null && capturedPointer is not null)
        {
            capturedElement.ReleasePointerCapture(capturedPointer);
        }
    }

    private void SeekFromPointer(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(TimelineContent).Position.X;
        var value = Snap(_scale.PixelsToTime(point));
        _playheadMilliseconds = Math.Clamp(value, 0, _durationMilliseconds);
        UpdatePlayhead();
        PlayheadChanged?.Invoke(this, new PlayheadChangedEventArgs(_playheadMilliseconds));
    }

    private void UpdatePlayhead()
    {
        Canvas.SetLeft(PlayheadHitTarget, _scale.TimeToPixels(_playheadMilliseconds) - PlayheadHitTarget.Width / 2);
        ToolTipService.SetToolTip(PlayheadHitTarget, FormatDuration(_playheadMilliseconds));
        UpdateCommandStates();
    }

    private void UpdateSelectionStyles()
    {
        foreach (var hitTarget in VideoCanvas.Children.Concat(TextCanvas.Children).Concat(AudioCanvas.Children).OfType<ContentControl>())
        {
            if (hitTarget.Tag is not ClipTag tag || hitTarget.Content is not Border card)
            {
                continue;
            }

            var selected = _selection.Kind == tag.Bounds.Kind && _selection.ItemId == tag.Bounds.ItemId;
            Canvas.SetZIndex(hitTarget, selected ? 1 : 0);
            card.BorderBrush = Brush(selected ? "AccentBrush" : "BorderBrush");
            card.BorderThickness = new Thickness(selected ? 2 : 1);
            card.Background = Brush(selected ? "SurfaceHoverBrush" : "SurfaceElevatedBrush");
            card.Opacity = _trackLocks.CanEdit(tag.Bounds.Kind) ? 1 : 0.68;
            if (card.Child is Grid content)
            {
                var showHandles = selected && _trackLocks.CanEdit(tag.Bounds.Kind);
                foreach (var handle in content.Children.OfType<Border>().Where(element => Equals(element.Tag, "TrimHandle")))
                {
                    handle.Visibility = showHandles ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    private bool IsSelected(TimelineItemBounds bound) =>
        _selection.Kind == bound.Kind && _selection.ItemId == bound.ItemId;

    private void CycleOverlappingAudioSelection(ClipTag hitTag, double pointerX)
    {
        if (_project is null ||
            hitTag.Bounds.Kind != EditorSelectionKind.AudioItem ||
            _selectionBeforePointerPress != new EditorSelection(EditorSelectionKind.AudioItem, hitTag.Bounds.ItemId))
        {
            return;
        }

        var itemId = TimelineOverlapSelection.GetPreviousAudioItemId(
            _project.AudioItems,
            _scale.PixelsToTime(pointerX),
            hitTag.Bounds.ItemId);
        if (itemId == hitTag.Bounds.ItemId)
        {
            return;
        }

        _selection = new EditorSelection(EditorSelectionKind.AudioItem, itemId);
        SelectionChanged?.Invoke(this, new EditorSelectionChangedEventArgs(_selection));
        UpdateCommandStates();
        UpdateSelectionStyles();
    }

    private void UpdateCommandStates()
    {
        var selectionCanEdit = _trackLocks.CanEdit(_selection.Kind);
        DeleteButton.IsEnabled = selectionCanEdit && _selection.ItemId is not null &&
            _selection.Kind is EditorSelectionKind.VideoItem or EditorSelectionKind.AudioItem or EditorSelectionKind.TextItem;
        SplitButton.IsEnabled = _selection is { Kind: EditorSelectionKind.VideoItem, ItemId: Guid id } &&
            selectionCanEdit &&
            _videoBounds.FirstOrDefault(bound => bound.ItemId == id) is var bound && bound.ItemId != Guid.Empty && CanSplit(bound);
    }

    private bool CanSplit(TimelineItemBounds bound) =>
        _playheadMilliseconds - bound.StartMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds &&
        bound.EndMilliseconds - _playheadMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds;

    private void RaiseEdit(TimelineEditRequestedEventArgs args)
    {
        if (IsMutatingEdit(args.Kind))
        {
            var kind = args.ItemId is Guid itemId ? SelectionKindForItem(itemId) : _selection.Kind;
            if (!_trackLocks.CanEdit(kind))
            {
                return;
            }
        }

        EditRequested?.Invoke(this, args);
    }

    private static bool IsMutatingEdit(TimelineEditKind kind) => kind is not
        TimelineEditKind.Undo and not
        TimelineEditKind.Redo and not
        TimelineEditKind.ShowSourceFile;

    private void SplitButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selection is { Kind: EditorSelectionKind.VideoItem, ItemId: Guid id })
        {
            RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.SplitVideo, id, _playheadMilliseconds));
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.Delete));
    private void UndoButton_Click(object sender, RoutedEventArgs e) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.Undo));
    private void RedoButton_Click(object sender, RoutedEventArgs e) => RaiseEdit(new TimelineEditRequestedEventArgs(TimelineEditKind.Redo));

    private void SnappingButton_Changed(object sender, RoutedEventArgs e)
    {
        _snappingEnabled = SnappingButton.IsChecked == true;
        var name = _snappingEnabled ? "Disable timeline snapping" : "Enable timeline snapping";
        AutomationProperties.SetName(SnappingButton, name);
        ToolTipService.SetToolTip(
            SnappingButton,
            _snappingEnabled ? "Disable 100 ms and edge snapping" : "Enable 100 ms and edge snapping");
    }

    private void SelectionToolButton_Click(object sender, RoutedEventArgs e) => SelectionToolButton.IsChecked = true;

    private void TrackLockButton_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button ||
            !Enum.TryParse<TimelineTrackKind>(button.Tag?.ToString(), out var track) ||
            track == TimelineTrackKind.None)
        {
            return;
        }

        var isLocked = button.IsChecked == true;
        _trackLocks.SetLocked(track, isLocked);
        if (button.Content is FontIcon icon)
        {
            icon.Glyph = isLocked ? "\uE72E" : "\uE785";
        }

        var action = isLocked ? "Unlock" : "Lock";
        var name = $"{action} {TimelineDropPolicy.DisplayName(track)} track";
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        UpdateCommandStates();
        RenderClips();
    }

    private void TrackStateButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingTrackState || sender is not ToggleButton button)
        {
            return;
        }

        var value = button.IsChecked == true;
        UpdateTrackStateButtonPresentation(button, value);
        var track = button.Tag?.ToString() switch
        {
            "VideoVisibility" => TimelineTrackState.VideoVisibility,
            "TextVisibility" => TimelineTrackState.TextVisibility,
            "AudioMute" => TimelineTrackState.AudioMute,
            _ => TimelineTrackState.None
        };
        if (track != TimelineTrackState.None)
        {
            TrackStateChanged?.Invoke(this, new TimelineTrackStateChangedEventArgs(track, value));
        }
    }

    private static void UpdateTrackStateButtonPresentation(ToggleButton button, bool value)
    {
        var (name, glyph) = button.Tag?.ToString() switch
        {
            "VideoVisibility" => (value ? "Hide V1 video track" : "Show V1 video track", value ? "\uE890" : "\uE8F5"),
            "TextVisibility" => (value ? "Hide T1 text track" : "Show T1 text track", value ? "\uE890" : "\uE8F5"),
            "AudioMute" => (value ? "Unmute A1 audio track" : "Mute A1 audio track", value ? "\uE74F" : "\uE767"),
            _ => (string.Empty, string.Empty)
        };
        if (name.Length == 0) return;
        if (button.Content is FontIcon icon) icon.Glyph = glyph;
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => SetZoom(_scale.PixelsPerSecond - 20, TimelineScroller.ViewportWidth / 2);
    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => SetZoom(_scale.PixelsPerSecond + 20, TimelineScroller.ViewportWidth / 2);

    private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingZoom && IsLoaded)
        {
            SetZoom(e.NewValue, TimelineScroller.ViewportWidth / 2);
        }
    }

    private void SetZoom(double requestedPixelsPerSecond, double anchorViewportX)
    {
        var zoom = Math.Clamp(requestedPixelsPerSecond, TimelineScale.MinimumPixelsPerSecond, TimelineScale.MaximumPixelsPerSecond);
        if (Math.Abs(zoom - _scale.PixelsPerSecond) < 0.01)
        {
            return;
        }

        var offset = TimelineScale.CalculateAnchoredOffset(
            _scale.PixelsPerSecond,
            zoom,
            TimelineScroller.HorizontalOffset,
            anchorViewportX,
            _durationMilliseconds,
            TimelineScroller.ViewportWidth,
            ContentEndPadding);
        _scale = new TimelineScale(zoom);
        _updatingZoom = true;
        ZoomSlider.Value = zoom;
        _updatingZoom = false;
        UpdateContentWidth();
        RenderRuler();
        RenderClips();
        UpdatePlayhead();
        TimelineScroller.UpdateLayout();
        TimelineScroller.ChangeView(offset, null, null, disableAnimation: true);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        var viewport = TimelineScroller.ViewportWidth;
        if (viewport <= 0)
        {
            return;
        }

        var fit = _durationMilliseconds <= 0
            ? 80
            : Math.Clamp((viewport - 32) * 1_000d / _durationMilliseconds, TimelineScale.MinimumPixelsPerSecond, TimelineScale.MaximumPixelsPerSecond);
        SetZoom(fit, 0);
        TimelineScroller.ChangeView(0, null, null, disableAnimation: true);
    }

    private void TimelineContent_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            return;
        }

        var point = e.GetCurrentPoint(TimelineScroller);
        if (point.Properties.IsHorizontalMouseWheel || point.Properties.MouseWheelDelta == 0)
        {
            return;
        }

        SetZoom(
            TimelineScale.CalculateWheelZoomTarget(_scale.PixelsPerSecond, point.Properties.MouseWheelDelta),
            point.Position.X);
        e.Handled = true;
    }

    private void TimelineScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) => RenderRuler();

    private void TimelineScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateContentWidth();
        RenderRuler();
    }

    private void TimelineContent_DragOver(object sender, DragEventArgs e)
    {
        if (!HasSupportedDropFormat(e.DataView))
        {
            return;
        }

        var track = HitTestTrack(e.GetPosition(TimelineContent).Y);
        var hasLibraryAsset = e.DataView.Contains(MediaAssetDragPayload.FormatId);
        var hasStorageItems = e.DataView.Contains(StandardDataFormats.StorageItems);
        var canAccept = TimelineDropPolicy.CanAcceptDrag(
            track,
            _trackLocks.IsLocked(track),
            hasLibraryAsset,
            hasStorageItems,
            e.DataView.Properties.FileTypes);
        var caption = track switch
        {
            TimelineTrackKind.None => "Drop media on V1 or audio on A1",
            TimelineTrackKind.Text => "Add text from the Text panel",
            _ when _trackLocks.IsLocked(track) => $"Unlock {TimelineDropPolicy.DisplayName(track)} to add media",
            TimelineTrackKind.Video when !canAccept => "Drop video or images on V1",
            TimelineTrackKind.Audio when !canAccept => "Drop audio on A1",
            _ => $"Add to {TimelineDropPolicy.DisplayName(track)}"
        };
        e.DragUIOverride.Caption = caption;
        e.DragUIOverride.IsCaptionVisible = true;
        e.AcceptedOperation = canAccept ? DataPackageOperation.Copy : DataPackageOperation.None;
    }

    private async void TimelineContent_Drop(object sender, DragEventArgs e)
    {
        var lifetimeToken = _lifetimeToken;
        if (lifetimeToken.IsCancellationRequested)
        {
            return;
        }

        var track = HitTestTrack(e.GetPosition(TimelineContent).Y);
        if (track is not TimelineTrackKind.Video and not TimelineTrackKind.Audio)
        {
            RejectDrop(track == TimelineTrackKind.Text
                ? "Files and media assets cannot be dropped on T1. Add text from the Text panel."
                : "Drop video or images on V1, or audio on A1.");
            return;
        }

        if (_trackLocks.IsLocked(track))
        {
            RejectDrop($"{TimelineDropPolicy.DisplayName(track)} is locked. Unlock the track before adding media.");
            return;
        }

        var position = DropPosition(e.GetPosition(TimelineContent).X);
        try
        {
            if (e.DataView.Contains(MediaAssetDragPayload.FormatId))
            {
                var payload = await e.DataView.GetDataAsync(MediaAssetDragPayload.FormatId);
                lifetimeToken.ThrowIfCancellationRequested();
                if (!MediaAssetDragPayload.TryParseAssetId(payload, out var assetId))
                {
                    RejectDrop($"{AppInfo.ProductName} could not identify the dragged library asset. Try dragging it again.");
                    return;
                }

                var asset = _project?.Assets.FirstOrDefault(candidate => candidate.Id == assetId);
                if (asset is null)
                {
                    RejectDrop("The dragged asset is no longer in this project.");
                    return;
                }

                if (!TimelineDropPolicy.Evaluate(track, asset.Kind, isLocked: false).IsAllowed)
                {
                    RejectDrop(track == TimelineTrackKind.Video
                        ? $"'{asset.FileName}' is audio. Drop it on A1."
                        : $"'{asset.FileName}' is visual media. Drop it on V1.");
                    return;
                }

                AssetDropped?.Invoke(this, new TimelineAssetDroppedEventArgs(assetId, track, position));
                e.AcceptedOperation = DataPackageOperation.Copy;
                return;
            }

            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            var result = await MediaDropReader.ReadAsync(
                async () => await e.DataView.GetStorageItemsAsync(),
                lifetimeToken);
            lifetimeToken.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                RejectDrop(result.ErrorMessage!);
            }
            else if (result.Files.Count == 0)
            {
                RejectDrop(result.RejectedItems.Count > 0
                    ? string.Join(" ", result.RejectedItems.Take(3).Select(item => $"'{item.ItemName}': {item.Message}"))
                    : $"The drop did not contain any files {AppInfo.ProductName} can import.");
            }
            else
            {
                var scope = track == TimelineTrackKind.Video ? MediaImportScope.Visual : MediaImportScope.Audio;
                var hasCompatibleFile = result.Files.Any(file =>
                    MediaImportService.TryResolveLocalSourcePath(file.Path, out var path, out _) &&
                    scope.Allows(path));
                if (!hasCompatibleFile)
                {
                    RejectDrop(track == TimelineTrackKind.Video
                        ? "Drop MP4, PNG, or JPEG files on V1."
                        : "Drop MP3 or WAV files on A1.");
                    e.AcceptedOperation = DataPackageOperation.None;
                    return;
                }

                MediaFilesDropped?.Invoke(this, new TimelineMediaFilesDroppedEventArgs(
                    result.Files,
                    track,
                    position,
                    result.RejectedItems.Select(item => $"'{item.ItemName}': {item.Message}").ToList()));
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            if (!lifetimeToken.IsCancellationRequested)
            {
                RejectDrop($"{AppInfo.ProductName} could not read the dropped item. Try importing it with the file picker.");
            }
        }
    }

    private TimelineTrackKind HitTestTrack(double pointerY) => TimelineDropPolicy.HitTest(
        pointerY,
        RulerCanvas.ActualHeight,
        VideoCanvas.ActualHeight,
        TextCanvas.ActualHeight,
        AudioCanvas.ActualHeight);

    private long DropPosition(double pointerX) => Math.Clamp(
        _scale.PixelsToTime(Math.Max(0, pointerX)),
        0,
        _durationMilliseconds);

    private static bool HasSupportedDropFormat(DataPackageView dataView) =>
        dataView.Contains(MediaAssetDragPayload.FormatId) ||
        dataView.Contains(StandardDataFormats.StorageItems);

    private void RejectDrop(string message) =>
        DropRejected?.Invoke(this, new TimelineDropRejectedEventArgs(message));

    private void TimelineControl_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or RichEditBox or PasswordBox)
        {
            return;
        }

        if (e.Key == VirtualKey.Home)
        {
            RequestSeek(0);
        }
        else if (e.Key == VirtualKey.End)
        {
            RequestSeek(_durationMilliseconds);
        }
        else if (e.Key == VirtualKey.Left)
        {
            RequestSeek(TimelinePlaybackMath.StepPosition(_playheadMilliseconds, -100, _durationMilliseconds));
        }
        else if (e.Key == VirtualKey.Right)
        {
            RequestSeek(TimelinePlaybackMath.StepPosition(_playheadMilliseconds, 100, _durationMilliseconds));
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    private void RequestSeek(long milliseconds)
    {
        _playheadMilliseconds = milliseconds;
        UpdatePlayhead();
        PlayheadChanged?.Invoke(this, new PlayheadChangedEventArgs(milliseconds));
    }

    private int CalculateVideoTargetIndex(double pointerX)
    {
        return TimelineLayoutProjection.GetVideoTargetIndex(
            _videoBounds,
            _dragTag?.Bounds.ItemId ?? Guid.Empty,
            _scale.PixelsToTime(pointerX));
    }

    private void SetClipPreviewDuration(FrameworkElement hitTarget, long durationMilliseconds)
    {
        var hitWidth = _scale.TimeToPixels(durationMilliseconds);
        hitTarget.Width = hitWidth;
        if (hitTarget is ContentControl { Content: Border card })
        {
            card.Width = Math.Max(MinimumClipVisualWidth, hitWidth - ClipMargin);
        }
    }

    private long Snap(long value)
    {
        return TimelineSnapper.Snap(value, _snappingEnabled, GetSnapEdges());
    }

    private IEnumerable<long> GetSnapEdges() =>
        TimelineLayoutProjection.GetAllBounds(_project ?? new ProjectDocument())
            .SelectMany(bound => new[] { bound.StartMilliseconds, bound.EndMilliseconds })
            .Append(_playheadMilliseconds)
            .Append(_durationMilliseconds);

    private long PixelsToSignedTime(double pixels)
    {
        var milliseconds = pixels * 1_000d / _scale.PixelsPerSecond;
        if (!double.IsFinite(milliseconds))
        {
            return milliseconds < 0 ? long.MinValue : long.MaxValue;
        }

        return (long)Math.Clamp(Math.Round(milliseconds, MidpointRounding.AwayFromZero), long.MinValue, long.MaxValue);
    }

    private async Task LoadThumbnailSafelyAsync(
        ProjectAsset? asset,
        Grid content,
        long thumbnailGeneration,
        CancellationToken cancellationToken)
    {
        if (asset is null || string.IsNullOrWhiteSpace(_projectRootPath))
        {
            return;
        }

        try
        {
            ThumbnailRequest request;
            string relativePath;
            lock (asset)
            {
                if (asset.IsMissing || string.IsNullOrWhiteSpace(asset.ThumbnailCachePath))
                {
                    return;
                }

                request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
                relativePath = asset.ThumbnailCachePath;
            }

            if (!ThumbnailService.IsCachePathForRequest(request, relativePath))
            {
                return;
            }

            var path = ThumbnailService.ResolveProjectCachePath(_projectRootPath, relativePath);
            if (!await ThumbnailService.IsUsableCachedThumbnailAsync(path, cancellationToken) ||
                !IsCurrentThumbnail(asset, request, relativePath, thumbnailGeneration, cancellationToken))
            {
                return;
            }

            var cachedFile = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
            using var stream = await cachedFile.OpenReadAsync().AsTask(cancellationToken);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream).AsTask(cancellationToken);
            if (!IsCurrentThumbnail(asset, request, relativePath, thumbnailGeneration, cancellationToken))
            {
                return;
            }

            content.ColumnDefinitions[0].Width = new GridLength(42);
            var image = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            Grid.SetColumn(image, 0);
            content.Children.Insert(0, image);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
        }
    }

    private CancellationToken RestartThumbnailWork()
    {
        _thumbnailRenderCts?.Cancel();
        _thumbnailRenderCts?.Dispose();
        _thumbnailRenderCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        return _thumbnailRenderCts.Token;
    }

    private bool IsCurrentThumbnail(
        ProjectAsset asset,
        ThumbnailRequest request,
        string relativePath,
        long generation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != _thumbnailRenderGeneration)
        {
            return false;
        }

        lock (asset)
        {
            return ThumbnailService.IsCurrentRequest(asset, request) &&
                   string.Equals(asset.ThumbnailCachePath, relativePath, StringComparison.Ordinal);
        }
    }

    private static bool IsControlDown() =>
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

    private static string FormatDuration(long milliseconds) => $"{Math.Max(0, milliseconds) / 1_000d:0.0}s";

    private static double ResourceDouble(string key) => (double)Application.Current.Resources[key];

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private sealed record ClipTag(
        TimelineItemBounds Bounds,
        ProjectAsset? Asset,
        long SourceInMilliseconds,
        long SourceOutMilliseconds,
        bool IsImage);

}

internal enum TimelineDragOperation
{
    None,
    Seek,
    Playhead,
    VideoReorder,
    VideoTrimStart,
    VideoTrimEnd,
    AudioMove,
    AudioTrimStart,
    AudioTrimEnd,
    TextMove,
    TextTrimStart,
    TextTrimEnd
}

internal static class TimelineDragPreview
{
    public static long CalculateAudioTrimEndSource(
        long sourceOutMilliseconds,
        long boundaryMilliseconds,
        long originalBoundaryMilliseconds)
    {
        var adjustedSourceOut = (decimal)sourceOutMilliseconds + boundaryMilliseconds - originalBoundaryMilliseconds;
        return adjustedSourceOut > long.MaxValue
            ? long.MaxValue
            : adjustedSourceOut < long.MinValue
                ? long.MinValue
                : (long)adjustedSourceOut;
    }

    public static long InitialValue(
        TimelineDragOperation operation,
        TimelineItemBounds bounds,
        long sourceInMilliseconds,
        long sourceOutMilliseconds,
        bool isImage) => operation switch
        {
            TimelineDragOperation.VideoTrimStart or TimelineDragOperation.VideoTrimEnd when isImage => bounds.DurationMilliseconds,
            TimelineDragOperation.VideoTrimStart or TimelineDragOperation.AudioTrimStart => sourceInMilliseconds,
            TimelineDragOperation.VideoTrimEnd or TimelineDragOperation.AudioTrimEnd => sourceOutMilliseconds,
            TimelineDragOperation.AudioMove or TimelineDragOperation.TextMove or TimelineDragOperation.TextTrimStart => bounds.StartMilliseconds,
            TimelineDragOperation.TextTrimEnd => bounds.EndMilliseconds,
            _ => 0
        };

    public static TimelineVideoTrimPreview CalculateVideoTrimStart(
        TimelineItemBounds bounds,
        long sourceInMilliseconds,
        long deltaMilliseconds,
        bool snappingEnabled,
        IEnumerable<long>? relevantEdges)
    {
        var desiredBoundary = Math.Max(0, TimelineMath.SaturatingAdd(bounds.StartMilliseconds, deltaMilliseconds));
        var snappedBoundary = TimelineSnapper.Snap(desiredBoundary, snappingEnabled, relevantEdges);
        var maximumBoundary = Math.Max(0, bounds.EndMilliseconds - ProjectDocument.MinimumItemDurationMilliseconds);
        var boundary = Math.Min(maximumBoundary, snappedBoundary);
        var acceptedDelta = boundary - bounds.StartMilliseconds;
        return new TimelineVideoTrimPreview(
            boundary,
            bounds.EndMilliseconds - boundary,
            TimelineMath.SaturatingAdd(sourceInMilliseconds, acceptedDelta));
    }

    public static TimelineVideoTrimPreview CalculateVideoTrimEnd(
        TimelineItemBounds bounds,
        long sourceOutMilliseconds,
        long deltaMilliseconds,
        bool snappingEnabled,
        IEnumerable<long>? relevantEdges)
    {
        var minimumBoundary = TimelineMath.SaturatingAdd(bounds.StartMilliseconds, ProjectDocument.MinimumItemDurationMilliseconds);
        var desiredBoundary = Math.Max(minimumBoundary, TimelineMath.SaturatingAdd(bounds.EndMilliseconds, deltaMilliseconds));
        var boundary = Math.Max(minimumBoundary, TimelineSnapper.Snap(desiredBoundary, snappingEnabled, relevantEdges));
        var duration = boundary - bounds.StartMilliseconds;
        return new TimelineVideoTrimPreview(
            boundary,
            duration,
            TimelineMath.SaturatingAdd(sourceOutMilliseconds, duration - bounds.DurationMilliseconds));
    }
}

internal static class TimelineOverlapSelection
{
    public static Guid GetPreviousAudioItemId(
        IReadOnlyList<AudioTimelineItem> items,
        long positionMilliseconds,
        Guid hitItemId)
    {
        ArgumentNullException.ThrowIfNull(items);
        var overlappingIds = items
            .Where(item =>
                item.StartMilliseconds <= positionMilliseconds &&
                positionMilliseconds < TimelineMath.End(item.StartMilliseconds, item.DurationMilliseconds))
            .Select(item => item.Id)
            .ToList();
        var hitIndex = overlappingIds.IndexOf(hitItemId);
        if (hitIndex < 0 || overlappingIds.Count < 2)
        {
            return hitItemId;
        }

        return overlappingIds[(hitIndex - 1 + overlappingIds.Count) % overlappingIds.Count];
    }
}

internal readonly record struct TimelineVideoTrimPreview(
    long BoundaryMilliseconds,
    long DurationMilliseconds,
    long SourceMilliseconds);

internal static class TimelineContextCommands
{
    public static bool SupportsDuplicate(EditorSelectionKind kind) =>
        kind is EditorSelectionKind.VideoItem or EditorSelectionKind.AudioItem or EditorSelectionKind.TextItem;

    public static TimelineContextCommandState Resolve(
        ProjectDocument? project,
        Guid itemId,
        TimelineTrackLocks trackLocks,
        long playheadMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(trackLocks);
        if (project is null)
        {
            return default;
        }

        var videoBounds = TimelineLayoutProjection.GetVideoBounds(project)
            .FirstOrDefault(bound => bound.ItemId == itemId);
        if (videoBounds.ItemId != Guid.Empty)
        {
            var item = project.VideoItems.First(candidate => candidate.Id == itemId);
            var asset = project.Assets.FirstOrDefault(candidate => candidate.Id == item.AssetId);
            var canEdit = trackLocks.CanEdit(EditorSelectionKind.VideoItem);
            var canSplit = canEdit &&
                playheadMilliseconds - videoBounds.StartMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds &&
                videoBounds.EndMilliseconds - playheadMilliseconds >= ProjectDocument.MinimumItemDurationMilliseconds;
            return new TimelineContextCommandState(
                canSplit,
                canEdit,
                canEdit,
                asset is not null && !string.IsNullOrWhiteSpace(asset.SourcePath));
        }

        var audio = project.AudioItems.FirstOrDefault(candidate => candidate.Id == itemId);
        if (audio is not null)
        {
            var asset = project.Assets.FirstOrDefault(candidate => candidate.Id == audio.AssetId);
            var canEdit = trackLocks.CanEdit(EditorSelectionKind.AudioItem);
            return new TimelineContextCommandState(
                CanSplit: false,
                CanDuplicate: canEdit,
                CanDelete: canEdit,
                CanShowSourceFile: asset is not null && !string.IsNullOrWhiteSpace(asset.SourcePath));
        }

        if (project.TextItems.Any(candidate => candidate.Id == itemId))
        {
            var canEdit = trackLocks.CanEdit(EditorSelectionKind.TextItem);
            return new TimelineContextCommandState(
                CanSplit: false,
                CanDuplicate: canEdit,
                CanDelete: canEdit,
                CanShowSourceFile: false);
        }

        return default;
    }
}

internal readonly record struct TimelineContextCommandState(
    bool CanSplit,
    bool CanDuplicate,
    bool CanDelete,
    bool CanShowSourceFile);

public enum TimelineTrackState
{
    None,
    VideoVisibility,
    TextVisibility,
    AudioMute
}

public sealed class TimelineTrackStateChangedEventArgs(TimelineTrackState state, bool value) : EventArgs
{
    public TimelineTrackState State { get; } = state;
    public bool Value { get; } = value;
}

public enum TimelineEditKind
{
    ReorderVideo,
    SplitVideo,
    TrimVideoStart,
    TrimVideoEnd,
    SetImageDuration,
    MoveAudio,
    TrimAudioStart,
    TrimAudioEnd,
    MoveText,
    TrimTextStart,
    TrimTextEnd,
    Delete,
    Duplicate,
    Undo,
    Redo,
    ShowSourceFile
}

public sealed class TimelineEditRequestedEventArgs(
    TimelineEditKind kind,
    Guid? itemId = null,
    long longValue = 0,
    int intValue = 0) : EventArgs
{
    public TimelineEditKind Kind { get; } = kind;
    public Guid? ItemId { get; } = itemId;
    public long LongValue { get; } = longValue;
    public int IntValue { get; } = intValue;
}

public sealed class TimelineAssetDroppedEventArgs(
    Guid assetId,
    TimelineTrackKind track,
    long positionMilliseconds) : EventArgs
{
    public Guid AssetId { get; } = assetId;
    public TimelineTrackKind Track { get; } = track;
    public long PositionMilliseconds { get; } = positionMilliseconds;
}

public sealed class TimelineMediaFilesDroppedEventArgs(
    IReadOnlyList<StorageFile> files,
    TimelineTrackKind track,
    long positionMilliseconds,
    IReadOnlyList<string> rejectedItems) : EventArgs
{
    public IReadOnlyList<StorageFile> Files { get; } = files;
    public TimelineTrackKind Track { get; } = track;
    public long PositionMilliseconds { get; } = positionMilliseconds;
    public IReadOnlyList<string> RejectedItems { get; } = rejectedItems;
}

public sealed class TimelineDropRejectedEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
