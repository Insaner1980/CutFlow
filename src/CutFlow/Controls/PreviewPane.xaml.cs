using CutFlow.Models;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Playback;

namespace CutFlow.Controls;

public sealed partial class PreviewPane : UserControl, IDisposable
{
    private const string ZeroTimecode = "00:00:00:00";

    private readonly MediaPlayer _player = new();
    private readonly DispatcherQueueTimer _positionTimer;
    private readonly EventGenerationGate _playerEventGeneration = new();
    private readonly LiveTextRenderGate _liveTextRenderGate = new();
    private readonly PreviewPlaybackStateCoordinator _playbackState = new();
    private MediaSource? _source;
    private MediaComposition? _composition;
    private ProjectDocument? _textProject;
    private EditorSelection _textSelection = EditorSelection.None;
    private long _textPositionMilliseconds;
    private long _textRevision;
    private FrameworkElement? _dragTextElement;
    private Guid _dragTextId;
    private Pointer? _dragPointer;
    private uint _dragPointerId;
    private string _currentTimecode = ZeroTimecode;
    private string _totalTimecode = ZeroTimecode;
    private string? _announcedCurrentTimecode;
    private string? _announcedTotalTimecode;
    private bool _canPlay;
    private bool _disposed;
    private TypedEventHandler<MediaPlaybackSession, object>? _playbackStateChangedHandler;
    private TypedEventHandler<MediaPlayer, object>? _mediaEndedHandler;
    private TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? _mediaFailedHandler;

    public PreviewPane()
    {
        InitializeComponent();
        _positionTimer = DispatcherQueue.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(33);
        _positionTimer.IsRepeating = true;
        PlayerElement.SetMediaPlayer(_player);
        _positionTimer.Tick += PositionTimer_Tick;
        AttachPlayerEvents();
    }

    public event EventHandler<PlaybackChangedEventArgs>? PlayPauseRequested;
    public event EventHandler<PlayheadChangedEventArgs>? SeekRequested;
    public event EventHandler<PlayheadChangedEventArgs>? PlaybackPositionChanged;
    public event EventHandler<PreviewPlaybackFailedEventArgs>? PlaybackFailed;
    public event EventHandler<TextPositionCommittedEventArgs>? TextPositionCommitted;
    public event EventHandler? ImportRequested;
    public event EventHandler? LoopChanged;

    public FrameworkElement RenderHost => RenderHostGrid;
    public long PositionMilliseconds => (long)Math.Max(0, _player.PlaybackSession.Position.TotalMilliseconds);
    public bool IsPlaying => _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
    public bool PlayIntent => _playbackState.PlayIntent;
    public bool IsLooping => LoopButton.IsChecked == true;

    public void SetPlayhead(string timecode)
    {
        _currentTimecode = string.IsNullOrWhiteSpace(timecode) ? ZeroTimecode : timecode;
        UpdateTimecodeText();
    }

    public void SetAspectRatio(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        PreviewSurface.Width = width;
        PreviewSurface.Height = height;
        ApplyFit();
    }

    public void SetBackgroundColor(string opaqueArgb)
    {
        if (!TimelineInput.IsOpaqueArgb(opaqueArgb)) opaqueArgb = ProjectSettings.DefaultBackgroundColor;
        PreviewSurface.Background = TextStyle.ParseBrush(opaqueArgb, ProjectSettings.DefaultBackgroundColor);
    }

