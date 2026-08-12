using Windows.Storage;
using Windows.Storage.Pickers;
using CutFlow.Models;
using ModernFileSavePicker = Microsoft.Windows.Storage.Pickers.FileSavePicker;
using ModernPickerLocationId = Microsoft.Windows.Storage.Pickers.PickerLocationId;

namespace CutFlow.Utilities;

public static class FilePickerHelper
{
    private static readonly string[] VisualExtensions = [".mp4", ".png", ".jpg", ".jpeg"];
    private static readonly string[] AudioExtensions = [".mp3", ".wav"];
    private static readonly string[] SupportedExtensions = [.. VisualExtensions, .. AudioExtensions];

    public static async Task<IReadOnlyList<StorageFile>> PickMediaFilesAsync(
        nint windowHandle,
        MediaImportScope scope = MediaImportScope.All,
        CancellationToken cancellationToken = default)
    {
        var location = scope == MediaImportScope.Audio ? PickerLocationId.MusicLibrary : PickerLocationId.VideosLibrary;
        var picker = CreateOpenPicker(windowHandle, location);
        foreach (var extension in GetMediaExtensions(scope))
        {
            picker.FileTypeFilter.Add(extension);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var files = await picker.PickMultipleFilesAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return files;
    }

    internal static IReadOnlyList<string> GetMediaExtensions(MediaImportScope scope) => scope switch
    {
        MediaImportScope.Visual => VisualExtensions,
        MediaImportScope.Audio => AudioExtensions,
        MediaImportScope.All => SupportedExtensions,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    public static async Task<StorageFile?> PickRelinkFileAsync(
        nint windowHandle,
        ProjectAssetKind kind,
        CancellationToken cancellationToken = default)
    {
        var location = kind == ProjectAssetKind.Audio ? PickerLocationId.MusicLibrary : PickerLocationId.VideosLibrary;
        var picker = CreateOpenPicker(windowHandle, location);
        foreach (var extension in kind switch
        {
            ProjectAssetKind.Video => new[] { ".mp4" },
            ProjectAssetKind.Image => new[] { ".png", ".jpg", ".jpeg" },
            ProjectAssetKind.Audio => new[] { ".mp3", ".wav" },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        })
        {
            picker.FileTypeFilter.Add(extension);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var file = await picker.PickSingleFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return file;
    }

    public static Task<string?> PickExportFileAsync(
        nint windowHandle,
        string suggestedFileName,
        CancellationToken cancellationToken = default) =>
        PickExportFileAsync(windowHandle, suggestedFileName, null, cancellationToken);

    public static async Task<string?> PickExportFileAsync(
        nint windowHandle,
        string suggestedFileName,
        string? suggestedFolder,
        CancellationToken cancellationToken = default)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A window handle is required.", nameof(windowHandle));
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var picker = new ModernFileSavePicker(windowId)
        {
            SuggestedStartLocation = ModernPickerLocationId.VideosLibrary,
            SuggestedFileName = ResolvePickerSuggestedFileName(suggestedFileName),
            DefaultFileExtension = ".mp4",
            ShowOverwritePrompt = true
        };
        if (ResolveSuggestedExportFolder(suggestedFolder) is { } folder)
        {
            picker.SuggestedFolder = folder;
        }
        picker.FileTypeChoices.Add("MP4 video", new List<string> { ".mp4" });

        cancellationToken.ThrowIfCancellationRequested();
        var result = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return result?.Path;
    }

    internal static string? ResolveSuggestedExportFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var value = path.Trim();
            if (!Path.IsPathFullyQualified(value))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(value);
            return Directory.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    internal static string ResolvePickerSuggestedFileName(string suggestedFileName)
    {
        var value = suggestedFileName.Trim();
        return value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }

    private static FileOpenPicker CreateOpenPicker(nint windowHandle, PickerLocationId location)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A window handle is required.", nameof(windowHandle));
        }

        var picker = new FileOpenPicker { SuggestedStartLocation = location, ViewMode = PickerViewMode.Thumbnail };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        return picker;
    }
}

public enum MediaImportScope
{
    All,
    Visual,
    Audio
}

public static class MediaImportScopeExtensions
{
    public static bool Allows(this MediaImportScope scope, string path) =>
        scope.AllowsExtension(Path.GetExtension(path));

    internal static bool AllowsExtension(this MediaImportScope scope, string extension) =>
        FilePickerHelper.GetMediaExtensions(scope).Contains(extension, StringComparer.OrdinalIgnoreCase);

    internal static bool CanAcceptDrop(
        this MediaImportScope scope,
        bool hasStorageItems,
        IEnumerable<string> advertisedFileTypes)
    {
        if (!hasStorageItems)
        {
            return false;
        }

        var fileTypes = advertisedFileTypes.Where(type => !string.IsNullOrWhiteSpace(type)).ToList();
        return fileTypes.Count == 0 || fileTypes.Any(type => scope.AllowsExtension(type));
    }
}
