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
    private readonly SimpleLogService _logService = new();
    private readonly WorkspaceSettingsSaveFailureNotice _workspaceSettingsSaveFailureNotice = new();
    private readonly TaskCompletionSource<AppWindowContext> _windowReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MainWindowLifecycle _lifecycle = new();
    private readonly Task _initializationTask;
    private EditorView? _editorView;
    private AppWindow? _appWindow;
    private AppSettings _appSettings = AppSettings.Normalize(null);

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
        _settingsSaveCoordinator = new DebouncedSaveCoordinator(() => _settingsService.SaveAsync(_appSettings));
        _settingsSaveCoordinator.StateChanged += SettingsSaveCoordinator_StateChanged;
        _viewModel = new MainViewModel(_projectService, _mediaImportService);
        _viewModel.CurrentViewChanged += (_, _) => ShowCurrentView();
        _homeView = new HomeView(_viewModel.Home);
        _homeView.ProjectOpenRequested = project => _viewModel.OpenEditorAsync(project);
        _initializationTask = _lifecycle.StartInitialization(InitializeAsync);
    }

    private void ShowCurrentView()
    {
        if (!_lifecycle.CanContinueInitialization)
        {
            return;
        }

        if (_viewModel.Editor is not null)
        {
            _editorView?.Dispose();
            _editorView = new EditorView(_viewModel.Editor, _projectService, _mediaImportService, _appSettings);
            _editorView.ReturnHomeRequested += async (_, _) => await _viewModel.ShowHomeAsync();
            _editorView.NewProjectRequested += async (_, _) =>
            {
                if (await _viewModel.ShowHomeAsync())
                {
                    await _homeView.CreateProjectAsync();
                }
            };
            _editorView.ImportRequested += Editor_ImportRequested;
            _editorView.RelinkAssetRequested += Editor_RelinkAssetRequested;
            _editorView.ExportRequested += Editor_ExportRequested;
            _editorView.WorkspaceSettingsChanged += Editor_WorkspaceSettingsChanged;
            ContentHost.Content = _editorView;
            SetTitleBar(_editorView.TitleBarElement);
            return;
        }

        _editorView?.Dispose();
        _editorView = null;
        ContentHost.Content = _homeView;
        SetTitleBar(_homeView.TitleBarElement);
    }

    private async Task InitializeAsync()
    {
        AppSettings loadedSettings = AppSettings.Normalize(null);
        string? settingsError = null;
        try
        {
            var context = await _windowReady.Task;
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

            try
            {
                loadedSettings = await _settingsService.LoadAsync();
            }
            catch (Exception exception)
            {
                settingsError = $"Could not restore window settings. {exception.Message}";
            }

            if (!_lifecycle.CanContinueClosing)
            {
                return;
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
            catch (Exception exception)
            {
                settingsError = $"Could not restore window geometry. {exception.Message}";
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
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }
        }
        catch (OperationCanceledException) when (!_lifecycle.CanContinueClosing)
        {
        }
        catch (Exception exception)
        {
            if (_lifecycle.CanContinueInitialization)
            {
                _viewModel.Home.ReportError(exception.Message);
                ShowCurrentView();
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
        _appWindow.Closing += AppWindow_Closing;
        _appWindow.Changed += AppWindow_Changed;
        _windowReady.TrySetResult(new AppWindowContext(_appWindow));
    }

    private void RestoreWindowGeometry(AppWindowContext context)
    {
        var hasPhysicalBounds = WindowGeometry.TryGetStoredPhysicalBounds(_appSettings, out var requestedBounds);
        var displayArea = hasPhysicalBounds
            ? DisplayArea.GetFromRect(ToRect(requestedBounds), DisplayAreaFallback.Nearest)
            : DisplayArea.GetFromRect(
                ToRect(WindowGeometry.FromLegacyLogicalSettings(_appSettings, targetDpi: 96)),
                DisplayAreaFallback.Nearest);
        var targetDpi = GetDisplayDpi(displayArea);
        if (!hasPhysicalBounds)
        {
            requestedBounds = WindowGeometry.FromLegacyLogicalSettings(_appSettings, targetDpi);
        }

        var workArea = displayArea.WorkArea;
        var physicalWorkArea = new WindowGeometry(workArea.X, workArea.Y, workArea.Width, workArea.Height);
        var geometry = WindowGeometry.ClampPhysicalToWorkArea(requestedBounds, physicalWorkArea, targetDpi);
        ApplyPreferredMinimum(context.AppWindow, workArea, targetDpi);
        context.AppWindow.MoveAndResize(ToRect(geometry));
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange || !_lifecycle.CanContinueInitialization)
        {
            return;
        }

        try
        {
            var bounds = new WindowGeometry(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
            var displayArea = DisplayArea.GetFromRect(ToRect(bounds), DisplayAreaFallback.Nearest);
            ApplyPreferredMinimum(sender, displayArea.WorkArea, GetWindowDpi());
        }
        catch (Exception exception)
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
        var bounds = displayArea.OuterBounds;
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

    private async void Editor_ImportRequested(object? sender, MediaImportRequestedEventArgs e)
    {
        if (sender is not EditorView editor)
        {
            return;
        }

        try
        {
            var files = await FilePickerHelper.PickMediaFilesAsync(GetWindowHandle(), e.Scope);
            if (ReferenceEquals(_editorView, editor))
            {
                await editor.ImportFilesAsync(files);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            if (ReferenceEquals(_editorView, editor))
            {
                editor.ReportError("Could not open media", $"The file picker or import failed. {exception.Message}");
            }
        }
    }

    private async void Editor_RelinkAssetRequested(object? sender, Controls.AssetActionEventArgs e)
    {
        if (sender is not EditorView editor || editor.FindAsset(e.AssetId) is not { } asset)
        {
            return;
        }

        try
        {
            var file = await FilePickerHelper.PickRelinkFileAsync(GetWindowHandle(), asset.Kind);
            if (file is not null && ReferenceEquals(_editorView, editor))
            {
                await editor.RelinkAssetAsync(e.AssetId, file);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            if (ReferenceEquals(_editorView, editor))
            {
                editor.ReportError($"Could not relink '{asset.FileName}'", $"The replacement could not be opened. {exception.Message}");
            }
        }
    }

    private async void Editor_ExportRequested(object? sender, EventArgs e)
    {
        if (sender is not EditorView editor || !ReferenceEquals(_editorView, editor))
        {
            return;
        }

        try
        {
            await editor.ExportAsync(GetWindowHandle());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (ReferenceEquals(_editorView, editor))
            {
                editor.ReportError("Export failed", $"{AppInfo.ProductName} could not start the export. Try again or choose another output location.");
                _ = _logService.TryWriteAsync($"Export start failed: {exception.GetType().Name}");
            }
        }
    }

    private nint GetWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private async void MainWindowRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var controlDown = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var editableControlFocused = Content is FrameworkElement root &&
                                     FocusManager.GetFocusedElement(root.XamlRoot) is TextBox or PasswordBox or RichEditBox;
        if (_viewModel.Editor is not null || !GlobalShortcutRouter.ShouldCreateNewProject(e.Handled, editableControlFocused, controlDown, e.Key))
        {
            return;
        }

        e.Handled = true;
        await _homeView.CreateProjectAsync();
    }

    private void Editor_WorkspaceSettingsChanged(object? sender, WorkspaceSettingsChangedEventArgs e)
    {
        if (!_lifecycle.CanContinueInitialization)
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
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

            if (_workspaceSettingsSaveFailureNotice.Observe(state))
            {
                ReportWorkspaceSettingsSaveFailure();
            }
        });
    }

    private void ReportWorkspaceSettingsSaveFailure()
    {
        const string title = "Could not save workspace settings";
        const string message = "Your timeline and playback preferences could not be saved. Try changing them again.";
        if (_editorView is not null)
        {
            _editorView.ReportError(title, message);
        }
        else
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

            await SaveWindowSettingsAsync(appWindow);
            if (!_lifecycle.CanContinueClosing)
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
        catch (Exception exception)
        {
            _ = _logService.TryWriteAsync($"Close save failed: {exception.GetType().Name}");
            if (!_lifecycle.CanContinueClosing)
            {
                return;
            }

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

    private async Task SaveWindowSettingsAsync(AppWindow appWindow)
    {
        var position = appWindow.Position;
        var size = appWindow.Size;
        _appSettings.WindowBoundsVersion = AppSettings.CurrentWindowBoundsVersion;
        _appSettings.WindowPixelX = position.X;
        _appSettings.WindowPixelY = position.Y;
        _appSettings.WindowPixelWidth = size.Width;
        _appSettings.WindowPixelHeight = size.Height;

        var currentBounds = new WindowGeometry(position.X, position.Y, size.Width, size.Height);
        var currentDpi = GetDisplayDpi(DisplayArea.GetFromRect(ToRect(currentBounds), DisplayAreaFallback.Nearest));
        _appSettings.WindowX = ToLogical(position.X, currentDpi);
        _appSettings.WindowY = ToLogical(position.Y, currentDpi);
        _appSettings.WindowWidth = ToLogical(size.Width, currentDpi);
        _appSettings.WindowHeight = ToLogical(size.Height, currentDpi);
        _settingsSaveCoordinator.NotifyEdited();
        await _settingsSaveCoordinator.FlushAsync();
    }

    private static double ToLogical(int value, uint dpi) => value * 96d / (dpi == 0 ? 96 : dpi);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromRect(ref NativeRect rectangle, uint flags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetScaleFactorForMonitor(nint monitor, out int scaleFactor);

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
        _editorView?.Dispose();
        _editorView = null;
    }
}

internal sealed class MainWindowLifecycle
{
    private Task? _initialization;
    private bool _closing;
    private bool _closed;

    public bool CanContinueInitialization => !_closing && !_closed;

    public bool CanContinueClosing => !_closed;

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
