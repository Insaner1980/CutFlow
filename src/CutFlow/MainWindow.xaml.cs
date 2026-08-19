using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using CutFlow.Controls;
using CutFlow.Services;
using CutFlow.ViewModels;
using CutFlow.Views;
using CutFlow.Utilities;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace CutFlow;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HomeView _homeView;
    private readonly ProjectService _projectService;
    private readonly MediaImportService _mediaImportService;
    private readonly SettingsService _settingsService;
    private readonly DebouncedSaveCoordinator _settingsSaveCoordinator;
    private readonly SimpleLogService _logService;
    private readonly WorkspaceSettingsSaveFailureNotice _workspaceSettingsSaveFailureNotice = new();
    private readonly TaskCompletionSource<AppWindowContext> _windowReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MainWindowLifecycle _lifecycle = new();
    private readonly Task _initializationTask;
    private EditorView? _editorView;
    private AppWindow? _appWindow;
    private WindowGeometry? _restoredWindowBounds;
    private AppSettings _appSettings = AppSettings.Normalize(null);
    private EditorView? _workspaceSettingsSaveEditor;
    private InfoBarPublication _workspaceSettingsSavePublication;

    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.ProductName;
        ExtendsContentIntoTitleBar = true;
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        _projectService = new ProjectService();
        _mediaImportService = new MediaImportService();
        _settingsService = new SettingsService();
        _logService = new SimpleLogService();
        _settingsSaveCoordinator = new DebouncedSaveCoordinator(() => _settingsService.SaveAsync(_appSettings));
        _settingsSaveCoordinator.StateChanged += SettingsSaveCoordinator_StateChanged;
        _viewModel = new MainViewModel(_projectService, _mediaImportService, () => _lifecycle.CanContinueInitialization);
        _viewModel.CurrentViewChanged += (_, _) => ShowCurrentView();
        _homeView = new HomeView(_viewModel.Home, () => _lifecycle.CanContinueInitialization);
        _homeView.ProjectOpenRequested = project => _viewModel.OpenEditorAsync(project);
        _initializationTask = _lifecycle.StartInitialization(InitializeAsync);
    }

    private void ShowCurrentView()
    {
        if (!_lifecycle.CanContinueInitialization)
        {
            return;
        }

        if (_viewModel.Editor is { } editorViewModel)
        {
            if (_editorView is not null &&
                ReferenceEquals(_editorView.ViewModel, editorViewModel) &&
                ReferenceEquals(ContentHost.Content, _editorView))
            {
                SetTitleBar(_editorView.TitleBarElement);
                return;
            }

            if (_editorView is not null)
            {
                DetachEditorView();
            }

            var editorView = new EditorView(editorViewModel, _projectService, _mediaImportService, _logService, _appSettings);
            _editorView = editorView;
            _editorView.ReturnHomeRequested += Editor_ReturnHomeRequested;
            _editorView.NewProjectRequested += Editor_NewProjectRequested;
            _editorView.ImportRequested += Editor_ImportRequested;
            _editorView.RelinkAssetRequested += Editor_RelinkAssetRequested;
            _editorView.ExportRequested += Editor_ExportRequested;
            _editorView.WorkspaceSettingsChanged += Editor_WorkspaceSettingsChanged;
            ContentHost.Content = _editorView;
            SetTitleBar(_editorView.TitleBarElement);
            QueueInitialFocus(_editorView, _editorView.FocusInitialControl);
            return;
        }

        if (_editorView is null && ReferenceEquals(ContentHost.Content, _homeView))
        {
            SetTitleBar(_homeView.TitleBarElement);
            return;
        }

        DetachEditorView();
        ContentHost.Content = _homeView;
        SetTitleBar(_homeView.TitleBarElement);
        QueueInitialFocus(_homeView, _homeView.FocusInitialControl);
    }

    private void QueueInitialFocus(UIElement content, Func<bool> focus)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_lifecycle.CanContinueInitialization && ReferenceEquals(ContentHost.Content, content))
            {
                focus();
            }
        });
    }

    private void DetachEditorView()
    {
        if (_editorView is not { } editor)
        {
            return;
        }

        _editorView = null;
        editor.ReturnHomeRequested -= Editor_ReturnHomeRequested;
        editor.NewProjectRequested -= Editor_NewProjectRequested;
        editor.ImportRequested -= Editor_ImportRequested;
        editor.RelinkAssetRequested -= Editor_RelinkAssetRequested;
        editor.ExportRequested -= Editor_ExportRequested;
        editor.WorkspaceSettingsChanged -= Editor_WorkspaceSettingsChanged;
        SetTitleBar(null);
        if (ReferenceEquals(ContentHost.Content, editor))
        {
            ContentHost.Content = null;
        }

        editor.Dispose();
    }

    private async Task InitializeAsync()
    {
        AppSettings loadedSettings = AppSettings.Normalize(null);
        string? settingsError = null;
        try
        {
            var context = await _windowReady.Task;
            try
            {
                loadedSettings = await _settingsService.LoadAsync();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)
            {
                settingsError = "Could not restore workspace settings. Default workspace settings were used.";
                _ = _logService.TryWriteAsync($"Workspace settings restore failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
            }

            _appSettings = loadedSettings;
            if (!_lifecycle.CanContinueInitialization)
            {
                return;
            }

            try
            {
                RestoreWindowGeometry(context);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or COMException or OverflowException)
            {
                settingsError = "Could not restore the previous window position. A safe default layout was used.";
                _ = _logService.TryWriteAsync($"Window geometry restore failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
            }

            if (!_lifecycle.CanContinueInitialization)
            {
                return;
            }

            if (settingsError is not null)
            {
                _viewModel.Home.ReportError(settingsError);
            }

            await _viewModel.ShowHomeAsync();
            if (!_lifecycle.CanContinueInitialization)
            {
                return;
            }
        }
        catch (OperationCanceledException) when (!_lifecycle.CanContinueClosing)
        {
            // Window closing canceled the pending initialization.
        }
        catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))
        {
            _ = _logService.TryWriteAsync($"Startup failed: {exception.GetType().Name}");
            if (_lifecycle.CanContinueInitialization)
            {
                try
                {
                    _viewModel.Home.ReportError($"{AppInfo.ProductName} could not finish starting. Close and reopen the app. If the problem continues, check the local log.");
                    ShowCurrentView();
                }
                catch (Exception recoveryException) when (!ExceptionPolicy.IsFatal(recoveryException))
                {
                    _ = _logService.TryWriteAsync($"Startup recovery failed: {recoveryException.GetType().Name}");
                    _lifecycle.ApproveClose();
                    _lifecycle.TryCommitClose(Close);
                }
            }
        }
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_windowReady.Task.IsCompleted || !_lifecycle.CanContinueClosing)
        {
            return;
        }

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        if (IsRestored(_appWindow))
        {
            _restoredWindowBounds = GetCurrentBounds(_appWindow);
        }

        _appWindow.Closing += AppWindow_Closing;
        _appWindow.Changed += AppWindow_Changed;
        _windowReady.TrySetResult(new AppWindowContext(_appWindow));
    }

    private void RestoreWindowGeometry(AppWindowContext context)
    {
        var hasPhysicalBounds = WindowGeometry.TryGetStoredPhysicalBounds(_appSettings, out var requestedBounds);
        var displays = DisplayArea.FindAll();
        var displayGeometry = displays
            .Select(area => new WindowDisplayGeometry(
                new WindowGeometry(area.OuterBounds.X, area.OuterBounds.Y, area.OuterBounds.Width, area.OuterBounds.Height),
                GetDisplayDpi(area),
                area.IsPrimary,
                area.DisplayId.Value))
            .ToArray();
        var targetDisplayIndex = hasPhysicalBounds
            ? WindowGeometry.SelectTargetDisplay(requestedBounds, displayGeometry)
            : WindowGeometry.SelectLegacyTargetDisplay(_appSettings, displayGeometry);
        var displayArea = displays[targetDisplayIndex];
        var targetDpi = displayGeometry[targetDisplayIndex].Dpi;
        if (!hasPhysicalBounds)
        {
            requestedBounds = WindowGeometry.FromLegacyLogicalSettings(_appSettings, targetDpi);
        }

        var workArea = displayArea.WorkArea;
        var physicalWorkArea = new WindowGeometry(workArea.X, workArea.Y, workArea.Width, workArea.Height);
        var geometry = WindowGeometry.ClampPhysicalToWorkArea(requestedBounds, physicalWorkArea, targetDpi);
        ApplyPreferredMinimum(context.AppWindow, workArea, targetDpi);
        context.AppWindow.MoveAndResize(ToRect(geometry));
        _restoredWindowBounds = geometry;
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((!args.DidPositionChange && !args.DidSizeChange) || !_lifecycle.CanContinueInitialization)
        {
            return;
        }

        try
        {
            var bounds = GetCurrentBounds(sender);
            if (IsRestored(sender))
            {
                _restoredWindowBounds = bounds;
            }

            var displayArea = DisplayArea.GetFromRect(ToRect(bounds), DisplayAreaFallback.Nearest);
            ApplyPreferredMinimum(sender, displayArea.WorkArea, GetWindowDpi());
        }
        catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))
        {
            _ = _logService.TryWriteAsync($"Window minimum tracking update failed: {exception.GetType().Name}");
        }
    }

    private static void ApplyPreferredMinimum(AppWindow appWindow, RectInt32 workArea, uint targetDpi)
    {
        var effectiveMinimum = WindowGeometry.GetEffectiveMinimumSize(workArea.Width, workArea.Height, targetDpi);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = effectiveMinimum.Width;
            presenter.PreferredMinimumHeight = effectiveMinimum.Height;
        }
    }

    private static RectInt32 ToRect(WindowGeometry geometry) =>
        new(geometry.X, geometry.Y, geometry.Width, geometry.Height);

    private static uint GetDisplayDpi(DisplayArea displayArea)
    {
        return GetDisplayDpi(displayArea.OuterBounds);
    }

    private static uint GetDisplayDpi(RectInt32 bounds)
    {
        var nativeBounds = new NativeRect(bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
        var monitor = MonitorFromRect(ref nativeBounds, 2);
        if (monitor == 0)
        {
            throw new InvalidOperationException("The target display could not be resolved.");
        }

        return GetMonitorDpi(monitor);
    }

    private uint GetWindowDpi()
    {
        var monitor = MonitorFromWindow(GetWindowHandle(), 2);
        if (monitor == 0)
        {
            throw new InvalidOperationException("The window's display could not be resolved.");
        }

        return GetMonitorDpi(monitor);
    }

    private static uint GetMonitorDpi(nint monitor)
    {
        var result = GetScaleFactorForMonitor(monitor, out var scaleFactor);
        if (result != 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        return WindowGeometry.ScaleFactorToDpi(scaleFactor);
    }

    private async void Editor_ReturnHomeRequested(object? sender, EventArgs e)
    {
        if (_lifecycle.CanHandleEditorEvent(_editorView, sender))
        {
            await _viewModel.ShowHomeAsync();
        }
    }

    private async void Editor_NewProjectRequested(object? sender, EventArgs e)
    {
        if (sender is not EditorView editor || !_lifecycle.CanHandleEditorEvent(_editorView, editor))
        {
            return;
        }

        await _homeView.RunProjectOpenGateAsync(async () =>
        {
            editor.IsEnabled = false;
            try
            {
                var project = await _viewModel.Home.CreateAsync();
                if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
                {
                    await _viewModel.ReplaceEditorAsync(project);
                }
            }
            catch (OperationCanceledException) when (!_lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                // The editor lifetime ended while the project was being created.
            }
            catch (Exception exception) when (HomeViewModel.IsExpectedProjectOperationFailure(exception))
            {
                if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
                {
                    editor.ReportError("Could not create project", "Check available disk space and try again.");
                    _ = _logService.TryWriteAsync($"Project creation failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
                }
            }
            finally
            {
                if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
                {
                    editor.IsEnabled = true;
                }
            }
        });
    }

    private async void Editor_ImportRequested(object? sender, MediaImportRequestedEventArgs e)
    {
        if (sender is not EditorView editor || !_lifecycle.CanHandleEditorEvent(_editorView, editor))
        {
            return;
        }

        try
        {
            var files = await FilePickerHelper.PickMediaFilesAsync(GetWindowHandle(), e.Scope);
            if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                await editor.ImportFilesAsync(files);
            }
        }
        catch (OperationCanceledException) when (!_lifecycle.CanHandleEditorEvent(_editorView, editor))
        {
            // The editor lifetime ended while the picker or import was active.
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                editor.ReportError("Could not open media", "The file picker or import could not be completed. Choose the media file again.");
                _ = _logService.TryWriteAsync($"Media picker or import failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
            }
        }
    }

    private async void Editor_RelinkAssetRequested(object? sender, Controls.AssetActionEventArgs e)
    {
        if (sender is not EditorView editor ||
            !_lifecycle.CanHandleEditorEvent(_editorView, editor) ||
            editor.FindAsset(e.AssetId) is not { } asset)
        {
            return;
        }

        try
        {
            var file = await FilePickerHelper.PickRelinkFileAsync(GetWindowHandle(), asset.Kind);
            if (file is not null && _lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                await editor.RelinkAssetAsync(e.AssetId, file);
            }
        }
        catch (OperationCanceledException) when (!_lifecycle.CanHandleEditorEvent(_editorView, editor))
        {
            // The editor lifetime ended while the picker or relink was active.
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                editor.ReportError($"Could not relink '{asset.FileName}'", "The replacement could not be opened. Choose the file again and confirm it is available.");
                _ = _logService.TryWriteAsync($"Media relink picker failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
            }
        }
    }

    private async void Editor_ExportRequested(object? sender, EventArgs e)
    {
        if (sender is not EditorView editor || !_lifecycle.CanHandleEditorEvent(_editorView, editor))
        {
            return;
        }

        try
        {
            await editor.ExportAsync(GetWindowHandle());
        }
        catch (Exception exception) when (EditorView.IsExpectedExportException(exception))
        {
            if (_lifecycle.CanHandleEditorEvent(_editorView, editor))
            {
                editor.ReportError("Export failed", $"{AppInfo.ProductName} could not start the export. Try again or choose another output location.");
                _ = _logService.TryWriteAsync($"Export start failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}");
            }
        }
    }

    private nint GetWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private async void MainWindowRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var controlDown = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var editableControlFocused = Content is FrameworkElement root &&
                                     FocusManager.GetFocusedElement(root.XamlRoot) is TextBox or PasswordBox or RichEditBox;
        if (_viewModel.Editor is not null ||
            !GlobalShortcutRouter.ShouldCreateNewProject(
                e.Handled,
                editableControlFocused,
                controlDown,
                e.Key,
                e.KeyStatus.WasKeyDown))
        {
            return;
        }

        e.Handled = true;
        await _homeView.CreateProjectAsync();
    }

    private void Editor_WorkspaceSettingsChanged(object? sender, WorkspaceSettingsChangedEventArgs e)
    {
        if (!_lifecycle.CanHandleEditorEvent(_editorView, sender))
        {
            return;
        }

        _appSettings.TimelineZoom = e.TimelineZoom;
        _appSettings.TimelineHeight = e.TimelineHeight;
        _appSettings.LoopPlayback = e.LoopPlayback;
        _appSettings.LastExportFolder = e.LastExportFolder;
        _settingsSaveCoordinator.NotifyEdited();
    }

    private void SettingsSaveCoordinator_StateChanged(object? sender, EventArgs e)
    {
        var state = _settingsSaveCoordinator.State;
        if (state == DebouncedSaveState.SaveFailed)
        {
            _ = _logService.TryWriteAsync("Workspace settings save failed.");
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_lifecycle.CanContinueInitialization)
            {
                return;
            }

            if (state == DebouncedSaveState.Saving)
            {
                _workspaceSettingsSaveEditor = _editorView;
                _workspaceSettingsSavePublication = _editorView?.CaptureInfoBarPublication() ?? default;
            }
            else if (state == DebouncedSaveState.Saved)
            {
                _workspaceSettingsSaveEditor = null;
            }

            var noticeTargetsCurrentView =
                (_editorView is not null && ReferenceEquals(_editorView, _workspaceSettingsSaveEditor)) ||
                (_editorView is null && _workspaceSettingsSaveEditor is null);
            if ((state != DebouncedSaveState.SaveFailed || noticeTargetsCurrentView) &&
                _workspaceSettingsSaveFailureNotice.Observe(state))
            {
                ReportWorkspaceSettingsSaveFailure();
            }

            if (state == DebouncedSaveState.SaveFailed)
            {
                _workspaceSettingsSaveEditor = null;
            }
        });
    }

    private void ReportWorkspaceSettingsSaveFailure()
    {
        const string title = "Could not save workspace settings";
        const string message = "Your timeline and playback preferences could not be saved. Try changing them again.";
        if (_editorView is { } editor && ReferenceEquals(editor, _workspaceSettingsSaveEditor))
        {
            editor.ReportError(_workspaceSettingsSavePublication, title, message);
        }
        else if (_editorView is null && _workspaceSettingsSaveEditor is null)
        {
            _viewModel.Home.ReportError($"{title}. {message}");
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_lifecycle.CloseApproved)
        {
            return;
        }

        args.Cancel = true;
        if (!_lifecycle.TryBeginClosing())
        {
            return;
        }

        _ = SaveBeforeClosingAsync(sender);
    }

    private async Task SaveBeforeClosingAsync(AppWindow appWindow)
    {
        try
        {
            if (!await _lifecycle.WaitForInitializationBeforeCloseAsync(_initializationTask))
            {
                return;
            }

            if (_editorView is not null && !await _editorView.PrepareToCloseAsync())
            {
                return;
            }

            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

            var closeBounds = CaptureBoundsForPersistence(appWindow);
            await SaveWindowSettingsAsync(closeBounds);
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

            if (_editorView is not null && !await _editorView.PrepareToCloseAsync())
            {
                return;
            }

            _lifecycle.ApproveClose();
            try
            {
                _lifecycle.TryCommitClose(Close);
            }
            catch
            {
                _lifecycle.CancelClosing();
                throw;
            }
        }
        catch (Exception exception) when (!ExceptionPolicy.IsFatal(exception))
        {
            _ = _logService.TryWriteAsync($"Close save failed: {exception.GetType().Name}");
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

            _workspaceSettingsSaveFailureNotice.Observe(_settingsSaveCoordinator.State);
            if (_editorView is not null)
            {
                _editorView.ReportError("Could not close safely", "The workspace could not be saved. Try again before closing the editor.");
            }
            else
            {
                _viewModel.Home.ReportError("Could not save window settings. Try again before closing the app.");
            }
        }
        finally
        {
            if (!_lifecycle.CloseApproved)
            {
                _lifecycle.CancelClosing();
                _editorView?.CancelClosePreparation();
            }
        }
    }

    private WindowGeometry CaptureBoundsForPersistence(AppWindow appWindow)
    {
        var currentBounds = GetCurrentBounds(appWindow);
        var isRestored = IsRestored(appWindow);
        var bounds = WindowGeometry.SelectBoundsForPersistence(currentBounds, _restoredWindowBounds, isRestored);
        if (isRestored)
        {
            _restoredWindowBounds = bounds;
        }

        return bounds;
    }

    private static WindowGeometry GetCurrentBounds(AppWindow appWindow) =>
        new(appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);

    private static bool IsRestored(AppWindow appWindow) =>
        appWindow.Presenter is not OverlappedPresenter presenter ||
        presenter.State == OverlappedPresenterState.Restored;

    private async Task SaveWindowSettingsAsync(WindowGeometry bounds)
    {
        var displayArea = DisplayArea.GetFromRect(ToRect(bounds), DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var outerBounds = displayArea.OuterBounds;
        var currentDpi = GetDisplayDpi(outerBounds);
        bounds = WindowGeometry.ClampPhysicalToWorkArea(
            bounds,
            new WindowGeometry(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            currentDpi);

        _appSettings.WindowBoundsVersion = AppSettings.CurrentWindowBoundsVersion;
        _appSettings.WindowPixelX = bounds.X;
        _appSettings.WindowPixelY = bounds.Y;
        _appSettings.WindowPixelWidth = bounds.Width;
        _appSettings.WindowPixelHeight = bounds.Height;

        _appSettings.WindowX = ToLogical(bounds.X, currentDpi);
        _appSettings.WindowY = ToLogical(bounds.Y, currentDpi);
        _appSettings.WindowWidth = ToLogical(bounds.Width, currentDpi);
        _appSettings.WindowHeight = ToLogical(bounds.Height, currentDpi);
        _settingsSaveCoordinator.NotifyEdited();
        await _settingsSaveCoordinator.FlushAsync();
    }

    private static double ToLogical(int value, uint dpi) => value * 96d / (dpi == 0 ? 96 : dpi);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromRect(ref NativeRect rectangle, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint windowHandle, uint flags);

    [LibraryImport("shcore.dll")]
    private static partial int GetScaleFactorForMonitor(nint monitor, out int scaleFactor);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect(int left, int top, int right, int bottom)
    {
        public readonly int Left = left;
        public readonly int Top = top;
        public readonly int Right = right;
        public readonly int Bottom = bottom;
    }

    private readonly record struct AppWindowContext(AppWindow AppWindow);

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _lifecycle.MarkClosed();
        _windowReady.TrySetCanceled();
        if (_appWindow is not null)
        {
            _appWindow.Closing -= AppWindow_Closing;
            _appWindow.Changed -= AppWindow_Changed;
            _appWindow = null;
        }

        _settingsSaveCoordinator.StateChanged -= SettingsSaveCoordinator_StateChanged;
        _settingsSaveCoordinator.Dispose();
        DetachEditorView();
    }
}

