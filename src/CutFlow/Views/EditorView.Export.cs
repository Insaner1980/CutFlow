using System.Runtime.InteropServices;
using CutFlow.Services;
using CutFlow.Utilities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CutFlow.Views;

public sealed partial class EditorView
{
    private CancellationTokenSource? _exportCts;
    private Task? _activeExportTask;
    private string? _lastExportPath;
    private readonly ExportOperationState _exportState = new();
    private bool _isExporting => _exportState.IsActive;
    private bool _isRendering => _exportState.IsRendering;

    public async Task ExportAsync(nint windowHandle)
    {
        EnsureActive();
        if (!ExportPresentation.CanStartExport(ViewModel.Project, _isExporting) ||
            !_exportState.TryBegin(out var operationId))
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

            var selection = await ShowExportDialogAsync();
            if (selection is null || !CanContinueExport(operationId))
            {
                return;
            }

            string? destinationPath;
            try
            {
                destinationPath = await FilePickerHelper.PickExportFileAsync(
                    windowHandle,
                    selection.FileName,
                    _workspaceSettings.LastExportFolder,
                    _lifetimeToken);
            }
            catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested || !CanContinueExport(operationId))
            {
                return;
            }
            catch (Exception exception) when (IsExpectedExportException(exception))
            {
                if (CanContinueExport(operationId))
                {
                    ReportExportFailure(exception);
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(destinationPath) || !CanContinueExport(operationId))
            {
                return;
            }

            _workspaceSettings.LastExportFolder = Path.GetDirectoryName(destinationPath) ?? string.Empty;
            RaiseWorkspaceSettingsChanged();
            if (!_exportState.TryBeginRender(operationId))
            {
                return;
            }

            var operation = RunExportAsync(destinationPath, selection.Options);
            _activeExportTask = operation;
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
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested || !CanContinueExport(operationId))
        {
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

    private bool CanContinueExport(long operationId) =>
        !_disposed && _exportState.CanContinue(operationId);

    public async Task<bool> PrepareToCloseAsync()
    {
        _exportState.BeginClosing();
        _exportCts?.Cancel();
        if (_activeExportTask is { } operation)
        {
            await operation;
        }

        return await SaveAsync();
    }

    public void CancelClosePreparation()
    {
        _exportState.CancelClosing();
        if (!_disposed)
        {
            RefreshExportAvailability();
        }
    }

    private async Task RunExportAsync(string destinationPath, ExportOptions options)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken);
        _exportCts = cancellation;
        ShowExportProgress(Path.GetFileName(destinationPath));
        try
        {
            var progress = new Progress<double>(value => UpdateExportProgress(value, cancellation));
            var service = new ExportService(_compositionService, _textOverlayRenderer, _logService);
            var result = await service.ExportToPathAsync(
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
                    _ = _logService.TryWriteAsync($"Export failed: {result.Status}");
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
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return new ExportDialogSelection(
            ExportPresentation.NormalizeSuggestedFileName(fileNameBox.Text),
            new ExportOptions(
                resolutionBox.SelectedIndex == 1 ? ExportResolutionTier.FullHd1080p : ExportResolutionTier.Hd720p,
                qualityBox.SelectedIndex == 1 ? ExportQuality.High : ExportQuality.Standard));
    }

    private void ShowExportProgress(string fileName)
    {
        _lastExportPath = null;
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

    private void UpdateExportProgress(double value, CancellationTokenSource operation)
    {
        var normalized = ExportPresentation.NormalizeProgress(value);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed || !ReferenceEquals(_exportCts, operation))
            {
                return;
            }

            ExportProgressBar.Value = normalized;
            ExportProgressText.Text = $"{normalized:0}%";
        });
    }

    private void ShowExportSuccess(string destinationPath)
    {
        _lastExportPath = destinationPath;
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
        AutomationProperties.SetHelpText(
            ExportButton,
            ExportButton.IsEnabled ? string.Empty : _isExporting ? "Export setup is open or rendering is in progress" : "Add visual media to V1 before exporting");
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
        if (string.IsNullOrWhiteSpace(_lastExportPath) || _disposed)
        {
            return;
        }

        try
        {
            bool launched;
            if (openFolder)
            {
                var folderPath = Path.GetDirectoryName(_lastExportPath)
                    ?? throw new InvalidOperationException("The export folder is unavailable.");
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folderPath);
                launched = await Launcher.LaunchFolderAsync(folder);
            }
            else
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(_lastExportPath);
                launched = await Launcher.LaunchFileAsync(file);
            }

            if (!launched)
            {
                ReportOpenExportFailure(openFolder, "LauncherReturnedFalse");
            }
        }
        catch (Exception exception) when (IsExpectedExportException(exception))
        {
            ReportOpenExportFailure(openFolder, exception.GetType().Name);
        }
    }

    private void ReportOpenExportFailure(bool openFolder, string reason)
    {
        ReportError(
            openFolder ? "Could not open export folder" : "Could not open exported file",
            "The exported item may have been moved or is no longer available.");
        _ = _logService.TryWriteAsync($"Open export result failed: {reason}");
    }

    private void ReportExportFailure(Exception exception)
    {
        var message = exception switch
        {
            UnauthorizedAccessException => $"{AppInfo.ProductName} could not write to that location. Choose another folder and try again.",
            IOException => "The output file could not be created. Check the destination and available disk space, then try again.",
            ArgumentException or NotSupportedException => "The output path is invalid. Choose another filename or folder.",
            _ => "Windows could not encode the project. Check the source files and try another output location."
        };
        ShowMessage(InfoBarSeverity.Error, "Export failed", message);
        _ = _logService.TryWriteAsync($"Export failed: {exception.GetType().Name}");
    }

    private static bool IsExpectedExportException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or COMException;

    private sealed record ExportDialogSelection(string FileName, ExportOptions Options);
}
