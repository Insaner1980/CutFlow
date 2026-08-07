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

namespace CutFlow.Controls;

public sealed partial class MediaPanel : UserControl
{
    private readonly ObservableCollection<MediaAssetCard> _cards = [];
    private ProjectDocument? _project;
    private ThumbnailService? _thumbnailService;
    private EditorTool _tool = EditorTool.Media;
    private CancellationTokenSource? _lifetimeCts;
    private CancellationTokenSource? _thumbnailRefreshCts;

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
        if (_project is null)
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
        EmptyTitle.Text = anyForTool && query.Length > 0
            ? "No matching assets"
            : _tool == EditorTool.Audio ? "No audio imported" : "No media imported";
        EmptyDescription.Text = anyForTool && query.Length > 0
            ? "Try a different filename."
            : "Import files or drop them here.";
    }

    public void SetTool(EditorTool tool)
    {
        _tool = tool;
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
        UnsupportedContent.Visibility = tool is EditorTool.Media or EditorTool.Audio or EditorTool.Text
            ? Visibility.Collapsed
            : Visibility.Visible;
        UnsupportedName.Text = ToolName(tool);
        ImportButtonText.Text = tool == EditorTool.Audio ? "Import audio" : "Import media";
        ImportIcon.Glyph = tool == EditorTool.Audio ? "\uE8D6" : "\uE8B7";
        SearchBox.PlaceholderText = tool == EditorTool.Audio ? "Search audio" : "Search assets";
        RefreshAssets();
    }

    private async Task LoadThumbnailSafelyAsync(ProjectAsset asset, MediaAssetCard card, CancellationToken cancellationToken)
    {
        try
        {
            if (_thumbnailService is null)
            {
                return;
            }

            var request = ThumbnailService.CaptureRequest(asset, ThumbnailService.DefaultRequestedSize);
            var source = await StorageFile.GetFileFromPathAsync(request.SourcePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ThumbnailService.IsCurrentRequest(asset, request) || !_cards.Contains(card))
            {
                return;
            }

            var cachePath = await _thumbnailService.GetOrCreateThumbnailAsync(source, asset, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (cachePath is null || !ThumbnailService.IsCurrentRequest(asset, request) || !_cards.Contains(card))
            {
                return;
            }

            var cachedFile = await StorageFile.GetFileFromPathAsync(cachePath);
            using var stream = await cachedFile.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            if (_cards.Contains(card) && ThumbnailService.IsCurrentRequest(asset, request))
            {
                card.Thumbnail = bitmap;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            if (!cancellationToken.IsCancellationRequested && _cards.Contains(card))
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

    private void MediaPanel_Loaded(object sender, RoutedEventArgs e)
    {
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = new CancellationTokenSource();
        RefreshAssets();
    }

    private void MediaPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        _thumbnailRefreshCts?.Cancel();
        _thumbnailRefreshCts?.Dispose();
        _thumbnailRefreshCts = null;
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = null;
    }

    private void Import_Click(object sender, RoutedEventArgs e) =>
        ImportRequested?.Invoke(this, new MediaImportRequestedEventArgs(CurrentImportScope));

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && Enum.TryParse<TextPreset>(element.Tag?.ToString(), out var preset))
        {
            AddTextRequested?.Invoke(this, new TextPresetRequestedEventArgs(preset));
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => RefreshAssets();

    private void AssetGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not MediaAssetCard card || card.ThumbnailRequested || _project is null)
        {
            return;
        }

        var asset = _project.Assets.FirstOrDefault(candidate => candidate.Id == card.AssetId);
        if (asset is null || asset.Kind == ProjectAssetKind.Audio || asset.IsMissing || _thumbnailService is null)
        {
            return;
        }

        card.ThumbnailRequested = true;
        _ = LoadThumbnailSafelyAsync(asset, card, _thumbnailRefreshCts?.Token ?? CancellationToken.None);
    }

    private void AssetGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FindCard(e.OriginalSource as DependencyObject) is { } card && card.CanAdd)
        {
            AddAssetRequested?.Invoke(this, new AssetActionEventArgs(card.AssetId));
        }
    }

    private void AssetGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var card = FindCard(e.OriginalSource as DependencyObject) ??
            FindCard(FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);
        if (card is null || !MediaAssetActivationPolicy.ShouldActivate(e.Key, card.CanAdd))
        {
            return;
        }

        AddAssetRequested?.Invoke(this, new AssetActionEventArgs(card.AssetId));
        e.Handled = true;
    }

    private void AssetCard_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MediaAssetCard { CanAdd: true } card })
        {
            e.Cancel = true;
            return;
        }

        e.Data.SetData(MediaAssetDragPayload.FormatId, MediaAssetDragPayload.Create(card.AssetId));
        e.Data.RequestedOperation = DataPackageOperation.Copy;
        e.Data.Properties.Title = card.FileName;
    }

    private void AddToTrack_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, AddAssetRequested);

    private void Relink_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, RelinkAssetRequested);

    private void ShowInExplorer_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, ShowAssetInExplorerRequested);

    private void Remove_Click(object sender, RoutedEventArgs e) => RaiseAssetAction(sender, RemoveAssetRequested);

    private static void RaiseAssetAction(object sender, EventHandler<AssetActionEventArgs>? handler)
    {
        if (sender is FrameworkElement { Tag: Guid assetId })
        {
            handler?.Invoke(sender, new AssetActionEventArgs(assetId));
        }
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
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Import into this project";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private async void PanelRoot_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
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
            if (!result.IsSuccess)
            {
                MediaDropFailed?.Invoke(this, new MediaDropFailedEventArgs(result.ErrorMessage!, result.Exception));
            }
            else if (result.Files.Count > 0)
            {
                var scope = CurrentImportScope;
                var acceptedFiles = result.Files.Where(file => scope.Allows(file.Path)).ToList();
                if (acceptedFiles.Count != result.Files.Count)
                {
                    MediaDropFailed?.Invoke(
                        this,
                        new MediaDropFailedEventArgs(
                            scope == MediaImportScope.Audio
                                ? "Only MP3 and WAV files can be dropped into the Audio panel."
                                : "Only MP4, PNG, and JPEG files can be dropped into the Media panel.",
                            null));
                }

                if (acceptedFiles.Count > 0)
                {
                    MediaFilesDropped?.Invoke(this, new MediaFilesDroppedEventArgs(acceptedFiles));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
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

    private MediaImportScope CurrentImportScope =>
        _tool == EditorTool.Audio ? MediaImportScope.Audio : MediaImportScope.Visual;

    private void RestartThumbnailRefresh()
    {
        _thumbnailRefreshCts?.Cancel();
        _thumbnailRefreshCts?.Dispose();
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

public sealed class MediaAssetCard : INotifyPropertyChanged
{
    private BitmapImage? _thumbnail;

    public MediaAssetCard(ProjectAsset asset)
    {
        AssetId = asset.Id;
        FileName = string.IsNullOrWhiteSpace(asset.FileName) ? Path.GetFileName(asset.SourcePath) : asset.FileName;
        FullPath = asset.SourcePath;
        TypeText = asset.Kind.ToString();
        DurationText = asset.Kind == ProjectAssetKind.Image
            ? TimecodeFormatter.Format(asset.DurationMilliseconds, 30)
            : TimecodeFormatter.Format(asset.DurationMilliseconds, 30);
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AssetActionEventArgs(Guid assetId) : EventArgs
{
    public Guid AssetId { get; } = assetId;
}

public sealed class MediaFilesDroppedEventArgs(IReadOnlyList<StorageFile> files) : EventArgs
{
    public IReadOnlyList<StorageFile> Files { get; } = files;
}

public sealed class MediaDropFailedEventArgs(string message, Exception? exception) : EventArgs
{
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}
