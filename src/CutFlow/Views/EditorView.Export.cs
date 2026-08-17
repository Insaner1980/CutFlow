using System.Runtime.InteropServices;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CutFlow.Views;

public sealed partial class EditorView
{
    private static readonly TimeSpan ExportCloseTimeout = TimeSpan.FromSeconds(10);
    private CancellationTokenSource? _exportCts;
    private Task? _activeExportTask;
    private ExportResultAction? _lastExportResult;
    private readonly ExportOperationState _exportState = new();
    private bool _isExporting => _exportState.IsActive;
    private bool _isRendering => _exportState.IsRendering;

    public async Task ExportAsync(nint windowHandle)
    {
        EnsureActive();
        if (!TryBeginExport(out var operationId))
        {
            return;
        }

        RefreshExportAvailability();
        try
        {
            var validationSnapshot = ExportService.CreateProjectSnapshot(ViewModel.Project);
            var validation = await ExportPreflight.ValidateAsync(validationSnapshot, _lifetimeToken);
            if (!CanContinueExport(operationId))
            {
                return;
            }

            if (!validation.CanExport)
            {
                ShowMessage(InfoBarSeverity.Error, "Could not export project", validation.ErrorMessage);
                return;
            }

            if (!await SaveAsync() || !CanContinueExport(operationId))
            {
                return;
            }

            var savedProjectSnapshot = ExportService.CreateProjectSnapshot(ViewModel.Project);
            var selection = await ShowExportDialogAsync();
            if (selection is null || !CanContinueExport(operationId))
            {
                return;
            }

            var destination = await PickExportDestinationAsync(windowHandle, operationId, selection);
            if (destination is null || !_exportState.TryBeginRender(operationId))
            {
                return;
            }

            var operation = RunExportAsync(savedProjectSnapshot, destination.Path, destination.Options);
            _activeExportTask = operation;
            await TrackExportOperationAsync(operation);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested || !CanContinueExport(operationId))
        {
            // Closing or a superseding export operation owns the remaining cleanup.
        }
        catch (Exception exception) when (IsExpectedExportException(exception))
        {
            if (CanContinueExport(operationId))
            {
                ReportExportFailure(exception);
            }
        }
        finally
        {
            _exportState.Complete(operationId);
            if (!_disposed)
            {
                RefreshExportAvailability();
            }
        }
    }

    private bool TryBeginExport(out long operationId)
    {
        operationId = 0;
        return ExportPresentation.CanStartExport(ViewModel.Project, _isExporting) &&
            _exportState.TryBegin(out operationId);
    }

    private async Task<ExportDestination?> PickExportDestinationAsync(
        nint windowHandle,
        long operationId,
        ExportDialogSelection selection)
    {
        try
        {
            var path = await FilePickerHelper.PickExportFileAsync(
                windowHandle,
                selection.FileName,
                _workspaceSettings.LastExportFolder,
                _lifetimeToken);
            if (string.IsNullOrWhiteSpace(path) || !CanContinueExport(operationId))
            {
                return null;
            }

            _workspaceSettings.LastExportFolder = Path.GetDirectoryName(path) ?? string.Empty;
            RaiseWorkspaceSettingsChanged();
            return new ExportDestination(path, selection.Options);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested || !CanContinueExport(operationId))
        {
            return null;
        }
        catch (Exception exception) when (IsExpectedExportException(exception))
        {
            if (CanContinueExport(operationId))
            {
                ReportExportFailure(exception);
            }

            return null;
        }
    }

    private async Task TrackExportOperationAsync(Task operation)
    {
        try
        {
            await operation;
        }
        finally
        {
            if (ReferenceEquals(_activeExportTask, operation))
            {
                _activeExportTask = null;
            }
        }
    }

    private bool CanContinueExport(long operationId) =>
        !_disposed && _exportState.CanContinue(operationId);

    public async Task<bool> PrepareToCloseAsync()
    {
        CommitProjectName();
        _exportState.BeginClosing();
        if (_exportCts is { } cancellation)
        {
            await cancellation.CancelAsync();
        }

        if (_activeExportTask is { } operation &&
            !await WaitForExportCleanupAsync(operation, ExportCloseTimeout))
        {
            ReportError(
                "Export is still stopping",
                $"{AppInfo.ProductName} stayed open so the export can finish safely. Try closing again.");
            return false;
        }

        return await SaveAsync();
    }

