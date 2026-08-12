using CutFlow.Services;
using Windows.Storage;

namespace CutFlow.Utilities;

internal static class MediaDropReader
{
    public static async Task<MediaDropReadResult> ReadAsync(
        Func<Task<IReadOnlyList<IStorageItem>>> readItemsAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readItemsAsync);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = await readItemsAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (items is null)
            {
                throw new InvalidDataException("The drop operation returned no item collection.");
            }

            var files = new List<StorageFile>(items.Count);
            var rejectedItems = new List<MediaDropItemFailure>();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    files.Add(file);
                }
                else
                {
                    rejectedItems.Add(new MediaDropItemFailure(
                        item?.Name ?? "Unknown item",
                        "Only local media files can be imported; folders and virtual shell items are not supported."));
                }
            }

            return MediaDropReadResult.Success(files, rejectedItems);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (MediaImportService.IsExpectedMediaFailure(exception))
        {
            return MediaDropReadResult.Failure(
                $"{AppInfo.ProductName} could not read the dropped items. Try importing them with the file picker.",
                exception);
        }
    }
}

internal sealed record MediaDropReadResult(
    IReadOnlyList<StorageFile> Files,
    IReadOnlyList<MediaDropItemFailure> RejectedItems,
    string? ErrorMessage,
    Exception? Exception)
{
    public bool IsSuccess => ErrorMessage is null;

    public static MediaDropReadResult Success(
        IReadOnlyList<StorageFile> files,
        IReadOnlyList<MediaDropItemFailure> rejectedItems) => new(files, rejectedItems, null, null);

    public static MediaDropReadResult Failure(string message, Exception exception) => new([], [], message, exception);
}

internal sealed record MediaDropItemFailure(string ItemName, string Message);