internal sealed class MainWindowLifecycle
{
    private Task? _initialization;
    private bool _closing;
    private bool _closed;

    public bool CanContinueInitialization => !_closing && !_closed;

    public bool CanContinueClosing => !_closed;

    public bool CanHandleEditorEvent(object? currentEditor, object? sender) =>
        CanContinueInitialization && currentEditor is not null && ReferenceEquals(currentEditor, sender);

    public bool CloseApproved { get; private set; }

    public Task StartInitialization(Func<Task> initializeAsync)
    {
        ArgumentNullException.ThrowIfNull(initializeAsync);
        if (_initialization is not null)
        {
            throw new InvalidOperationException("Window initialization can only be started once.");
        }

        return _initialization = initializeAsync();
    }

    public bool TryBeginClosing()
    {
        if (_closing || _closed)
        {
            return false;
        }

        _closing = true;
        return true;
    }

    public async Task<bool> WaitForInitializationBeforeCloseAsync(Task initialization)
    {
        if (!ReferenceEquals(initialization, _initialization))
        {
            throw new InvalidOperationException("Closing must await the window's initialization task.");
        }

        await initialization;
        return CanContinueClosing;
    }

    public void ApproveClose() => CloseApproved = true;

    public bool TryCommitClose(Action close)
    {
        ArgumentNullException.ThrowIfNull(close);
        if (!CloseApproved || _closed)
        {
            return false;
        }

        close();
        return true;
    }

    public void CancelClosing()
    {
        if (_closed)
        {
            return;
        }

        CloseApproved = false;
        _closing = false;
    }

    public void MarkClosed()
    {
        _closed = true;
        _closing = false;
        CloseApproved = false;
    }
}