    public void ReplaceComposition(
        MediaComposition? composition,
        long preservedPositionMilliseconds,
        bool resumePlayback,
        bool hasVisualContent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var (hasContent, preparedSource) = PrepareSource(composition);

        var sourceTransferred = false;
        try
        {
            var oldSource = _source;
            var swapPositionMilliseconds = oldSource is not null
                ? PositionMilliseconds
                : preservedPositionMilliseconds;
            RunSuccessfulSourceSwap([
                () => _playerEventGeneration.Advance(),
                DetachPlayerEvents,
                () => _player.Pause(),
                () =>
                {
                    _player.Source = null;
                    _source = null;
                },
                () => oldSource?.Dispose(),
                () => ApplyPreparedComposition(composition, preparedSource, hasContent, hasVisualContent),
                () => RestorePlaybackPosition(composition, hasContent, swapPositionMilliseconds, resumePlayback),
                AttachPlayerEvents,
                ResumePlaybackIfRequested
            ]);
            sourceTransferred = preparedSource is not null && ReferenceEquals(_source, preparedSource);
        }
        catch
        {
            RunFailedSourceSwapCleanup(
                () => _playerEventGeneration.Advance(),
                DetachPlayerEvents,
                () => _player.Pause(),
                ClearFailedComposition,
                () =>
                {
                    preparedSource?.Dispose();
                    preparedSource = null;
                },
                AttachPlayerEvents);
            throw;
        }
        finally
        {
            if (!sourceTransferred) preparedSource?.Dispose();
        }
    }

    private (bool HasContent, MediaSource? Source) PrepareSource(MediaComposition? composition)
    {
        if (composition is null || composition.Clips.Count == 0 || composition.Duration <= TimeSpan.Zero)
        {
            return (false, null);
        }

        var previewSize = PreviewStreamSize.Fit((int)PreviewSurface.Width, (int)PreviewSurface.Height, 1280, 720);
        return (true, MediaSource.CreateFromMediaStreamSource(
            composition.GeneratePreviewMediaStreamSource(previewSize.Width, previewSize.Height)));
    }

