using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CutFlow.Models;
using CutFlow.Services;
using CutFlow.Utilities;
using CutFlow.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace CutFlow.Controls;

public sealed partial class MediaPanel : UserControl, IDisposable
{
    private readonly ObservableCollection<MediaAssetCard> _cards = [];
    private ProjectDocument? _project;
    private ThumbnailService? _thumbnailService;
    private EditorTool _tool = EditorTool.Media;
    private CancellationTokenSource? _lifetimeCts;
    private CancellationTokenSource? _thumbnailRefreshCts;
    private bool _disposed;

    public MediaPanel()
    {
        InitializeComponent();
        AssetGrid.ItemsSource = _cards;
    }

    public event EventHandler<MediaImportRequestedEventArgs>? ImportRequested;
    public event EventHandler<TextPresetRequestedEventArgs>? AddTextRequested;
    public event EventHandler<AssetActionEventArgs>? AddAssetRequested;
    public event EventHandler<AssetActionEventArgs>? RelinkAssetRequested;
    public event EventHandler<AssetActionEventArgs>? ShowAssetInExplorerRequested;
    public event EventHandler<AssetActionEventArgs>? RemoveAssetRequested;
    public event EventHandler<MediaFilesDroppedEventArgs>? MediaFilesDropped;
    public event EventHandler<MediaDropFailedEventArgs>? MediaDropFailed;

