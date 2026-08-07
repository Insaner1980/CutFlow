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

            return MediaDropReadResult.Success(items.OfType<StorageFile>().ToList());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return MediaDropReadResult.Failure(
                $"{AppInfo.ProductName} could not read the dropped items. Try importing them with the file picker.",
                exception);
        }
    }
}

internal sealed record MediaDropReadResult(
    IReadOnlyList<StorageFile> Files,
    string? ErrorMessage,
    Exception? Exception)
{
    public bool IsSuccess => ErrorMessage is null;

    public static MediaDropReadResult Success(IReadOnlyList<StorageFile> files) => new(files, null, null);

    public static MediaDropReadResult Failure(string message, Exception exception) => new([], message, exception);
}