    private void ApplyPreparedComposition(
        MediaComposition? composition,
        MediaSource? preparedSource,
        bool hasContent,
        bool hasVisualContent)
    {
        _composition = composition;
        SetDuration(hasContent && composition is not null ? (long)composition.Duration.TotalMilliseconds : 0);
        if (!hasContent || preparedSource is null)
        {
            SetCanPlay(false);
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        _player.Source = preparedSource;
        _source = preparedSource;
        SetCanPlay(true);
        EmptyState.Visibility = hasVisualContent ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RestorePlaybackPosition(
        MediaComposition? composition,
        bool hasContent,
        long swapPositionMilliseconds,
        bool resumePlayback)
    {
        if (hasContent && composition is not null)
        {
            _player.PlaybackSession.Position = TimeSpan.FromMilliseconds(
                PreviewPlaybackPolicy.GetRebuildStartPosition(
                    swapPositionMilliseconds,
                    (long)composition.Duration.TotalMilliseconds,
                    resumePlayback));
        }

        _playbackState.SetIntent(hasContent && resumePlayback);
        ApplyActualPlaybackState(isPlaying: false);
    }

    private void ResumePlaybackIfRequested()
    {
        if (_playbackState.PlayIntent)
        {
            _player.Play();
        }
    }

    private void ClearFailedComposition()
    {
        _player.Source = null;
        _source = null;
        _composition = null;
        SetDuration(0);
        _playbackState.SetIntent(false);
        ApplyActualPlaybackState(isPlaying: false);
        SetCanPlay(false);
        EmptyState.Visibility = Visibility.Visible;
    }

    internal static void RunSuccessfulSourceSwap(IReadOnlyList<Action> steps)
    {
        if (steps.Count != 9) throw new ArgumentException("A successful source swap requires exactly nine ordered steps.", nameof(steps));
        foreach (var step in steps) step();
    }

    internal static void RunFailedSourceSwapCleanup(
        Action invalidateGeneration,
        Action detachEvents,
        Action pause,
        Action clearSourceAndState,
        Action disposeSource,
        Action attachEvents)
    {
        invalidateGeneration();
        detachEvents();
        try
        {
            pause();
        }
        finally
        {
            try
            {
                clearSourceAndState();
            }
            finally
            {
                try
                {
                    disposeSource();
                }
                finally
                {
                    attachEvents();
                }
            }
        }
    }

    public void Seek(long positionMilliseconds)
    {
        if (!_canPlay || _composition is null) return;
        _player.PlaybackSession.Position = TimeSpan.FromMilliseconds(
            TimelinePlaybackMath.ClampPosition(positionMilliseconds, (long)_composition.Duration.TotalMilliseconds));
    }

    public void PlayPause()
    {
        if (!_canPlay) return;
        if (!_playbackState.ToggleIntent())
        {
            _player.Pause();
        }
        else
        {
            if (_composition is not null &&
                PreviewPlaybackPolicy.ShouldRestartForPlay(
                    _player.PlaybackSession.Position,
                    _composition.Duration))
            {
                _player.PlaybackSession.Position = TimeSpan.Zero;
            }

            _player.Play();
        }
    }

    public void Mute(bool isMuted)
    {
        _player.IsMuted = isMuted;
        MuteButton.IsChecked = isMuted;
        var name = isMuted ? "Unmute preview" : "Mute preview";
        AutomationProperties.SetName(MuteButton, name);
        ToolTipService.SetToolTip(MuteButton, name);
    }

    public void Loop(bool loop)
    {
        _player.IsLoopingEnabled = false;
        var changed = LoopButton.IsChecked != loop;
        LoopButton.IsChecked = loop;
        UpdateLoopButtonPresentation(loop);
        if (changed) LoopChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLoopButtonPresentation(bool loop)
    {
        var name = loop ? "Disable preview looping" : "Loop preview";
        AutomationProperties.SetName(LoopButton, name);
        ToolTipService.SetToolTip(LoopButton, name);
    }

    public void SetCanPlay(bool canPlay)
    {
        var wasCanPlay = _canPlay;
        _canPlay = canPlay;
        if (!canPlay) _playbackState.SetIntent(false);
        PlayButton.IsEnabled = canPlay;
        PreviousFrameButton.IsEnabled = canPlay;
        NextFrameButton.IsEnabled = canPlay;
        MuteButton.IsEnabled = canPlay;
        LoopButton.IsEnabled = canPlay;
        if (!canPlay || !wasCanPlay) SetPlaying(false);
    }

    public void SetPlaying(bool isPlaying)
    {
        var playing = _canPlay && isPlaying;
        var playAction = playing ? "Pause" : "Play";
        PlayIcon.Glyph = playing ? "\uE769" : "\uE768";
        PlayToolTip.Content = _canPlay ? playAction : "Playback is unavailable until the preview has media";
        AutomationProperties.SetName(PlayButton, _canPlay ? $"{playAction} preview" : "Play preview unavailable");
        UpdateTimecodeText(playing);
    }

    public void RequestSeek(long positionMilliseconds) =>
        SeekRequested?.Invoke(this, new PlayheadChangedEventArgs(positionMilliseconds));

    public void SetTextItems(ProjectDocument project, EditorSelection selection, long positionMilliseconds, long revision)
    {
        if (_dragTextElement is not null &&
            (!ReferenceEquals(_textProject, project) || _textSelection != selection))
        {
            CancelTextDrag(render: false);
        }

        _textProject = project ?? throw new ArgumentNullException(nameof(project));
        _textSelection = selection;
        _textPositionMilliseconds = Math.Max(0, positionMilliseconds);
        _textRevision = revision;
        RenderLiveText();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelTextDrag(render: false);
        _positionTimer.Stop();
        _positionTimer.Tick -= PositionTimer_Tick;
        _playerEventGeneration.Advance();
        DetachPlayerEvents();
        try
        {
            PreviewResourceCleanup.Run(
                () => _player.Pause(),
                () => _player.Source = null,
                () => PlayerElement.SetMediaPlayer(null),
                () => _source?.Dispose(),
                () => _player.Dispose());
        }
        finally
        {
            _source = null;
            _composition = null;
            _textProject = null;
            _dragTextElement = null;
            _dragPointer = null;
            _dragPointerId = 0;
            TextOverlayCanvas.Children.Clear();
        }
    }

    private void AttachPlayerEvents()
    {
        var generation = _playerEventGeneration.Current;
        _playbackStateChangedHandler = (sender, args) => PlaybackSession_PlaybackStateChanged(sender, args, generation);
        _mediaEndedHandler = (sender, args) => Player_MediaEnded(sender, args, generation);
        _mediaFailedHandler = (sender, args) => Player_MediaFailed(sender, args, generation);
        _player.PlaybackSession.PlaybackStateChanged += _playbackStateChangedHandler;
        _player.MediaEnded += _mediaEndedHandler;
        _player.MediaFailed += _mediaFailedHandler;
    }

    private void DetachPlayerEvents()
    {
        if (_playbackStateChangedHandler is not null)
        {
            _player.PlaybackSession.PlaybackStateChanged -= _playbackStateChangedHandler;
            _playbackStateChangedHandler = null;
        }

        if (_mediaEndedHandler is not null)
        {
            _player.MediaEnded -= _mediaEndedHandler;
            _mediaEndedHandler = null;
        }

        if (_mediaFailedHandler is not null)
        {
            _player.MediaFailed -= _mediaFailedHandler;
            _mediaFailedHandler = null;
        }
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object eventArgs, long generation)
    {
        _ = eventArgs;
        if (_disposed || !_playerEventGeneration.IsCurrent(generation)) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            _playerEventGeneration.TryRun(generation, () =>
            {
                var state = sender.PlaybackState;
                ApplyActualPlaybackState(state == MediaPlaybackState.Playing);
            });
        });
    }

    private void Player_MediaEnded(MediaPlayer sender, object eventArgs, long generation)
    {
        _ = eventArgs;
        if (_disposed || !_playerEventGeneration.IsCurrent(generation)) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            _playerEventGeneration.TryRun(generation, () =>
            {
                if (PreviewPlaybackPolicy.ShouldRestartAtEnd(LoopButton.IsChecked == true) &&
                    _playbackState.PlayIntent)
                {
                    sender.PlaybackSession.Position = TimeSpan.Zero;
                    PlaybackPositionChanged?.Invoke(this, new PlayheadChangedEventArgs(0));
                    sender.Play();
                    return;
                }

                _positionTimer.Stop();
                _playbackState.SetIntent(false);
                sender.Pause();
                PlaybackPositionChanged?.Invoke(this, new PlayheadChangedEventArgs(PositionMilliseconds));
                ApplyActualPlaybackState(isPlaying: false);
            });
        });
    }

    private void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs eventArgs, long generation)
    {
        if (_disposed || !_playerEventGeneration.IsCurrent(generation)) return;
        var error = eventArgs.Error;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            _playerEventGeneration.TryRun(generation, () =>
            {
                _positionTimer.Stop();
                _playbackState.SetIntent(false);
                sender.Pause();
                ApplyActualPlaybackState(isPlaying: false);
                PlaybackFailed?.Invoke(this, new PreviewPlaybackFailedEventArgs(error));
            });
        });
    }

    private void ApplyActualPlaybackState(bool isPlaying)
    {
        _playbackState.ReportActual(
            isPlaying,
            playing =>
            {
                if (playing) _positionTimer.Start(); else _positionTimer.Stop();
                SetPlaying(playing);
                PlayPauseRequested?.Invoke(this, new PlaybackChangedEventArgs(playing));
            });
    }

    private void PositionTimer_Tick(object? sender, object e)
    {
        if (_disposed || !IsPlaying) return;
        var position = PositionMilliseconds;
        PlaybackPositionChanged?.Invoke(this, new PlayheadChangedEventArgs(position));
    }

    private void StepByFrame(bool forward)
    {
        if (!_canPlay || _composition is null) return;
        if (_playbackState.PlayIntent || IsPlaying)
        {
            _playbackState.SetIntent(false);
            _player.Pause();
            ApplyActualPlaybackState(isPlaying: false);
        }

        var position = PreviewTransportMath.StepByFrame(
            PositionMilliseconds,
            (long)_composition.Duration.TotalMilliseconds,
            forward);
        SetPlayhead(TimecodeFormatter.Format(position, PreviewTransportMath.FrameRate));
        RequestSeek(position);
    }

    private void SetDuration(long durationMilliseconds)
    {
        _totalTimecode = TimecodeFormatter.Format(
            Math.Max(0, durationMilliseconds),
            PreviewTransportMath.FrameRate);
        UpdateTimecodeText();
    }

    private void UpdateTimecodeText(bool? isPlaying = null)
    {
        TimecodeText.Text = $"{_currentTimecode} / {_totalTimecode}";
        if (!PreviewTransportMath.ShouldUpdateTimecodeAutomationName(
                _currentTimecode,
                _totalTimecode,
                _announcedCurrentTimecode,
                _announcedTotalTimecode,
                isPlaying ?? IsPlaying))
        {
            return;
        }

        AutomationProperties.SetName(
            TimecodeText,
            $"Current time {_currentTimecode}, total duration {_totalTimecode}");
        _announcedCurrentTimecode = _currentTimecode;
        _announcedTotalTimecode = _totalTimecode;
    }

    private void ApplyFit()
    {
        PreviewViewbox.Stretch = Stretch.Uniform;
        var availableWidth = Math.Min(PreviewWorkspace.ActualWidth, PreviewFrame.MaxWidth);
        var availableHeight = Math.Min(PreviewWorkspace.ActualHeight, PreviewFrame.MaxHeight);
        var size = PreviewTransportMath.CalculateFitSize(
            availableWidth,
            availableHeight,
            PreviewSurface.Width,
            PreviewSurface.Height);
        if (size.Width <= 0 || size.Height <= 0) return;
        PreviewFrame.Width = size.Width;
        PreviewFrame.Height = size.Height;
    }

    private void RenderLiveText()
    {
        if (_textProject is null) return;
        if (!_liveTextRenderGate.ShouldBuildKey(
                _textProject,
                _textRevision,
                _textSelection,
                _textPositionMilliseconds,
                PreviewSurface.Width,
                PreviewSurface.Height,
                _dragTextElement is not null)) return;
        var renderKey = LiveTextRenderKey.Create(
            _textProject,
            _textSelection,
            _textPositionMilliseconds,
            PreviewSurface.Width,
            PreviewSurface.Height,
            _textRevision);
        if (!_liveTextRenderGate.ShouldRender(renderKey, hasPointerCapture: false)) return;

        TextOverlayCanvas.Children.Clear();
        if (!_textProject.Settings.TextTrackVisible) return;
        foreach (var item in _textProject.TextItems.Where(item =>
                     TimelineMath.IsActiveAt(_textPositionMilliseconds, item.StartMilliseconds, item.DurationMilliseconds)))
        {
            var selected = _textSelection is { Kind: EditorSelectionKind.TextItem, ItemId: Guid id } && id == item.Id;
            var text = new TextBlock();
            TextStyle.ApplyTextBlockStyle(text, item, PreviewSurface.Width * 0.9);
            var content = new Border
            {
                Child = text
            };
            TextStyle.ApplyContainerStyle(content, item);
            var visual = new Grid
            {
                Tag = item.Id,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            visual.Children.Add(content);
            if (selected)
            {
                visual.Children.Add(new Rectangle
                {
                    Stroke = (Brush)Application.Current.Resources["AccentBrush"],
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                });
                visual.PointerPressed += Text_PointerPressed;
                visual.PointerMoved += Text_PointerMoved;
                visual.PointerReleased += Text_PointerReleased;
                visual.PointerCanceled += Text_PointerCanceled;
                visual.PointerCaptureLost += Text_PointerCaptureLost;
            }
            visual.Measure(new Windows.Foundation.Size(PreviewSurface.Width * 0.9, PreviewSurface.Height));
            PositionText(visual, item.NormalizedX, item.NormalizedY);
            TextOverlayCanvas.Children.Add(visual);
        }
    }

    private void PositionText(FrameworkElement element, double normalizedX, double normalizedY)
    {
        Canvas.SetLeft(element, TextStyle.ClampNormalized(normalizedX) * PreviewSurface.Width - element.DesiredSize.Width / 2);
        Canvas.SetTop(element, TextStyle.ClampNormalized(normalizedY) * PreviewSurface.Height - element.DesiredSize.Height / 2);
    }

    private void Text_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_disposed ||
            _dragTextElement is not null ||
            sender is not Grid { Tag: Guid itemId } visual ||
            !e.GetCurrentPoint(visual).Properties.IsLeftButtonPressed ||
            !visual.CapturePointer(e.Pointer)) return;
        _dragTextElement = visual;
        _dragTextId = itemId;
        _dragPointer = e.Pointer;
        _dragPointerId = e.Pointer.PointerId;
        e.Handled = true;
    }

    private void Text_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTextElement is null || e.Pointer.PointerId != _dragPointerId) return;
        var point = e.GetCurrentPoint(TextOverlayCanvas).Position;
        Canvas.SetLeft(_dragTextElement, point.X - _dragTextElement.ActualWidth / 2);
        Canvas.SetTop(_dragTextElement, point.Y - _dragTextElement.ActualHeight / 2);
        e.Handled = true;
    }

    private void Text_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTextElement is null || e.Pointer.PointerId != _dragPointerId) return;
        var element = _dragTextElement;
        var itemId = _dragTextId;
        var x = TextStyle.ClampNormalized((Canvas.GetLeft(element) + element.ActualWidth / 2) / PreviewSurface.Width);
        var y = TextStyle.ClampNormalized((Canvas.GetTop(element) + element.ActualHeight / 2) / PreviewSurface.Height);
        ClearTextDrag(element, e.Pointer);
        _liveTextRenderGate.MarkVisualDirty();
        TextPositionCommitted?.Invoke(this, new TextPositionCommittedEventArgs(itemId, x, y));
        RenderLiveText();
        e.Handled = true;
    }

    private void Text_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTextElement is not { } element || e.Pointer.PointerId != _dragPointerId) return;
        ClearTextDrag(element, e.Pointer);
        _liveTextRenderGate.MarkVisualDirty();
        RenderLiveText();
        e.Handled = true;
    }

    private void Text_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTextElement is null || e.Pointer.PointerId != _dragPointerId) return;
        _dragTextElement = null;
        _dragTextId = Guid.Empty;
        _dragPointer = null;
        _dragPointerId = 0;
        _liveTextRenderGate.MarkVisualDirty();
        RenderLiveText();
        e.Handled = true;
    }

    private void ClearTextDrag(FrameworkElement element, Pointer pointer)
    {
        _dragTextElement = null;
        _dragTextId = Guid.Empty;
        _dragPointer = null;
        _dragPointerId = 0;
        element.ReleasePointerCapture(pointer);
    }

    private void CancelTextDrag(bool render)
    {
        var element = _dragTextElement;
        var pointer = _dragPointer;
        _dragTextElement = null;
        _dragTextId = Guid.Empty;
        _dragPointer = null;
        _dragPointerId = 0;
        if (element is not null && pointer is not null)
        {
            element.ReleasePointerCapture(pointer);
        }

        _liveTextRenderGate.MarkVisualDirty();
        if (render)
        {
            RenderLiveText();
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_disposed) ImportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PreviousFrameButton_Click(object sender, RoutedEventArgs e) => StepByFrame(forward: false);
    private void PlayButton_Click(object sender, RoutedEventArgs e) => PlayPause();
    private void NextFrameButton_Click(object sender, RoutedEventArgs e) => StepByFrame(forward: true);
    private void MuteButton_Click(object sender, RoutedEventArgs e) => Mute(MuteButton.IsChecked == true);
    private void LoopButton_Click(object sender, RoutedEventArgs e)
    {
        _player.IsLoopingEnabled = false;
        UpdateLoopButtonPresentation(LoopButton.IsChecked == true);
        LoopChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FitButton_Click(object sender, RoutedEventArgs e) => ApplyFit();
    private void PreviewWorkspace_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyFit();

}

public sealed class TextPositionCommittedEventArgs(Guid itemId, double normalizedX, double normalizedY) : EventArgs
{
    public Guid ItemId { get; } = itemId;
    public double NormalizedX { get; } = normalizedX;
    public double NormalizedY { get; } = normalizedY;
}

public sealed class PreviewPlaybackFailedEventArgs(MediaPlayerError error) : EventArgs
{
    public MediaPlayerError Error { get; } = error;
}