    internal static async Task<bool> WaitForExportCleanupAsync(Task operation, TimeSpan timeout)
    {
        try
        {
            await operation.WaitAsync(timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void CancelClosePreparation()
    {
        _exportState.CancelClosing();
        if (!_disposed)
        {
            RefreshExportAvailability();
        }
    }

    private async Task RunExportAsync(
        ProjectDocument projectSnapshot,
        string destinationPath,
        ExportOptions options)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        _exportCts = cancellation;
        ShowExportProgress(Path.GetFileName(destinationPath));
        try
        {
            var progress = new ExportProgressDispatcher(
                callback => DispatcherQueue.TryEnqueue(() => callback()),
                value => ApplyExportProgress(value, cancellation),
                cancellation.Token);
            var service = new ExportService(_compositionService, _textOverlayRenderer, _logService);
            var result = await service.ExportToPathAsync(
                projectSnapshot,
                ViewModel.Project,
                destinationPath,
                options,
                progress,
                cancellation.Token);
            if (_disposed || !ReferenceEquals(_exportCts, cancellation))
            {
                return;
            }

            switch (ExportPresentation.ResolveCompletion(result.Status, cancellation.IsCancellationRequested))
            {
                case ExportCompletionState.Success:
                    ShowExportSuccess(result.DestinationPath);
                    break;
                case ExportCompletionState.Cancelled:
                    HideExportStatus();
                    ShowMessage(InfoBarSeverity.Informational, "Export cancelled", "No output file was changed.");
                    break;
                default:
                    HideExportStatus();
                    ShowMessage(InfoBarSeverity.Error, "Export failed", result.ErrorMessage);
                    _ = _logService.TryWriteAsync($"Export failed: {result.Status}", CancellationToken.None);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_disposed && ReferenceEquals(_exportCts, cancellation))
            {
                HideExportStatus();
                ShowMessage(InfoBarSeverity.Informational, "Export cancelled", "No output file was changed.");
            }
        }
        catch (Exception exception) when (IsExpectedExportException(exception))
        {
            if (!_disposed && ReferenceEquals(_exportCts, cancellation))
            {
                HideExportStatus();
                ReportExportFailure(exception);
            }
        }
        finally
        {
            if (ReferenceEquals(_exportCts, cancellation))
            {
                _exportCts = null;
            }

        }
    }

    private async Task<ExportDialogSelection?> ShowExportDialogAsync()
    {
        var fileNameBox = new TextBox
        {
            Header = "Output filename",
            Text = ExportPresentation.NormalizeSuggestedFileName(ViewModel.ProjectName),
            MaxLength = 120
        };
        AutomationProperties.SetName(fileNameBox, "Output filename");

        var validationText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        AutomationProperties.SetLiveSetting(validationText, AutomationLiveSetting.Polite);

        var resolutionBox = new ComboBox
        {
            Header = "Resolution",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0,
            Items = { "720p", "1080p" }
        };
        AutomationProperties.SetName(resolutionBox, "Export resolution");

        var qualityBox = new ComboBox
        {
            Header = "Quality",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedIndex = 0,
            Items = { "Standard", "High" }
        };
        AutomationProperties.SetName(qualityBox, "Export quality");

        var optionGrid = new Grid { ColumnSpacing = 12 };
        optionGrid.ColumnDefinitions.Add(new ColumnDefinition());
        optionGrid.ColumnDefinitions.Add(new ColumnDefinition());
        optionGrid.Children.Add(resolutionBox);
        Grid.SetColumn(qualityBox, 1);
        optionGrid.Children.Add(qualityBox);

        var lastFolder = _workspaceSettings.LastExportFolder;
        var locationText = string.IsNullOrWhiteSpace(lastFolder)
            ? "Choose an output location in the Windows Save dialog."
            : $"Last used: {lastFolder}\nConfirm it or choose another location in the Windows Save dialog.";
        var content = new StackPanel
        {
            Width = 440,
            Spacing = 16,
            Children =
            {
                fileNameBox,
                validationText,
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "Output location", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = locationText, TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextSecondaryBrush"] }
                    }
                },
                optionGrid,
                new Border
                {
                    Padding = new Thickness(12),
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlBackgroundBrush"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
                    Child = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock { Text = "MP4 · H.264 video · AAC audio · 30 fps", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = $"Project duration: {ViewModel.DurationText}", Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextSecondaryBrush"] }
                        }
                    }
                }
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = EditorRoot.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
            Title = "Export video",
            Content = content,
            PrimaryButtonText = "Choose location",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        ExportDialogSelection? selection = null;
        dialog.Opened += (_, _) =>
        {
            fileNameBox.Focus(FocusState.Programmatic);
            fileNameBox.SelectAll();
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            validationText.Visibility = Visibility.Collapsed;
            if (!ExportPresentation.TryValidateFileName(fileNameBox.Text, out var fileName))
            {
                fileNameBox.Text = fileName;
                validationText.Text = "The filename was adjusted to a valid MP4 name. Review it, then choose the location again.";
                validationText.Visibility = Visibility.Visible;
                fileNameBox.Focus(FocusState.Programmatic);
                fileNameBox.SelectAll();
                args.Cancel = true;
                return;
            }

            if (!ExportPresentation.TryCreateOptions(
                    resolutionBox.SelectedIndex,
                    qualityBox.SelectedIndex,
                    out var options))
            {
                validationText.Text = "Choose a supported resolution and quality.";
                validationText.Visibility = Visibility.Visible;
                (resolutionBox.SelectedIndex is 0 or 1 ? qualityBox : resolutionBox).Focus(FocusState.Programmatic);
                args.Cancel = true;
                return;
            }

            selection = new ExportDialogSelection(fileName, options);
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return selection;
    }

    private void ShowExportProgress(string fileName)
    {
        _lastExportResult = null;
        ExportStatusTitle.Text = "Exporting video";
        ExportStatusMessage.Text = $"Creating '{fileName}' at full resolution. You can keep working in the editor.";
        ExportProgressBar.Value = 0;
        ExportProgressText.Text = "0%";
        ExportProgressActions.Visibility = Visibility.Visible;
        ExportSuccessActions.Visibility = Visibility.Collapsed;
        DismissExportStatusButton.Visibility = Visibility.Collapsed;
        CancelExportButton.IsEnabled = true;
        ExportStatusPanel.Visibility = Visibility.Visible;
    }

    private void ApplyExportProgress(double value, CancellationTokenSource operation)
    {
        if (!CanApplyExportProgress(_disposed, _exportCts, operation))
        {
            return;
        }

        ExportProgressBar.Value = value;
        ExportProgressText.Text = $"{value:0}%";
    }

    internal static bool CanApplyExportProgress(
        bool disposed,
        CancellationTokenSource? currentOperation,
        CancellationTokenSource operation) =>
        !disposed && ReferenceEquals(currentOperation, operation) && !operation.IsCancellationRequested;

    private void ShowExportSuccess(string destinationPath)
    {
        _lastExportResult = new ExportResultAction(destinationPath);
        ExportStatusTitle.Text = "Export complete";
        ExportStatusMessage.Text = $"'{Path.GetFileName(destinationPath)}' is ready.";
        ExportProgressBar.Value = 100;
        ExportProgressText.Text = "100%";
        ExportProgressActions.Visibility = Visibility.Collapsed;
        ExportSuccessActions.Visibility = Visibility.Visible;
        DismissExportStatusButton.Visibility = Visibility.Visible;
        ExportStatusPanel.Visibility = Visibility.Visible;
    }

    private void RefreshExportAvailability()
    {
        ExportButton.IsEnabled = ExportPresentation.CanStartExport(ViewModel.Project, _isExporting);
        var helpText = string.Empty;
        if (!ExportButton.IsEnabled)
        {
            helpText = _isExporting
                ? "Export setup is open or rendering is in progress"
                : "Add visual media to V1 before exporting";
        }

        AutomationProperties.SetHelpText(ExportButton, helpText);
    }

    private void HideExportStatus() => ExportStatusPanel.Visibility = Visibility.Collapsed;

    private void CancelExport_Click(object sender, RoutedEventArgs e)
    {
        CancelExportButton.IsEnabled = false;
        ExportStatusMessage.Text = "Cancelling export…";
        _exportCts?.Cancel();
    }

    private void DismissExportStatus_Click(object sender, RoutedEventArgs e) => HideExportStatus();

    private async void OpenExportedFile_Click(object sender, RoutedEventArgs e) =>
        await OpenExportResultAsync(openFolder: false);

    private async void OpenExportFolder_Click(object sender, RoutedEventArgs e) =>
        await OpenExportResultAsync(openFolder: true);

    private async Task OpenExportResultAsync(bool openFolder)
    {
        var operation = _lastExportResult;
        if (_disposed || operation is null || !operation.TryBegin())
        {
            return;
        }

        try
        {
            bool launched;
            if (openFolder)
            {
                var folderPath = Path.GetDirectoryName(operation.DestinationPath)
                    ?? throw new InvalidOperationException("The export folder is unavailable.");
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
                if (!CanContinueOpenExportResult(_disposed, _lastExportResult, operation))
                {
                    return;
                }

                launched = await Launcher.LaunchFolderAsync(folder);
            }
            else
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(operation.DestinationPath);
                if (!CanContinueOpenExportResult(_disposed, _lastExportResult, operation))
                {
                    return;
                }

                launched = await Launcher.LaunchFileAsync(file);
            }

            if (!launched)
            {
                ReportOpenExportFailure(operation, openFolder, "LauncherReturnedFalse");
            }
        }
        catch (Exception exception) when (IsExpectedExportException(exception) || exception is InvalidOperationException)
        {
            ReportOpenExportFailure(operation, openFolder, exception.GetType().Name);
        }
        finally
        {
            operation.Complete();
        }
    }

    internal static bool CanContinueOpenExportResult(
        bool disposed,
        ExportResultAction? currentOperation,
        ExportResultAction operation) =>
        !disposed && ReferenceEquals(currentOperation, operation);

    private void ReportOpenExportFailure(ExportResultAction operation, bool openFolder, string reason)
    {
        if (!CanContinueOpenExportResult(_disposed, _lastExportResult, operation))
        {
            return;
        }

        _lastExportResult = null;
        HideExportStatus();
        ReportError(
            openFolder ? "Could not open export folder" : "Could not open exported file",
            "The exported item may have been moved or is no longer available.");
        _ = _logService.TryWriteAsync($"Open export result failed: {reason}", CancellationToken.None);
    }

    private void ReportExportFailure(Exception exception)
    {
        var message = ExportFailureMapper.GetExceptionMessage(exception)
            ?? throw new InvalidOperationException("Unexpected export exceptions must not be converted to user-facing failures.");
        ShowMessage(InfoBarSeverity.Error, "Export failed", message);
        _ = _logService.TryWriteAsync(
            $"Export failed: {exception.GetType().Name}; HRESULT=0x{exception.HResult:X8}",
            CancellationToken.None);
    }

    internal static bool IsExpectedExportException(Exception exception) =>
        ExportFailureMapper.GetExceptionMessage(exception) is not null;

    private sealed record ExportDialogSelection(string FileName, ExportOptions Options);
    private sealed record ExportDestination(string Path, ExportOptions Options);
}

internal sealed class ExportResultAction(string destinationPath)
{
    private bool _isActive;

