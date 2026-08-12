using System.Security.Cryptography;
using System.Text;
using Windows.Storage;

namespace CutFlow.Services;

public sealed class SimpleLogService
{
    private readonly string _logPath;
    private readonly int _maximumEntries;
    private readonly int _maximumMessageLength;
    private readonly string _processWriteLockName;
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
        var lockIdentity = SHA256.HashData(Encoding.UTF8.GetBytes(_logPath.ToUpperInvariant()));
        _processWriteLockName = $@"Local\CutFlow.SimpleLog.{Convert.ToHexString(lockIdentity)}";
    }

    public async Task WriteAsync(string technicalMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(technicalMessage);
        var normalizedMessage = technicalMessage.ReplaceLineEndings(" ");
        if (normalizedMessage.Length > _maximumMessageLength)
        {
            var truncatedLength = _maximumMessageLength;
            if (char.IsSurrogatePair(normalizedMessage, truncatedLength - 1))
            {
                truncatedLength--;
            }

            normalizedMessage = normalizedMessage[..truncatedLength];
        }

        await _writeLock.WaitAsync(cancellationToken);
        Semaphore? processWriteLock = null;
        var processWriteLockAcquired = false;
        try
        {
            processWriteLock = new Semaphore(1, 1, _processWriteLockName);
            await WaitForProcessWriteLockAsync(processWriteLock, cancellationToken);
            processWriteLockAcquired = true;
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            var entries = File.Exists(_logPath)
                ? (await File.ReadAllLinesAsync(_logPath, cancellationToken)).TakeLast(_maximumEntries - 1).ToList()
                : [];
            entries.Add($"{DateTimeOffset.UtcNow:O} {normalizedMessage}");
            await WriteAtomicallyAsync(entries, cancellationToken);
        }
        finally
        {
            try
            {
                if (processWriteLockAcquired)
                {
                    processWriteLock!.Release();
                }
            }
            finally
            {
                processWriteLock?.Dispose();
                _writeLock.Release();
            }
        }
    }

    private async Task WriteAtomicallyAsync(IEnumerable<string> entries, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{_logPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllLinesAsync(
                temporaryPath,
                entries,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_logPath))
            {
                File.Replace(temporaryPath, _logPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, _logPath);
            }
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed best-effort log write must not be replaced by a temporary-file cleanup failure.
            }
        }
    }

    private static async Task WaitForProcessWriteLockAsync(Semaphore processWriteLock, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await Task.Run(() => processWriteLock.WaitOne());
            return;
        }

        var signaledHandle = await Task.Run(
            () => WaitHandle.WaitAny([processWriteLock, cancellationToken.WaitHandle]));
        if (signaledHandle != 0)
        {
            throw new OperationCanceledException(cancellationToken);
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
