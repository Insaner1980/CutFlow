using System.Text;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class SimpleLogService
{
    private readonly string _logPath;
    private readonly int _maximumEntries;
    private readonly int _maximumMessageLength;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SimpleLogService(string? rootPath = null, int maximumEntries = 200, int maximumMessageLength = 1024)
    {
        if (maximumEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        if (maximumMessageLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumMessageLength));
        }

        var localRootPath = rootPath ?? ApplicationData.Current.LocalFolder.Path;
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            throw new ArgumentException("A local log root path is required.", nameof(rootPath));
        }

        _logPath = Path.Combine(Path.GetFullPath(localRootPath), "cutflow.log");
        _maximumEntries = maximumEntries;
        _maximumMessageLength = maximumMessageLength;
    }

    public async Task WriteAsync(string technicalMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(technicalMessage);
        var normalizedMessage = technicalMessage.ReplaceLineEndings(" ");
        if (normalizedMessage.Length > _maximumMessageLength)
        {
            normalizedMessage = normalizedMessage[.._maximumMessageLength];
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            var entries = File.Exists(_logPath)
                ? (await File.ReadAllLinesAsync(_logPath, cancellationToken)).TakeLast(_maximumEntries - 1).ToList()
                : [];
            entries.Add($"{DateTimeOffset.UtcNow:O} {normalizedMessage}");
            await File.WriteAllLinesAsync(_logPath, entries, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task TryWriteAsync(string technicalMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteAsync(technicalMessage, cancellationToken);
        }
        catch (Exception)
        {
            // Technical logging must never interfere with the user flow it observes.
        }
    }
}