    public string DestinationPath { get; } = destinationPath;

    public bool TryBegin()
    {
        if (_isActive)
        {
            return false;
        }

        _isActive = true;
        return true;
    }

    public void Complete() => _isActive = false;
}

internal sealed class ExportProgressDispatcher : IProgress<double>
{
    private const double MaximumActiveProgress = 99;
    private readonly object _sync = new();
    private readonly Func<Action, bool> _tryEnqueue;
    private readonly Action<double> _apply;
    private readonly CancellationToken _cancellationToken;
    private double _latestValue;
    private bool _updateQueued;

    public ExportProgressDispatcher(
        Func<Action, bool> tryEnqueue,
        Action<double> apply,
        CancellationToken cancellationToken)
    {
        _tryEnqueue = tryEnqueue;
        _apply = apply;
        _cancellationToken = cancellationToken;
    }

    public void Report(double value)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var normalized = Math.Min(ExportPresentation.NormalizeProgress(value), MaximumActiveProgress);
        lock (_sync)
        {
            if (_cancellationToken.IsCancellationRequested || normalized <= _latestValue)
            {
                return;
            }

            _latestValue = normalized;
            if (_updateQueued)
            {
                return;
            }

            _updateQueued = true;
        }

        if (!_tryEnqueue(Drain))
        {
            lock (_sync)
            {
                _updateQueued = false;
            }
        }
    }

    private void Drain()
    {
        double value;
        lock (_sync)
        {
            _updateQueued = false;
            value = _latestValue;
        }

        if (!_cancellationToken.IsCancellationRequested)
        {
            _apply(value);
        }
    }
}