    public void SetProject(ProjectDocument project, ThumbnailService thumbnailService)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        RefreshAssets();
    }

    public void RefreshAssets()
    {
        if (_disposed || _project is null)
        {
            return;
        }

        RestartThumbnailRefresh();

        var query = SearchBox.Text.Trim();
        var kinds = _tool == EditorTool.Audio
            ? new[] { ProjectAssetKind.Audio }
            : new[] { ProjectAssetKind.Video, ProjectAssetKind.Image };
        var assets = _project.Assets
            .Where(asset => kinds.Contains(asset.Kind))
            .Where(asset => query.Length == 0 || AssetFileName(asset).Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(asset => AssetFileName(asset), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _cards.Clear();
        foreach (var asset in assets)
        {
            var card = new MediaAssetCard(asset);
            _cards.Add(card);
        }

        AssetEmptyState.Visibility = assets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var anyForTool = _project.Assets.Any(asset => kinds.Contains(asset.Kind));
        if (anyForTool && query.Length > 0)
        {
            EmptyTitle.Text = "No matching assets";
        }
        else
        {
            EmptyTitle.Text = _tool == EditorTool.Audio ? "No audio imported" : "No media imported";
        }
        EmptyDescription.Text = anyForTool && query.Length > 0
            ? "Try a different filename."
            : "Import files or drop them here.";
    }

    public void SetTool(EditorTool tool)
    {
        _tool = tool;
        var acceptsMediaInput = TryGetMediaImportScope(tool, out _);
        PanelTitle.Text = ToolName(tool);
        PanelDescription.Text = tool switch
        {
            EditorTool.Media => "Project videos and images",
            EditorTool.Audio => "Project audio",
            EditorTool.Text => "Text items and presets",
            _ => "Creative category"
        };
        AssetContent.Visibility = tool is EditorTool.Media or EditorTool.Audio ? Visibility.Visible : Visibility.Collapsed;
        TextContent.Visibility = tool == EditorTool.Text ? Visibility.Visible : Visibility.Collapsed;
        UnsupportedContent.Visibility = acceptsMediaInput || tool == EditorTool.Text
            ? Visibility.Collapsed
            : Visibility.Visible;
        PanelRoot.AllowDrop = acceptsMediaInput;
        UnsupportedName.Text = ToolName(tool);
        ImportButtonText.Text = tool == EditorTool.Audio ? "Import audio" : "Import media";
        ImportIcon.Glyph = tool == EditorTool.Audio ? "\uE8D6" : "\uE8B7";
        SearchBox.PlaceholderText = tool == EditorTool.Audio ? "Search audio" : "Search assets";
        RefreshAssets();
    }

    private async Task LoadThumbnailSafelyAsync(
        ProjectAsset asset,
        MediaAssetCard card,
        long cardGeneration,
        CancellationToken cancellationToken)
    {
        ThumbnailRequest? request = null;
        try
        {
            if (_thumbnailService is null)
            {
                return;
            }

            request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
            var source = await StorageFile.GetFileFromPathAsync(request.Value.SourcePath).AsTask(cancellationToken);
            if (!ThumbnailService.IsCurrentRequest(asset, request.Value) ||
                !_cards.Contains(card) ||
                !card.IsCurrentThumbnailRequest(cardGeneration))
            {
                return;
            }

            var cachePath = await _thumbnailService.GetOrCreateThumbnailAsync(source, asset, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (cachePath is null || !_cards.Contains(card) || !card.IsCurrentThumbnailRequest(cardGeneration))
            {
                return;
            }

            string relativePath;
            lock (asset)
            {
                request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
                relativePath = asset.ThumbnailCachePath;
            }
            if (!ThumbnailService.IsCachePathForRequest(request.Value, relativePath))
            {
                return;
            }

            var cachedFile = await StorageFile.GetFileFromPathAsync(cachePath).AsTask(cancellationToken);
            using var stream = await cachedFile.OpenReadAsync().AsTask(cancellationToken);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream).AsTask(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_cards.Contains(card) &&
                card.IsCurrentThumbnailRequest(cardGeneration) &&
                ThumbnailService.IsCurrentRequest(asset, request.Value) &&
                string.Equals(asset.ThumbnailCachePath, relativePath, StringComparison.Ordinal))
            {
                card.Thumbnail = bitmap;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer thumbnail request superseded this load.
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            if (!cancellationToken.IsCancellationRequested &&
                _cards.Contains(card) &&
                card.IsCurrentThumbnailRequest(cardGeneration) &&
                request is { } failedRequest &&
                ThumbnailService.IsCurrentRequest(asset, failedRequest))
            {
                card.UseFallback();
            }
        }
    }

    private static string AssetFileName(ProjectAsset asset) =>
        string.IsNullOrWhiteSpace(asset.FileName) ? Path.GetFileName(asset.SourcePath) : asset.FileName;

    private static string ToolName(EditorTool tool) => tool switch
    {
        EditorTool.Media => "Media",
        EditorTool.Audio => "Audio",
        EditorTool.Text => "Text",
        EditorTool.Stickers => "Stickers",
        EditorTool.Effects => "Effects",
        EditorTool.Transitions => "Transitions",
        EditorTool.Captions => "Captions",
        EditorTool.Filters => "Filters",
        EditorTool.Adjustment => "Adjustment",
        _ => tool.ToString()
    };

    internal static bool TryGetMediaImportScope(EditorTool tool, out MediaImportScope scope)
    {
        switch (tool)
        {
            case EditorTool.Media:
            case EditorTool.Text:
                scope = MediaImportScope.Visual;
                return true;
            case EditorTool.Audio:
                scope = MediaImportScope.Audio;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    internal static bool CanUseAssetKind(EditorTool tool, ProjectAssetKind kind) => tool switch
    {
        EditorTool.Media or EditorTool.Text => kind is ProjectAssetKind.Video or ProjectAssetKind.Image,
        EditorTool.Audio => kind == ProjectAssetKind.Audio,
        _ => false
    };

    private void MediaPanel_Loaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = new CancellationTokenSource();
        RefreshAssets();
    }

    private void MediaPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        StopThumbnailWork();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopThumbnailWork();
    }

    private void StopThumbnailWork()
    {
        _thumbnailRefreshCts?.Cancel();
        _thumbnailRefreshCts?.Dispose();
        _thumbnailRefreshCts = null;
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = null;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetMediaImportScope(_tool, out var scope))
        {
            ImportRequested?.Invoke(this, new MediaImportRequestedEventArgs(scope));
        }
    }

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        if (_tool == EditorTool.Text && sender is FrameworkElement element && Enum.TryParse<TextPreset>(element.Tag?.ToString(), out var preset))
        {
            AddTextRequested?.Invoke(this, new TextPresetRequestedEventArgs(preset));
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => RefreshAssets();

    private void AssetGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not MediaAssetCard card)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            card.CancelThumbnailRequest();
            return;
        }

        if (card.ThumbnailRequested || _project is null)
        {
            return;
        }

        var asset = _project.Assets.FirstOrDefault(candidate => candidate.Id == card.AssetId);
        if (asset is null || asset.Kind == ProjectAssetKind.Audio || asset.IsMissing || _thumbnailService is null)
        {
            return;
        }

        var work = card.BeginThumbnailRequest(_thumbnailRefreshCts?.Token ?? CancellationToken.None);
        _ = LoadThumbnailSafelyAsync(asset, card, work.Generation, work.Token);
    }

    private void AssetGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindCard(e.OriginalSource as DependencyObject) is { } card && card.CanAdd && CanUseAsset(card.AssetId))
        {
            AddAssetRequested?.Invoke(this, new AssetActionEventArgs(card.AssetId));
        }
    }

    private void AssetGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var card = FindCard(e.OriginalSource as DependencyObject) ??
            FindCard(FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);
        if (card is null || !CanUseAsset(card.AssetId) || e.Key is not VirtualKey.Enter and not VirtualKey.Space)
        {
            return;
        }

        e.Handled = true;
        if (e.KeyStatus.WasKeyDown)
        {
            return;
        }

        if (MediaAssetActivationPolicy.ShouldActivate(e.Key, card.CanAdd))
        {
            AddAssetRequested?.Invoke(this, new AssetActionEventArgs(card.AssetId));
        }
    }

    private void AssetCard_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MediaAssetCard { CanAdd: true } card } || !CanUseAsset(card.AssetId))
        {
            e.Cancel = true;
            return;
        }

        var asset = _project!.Assets.First(candidate => candidate.Id == card.AssetId);
        MediaAssetDragPayload.Set(e.Data, asset);
        e.Data.RequestedOperation = DataPackageOperation.Copy;
        e.Data.Properties.Title = card.FileName;
    }

    private void AddToTrack_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, AddAssetRequested);

    private void Relink_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, RelinkAssetRequested);

    private void ShowInExplorer_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, ShowAssetInExplorerRequested);

    private void Remove_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, RemoveAssetRequested);

    private void RaiseAssetAction(object sender, EventHandler<AssetActionEventArgs>? handler)
    {
        if (sender is FrameworkElement { Tag: Guid assetId } && CanUseAsset(assetId))
        {
            handler?.Invoke(sender, new AssetActionEventArgs(assetId));
        }
    }

    private bool CanUseAsset(Guid assetId)
    {
        var asset = _project?.Assets.FirstOrDefault(candidate => candidate.Id == assetId);
        return asset is not null && CanUseAssetKind(_tool, asset.Kind);
    }

    private static MediaAssetCard? FindCard(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: MediaAssetCard card })
            {
                return card;
            }

            source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void PanelRoot_DragOver(object sender, DragEventArgs e)
    {
        if (TryGetMediaImportScope(_tool, out var scope) &&
            scope.CanAcceptDrop(
                e.DataView.Contains(StandardDataFormats.StorageItems),
                e.DataView.Properties.FileTypes))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Import into this project";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void PanelRoot_Drop(object sender, DragEventArgs e)
    {
        var tool = _tool;
        if (!TryGetMediaImportScope(tool, out var scope) || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var cancellationToken = _lifetimeCts?.Token ?? CancellationToken.None;
        try
        {
            var result = await MediaDropReader.ReadAsync(
                async () => await e.DataView.GetStorageItemsAsync(),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_tool != tool)
            {
                return;
            }

            PublishDropResult(scope, result, e);
        }
        catch (OperationCanceledException)
        {
            // The panel lifetime ended while the drop was being read.
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                MediaDropFailed?.Invoke(
                    this,
                    new MediaDropFailedEventArgs(
                        $"{AppInfo.ProductName} could not read the dropped items. Try importing them with the file picker.",
                        exception));
            }
        }
    }

    private void PublishDropResult(MediaImportScope scope, MediaDropReadResult result, DragEventArgs e)
    {
        if (!result.IsSuccess)
        {
            MediaDropFailed?.Invoke(this, new MediaDropFailedEventArgs(result.ErrorMessage!, result.Exception));
            return;
        }

        if (result.Files.Count == 0 && result.RejectedItems.Count == 0)
        {
            return;
        }

        var (acceptedFiles, resultSlots) = ClassifyDroppedFiles(scope, result);
        MediaFilesDropped?.Invoke(this, new MediaFilesDroppedEventArgs(acceptedFiles, resultSlots));
        e.AcceptedOperation = acceptedFiles.Count > 0
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private static (List<StorageFile> AcceptedFiles, List<ImportResult?> ResultSlots) ClassifyDroppedFiles(
        MediaImportScope scope,
        MediaDropReadResult result)
    {
        var acceptedFiles = new List<StorageFile>(result.Files.Count);
        var resultSlots = new List<ImportResult?>(result.Files.Count + result.RejectedItems.Count);
        foreach (var file in result.Files)
        {
            if (!MediaImportService.TryResolveLocalSourcePath(file.Path, out var normalizedPath, out var pathError))
            {
                resultSlots.Add(ImportResult.Failure(file.Name, pathError!));
            }
            else if (!scope.Allows(normalizedPath))
            {
                resultSlots.Add(ImportResult.Failure(file.Name, UnsupportedDropMessage(scope)));
            }
            else
            {
                acceptedFiles.Add(file);
                resultSlots.Add(null);
            }
        }

        resultSlots.AddRange(result.RejectedItems.Select(
            item => ImportResult.Failure(item.ItemName, item.Message)));
        return (acceptedFiles, resultSlots);
    }

    private static string UnsupportedDropMessage(MediaImportScope scope) => scope == MediaImportScope.Audio
        ? "Only MP3 and WAV files can be dropped into the Audio panel."
        : "Only MP4, PNG, and JPEG files can be dropped into the Media panel.";

    private void RestartThumbnailRefresh()
    {
        _thumbnailRefreshCts?.Cancel();
        _thumbnailRefreshCts?.Dispose();
        if (_disposed)
        {
            _thumbnailRefreshCts = null;
            return;
        }

        _thumbnailRefreshCts = _lifetimeCts is null
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
    }
}

public sealed class TextPresetRequestedEventArgs(TextPreset preset) : EventArgs
{
    public TextPreset Preset { get; } = preset;
}

public sealed class MediaImportRequestedEventArgs(MediaImportScope scope) : EventArgs
{
    public MediaImportScope Scope { get; } = scope;
}

public sealed partial class MediaAssetCard : INotifyPropertyChanged
{
    private BitmapImage? _thumbnail;
    private CancellationTokenSource? _thumbnailCts;
    private long _thumbnailGeneration;

    public MediaAssetCard(ProjectAsset asset)
    {
        AssetId = asset.Id;
        FileName = string.IsNullOrWhiteSpace(asset.FileName) ? Path.GetFileName(asset.SourcePath) : asset.FileName;
        FullPath = asset.SourcePath;
        TypeText = asset.Kind.ToString();
        DurationText = TimecodeFormatter.Format(asset.DurationMilliseconds, 30);
        DimensionsText = asset.Width > 0 && asset.Height > 0 ? $"{asset.Width} × {asset.Height}" : "—";
        MissingVisibility = asset.IsMissing ? Visibility.Visible : Visibility.Collapsed;
        FallbackGlyph = asset.Kind == ProjectAssetKind.Audio ? "\uE8D6" : "\uE7C5";
        FallbackVisibility = Visibility.Visible;
        CanAdd = !asset.IsMissing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid AssetId { get; }
    public string FileName { get; }
    public string FullPath { get; }
    public string TypeText { get; }
    public string DurationText { get; }
    public string DimensionsText { get; }
    public Visibility MissingVisibility { get; }
    public string FallbackGlyph { get; }
    public bool CanAdd { get; }
    public bool ThumbnailRequested { get; set; }

    internal MediaAssetCardThumbnailRequest BeginThumbnailRequest(CancellationToken lifetimeToken)
    {
        StopThumbnailRequest();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        _thumbnailCts = cancellation;
        ThumbnailRequested = true;
        return new MediaAssetCardThumbnailRequest(++_thumbnailGeneration, cancellation.Token);
    }

    internal void CancelThumbnailRequest()
    {
        StopThumbnailRequest();
        ThumbnailRequested = false;
        _thumbnailGeneration++;
    }

    internal bool IsCurrentThumbnailRequest(long generation) =>
        ThumbnailRequested &&
        generation == _thumbnailGeneration &&
        _thumbnailCts is { IsCancellationRequested: false };

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            FallbackVisibility = value is null ? Visibility.Visible : Visibility.Collapsed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FallbackVisibility));
        }
    }

    public Visibility FallbackVisibility { get; private set; }

    public void UseFallback() => Thumbnail = null;

    private void StopThumbnailRequest()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal readonly record struct MediaAssetCardThumbnailRequest(long Generation, CancellationToken Token);

public sealed class AssetActionEventArgs(Guid assetId) : EventArgs
{
    public Guid AssetId { get; } = assetId;
}

public sealed class MediaFilesDroppedEventArgs(
    IReadOnlyList<StorageFile> files,
    IReadOnlyList<ImportResult?> resultSlots) : EventArgs
{
    public IReadOnlyList<StorageFile> Files { get; } = files;
    public IReadOnlyList<ImportResult?> ResultSlots { get; } = resultSlots;
}

public sealed class MediaDropFailedEventArgs(string message, Exception? exception) : EventArgs
{
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}
