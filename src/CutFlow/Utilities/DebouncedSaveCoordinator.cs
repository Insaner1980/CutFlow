namespace CutFlow.Utilities;

internal enum DebouncedSaveState
{
    Saved,
    Unsaved,
    Saving,
    SaveFailed
}

internal sealed partial class DebouncedSaveCoordinator : IDisposable
{
    private readonly Func<Task> _saveAsync;
    private readonly Func<CancellationToken, Task> _delayAsync;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _debounceCancellation;
    private long _latestRevision;
    private long _savedRevision;
    private bool _disposed;
    private DebouncedSaveState _state = DebouncedSaveState.Saved;

    public DebouncedSaveCoordinator(
        Func<Task> saveAsync,
        Func<CancellationToken, Task>? delayAsync = null,
        TimeSpan? debounceDelay = null)
    {
        _saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
        var delay = debounceDelay ?? TimeSpan.FromSeconds(1);
        _delayAsync = delayAsync ?? (cancellationToken => Task.Delay(delay, cancellationToken));
    }

    public event EventHandler? StateChanged;

    public DebouncedSaveState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public void NotifyEdited()
    {
        CancellationTokenSource cancellation;
        CancellationTokenSource? previous;
        lock (_sync)
        {
            ThrowIfDisposed();
            _latestRevision++;
            previous = _debounceCancellation;
            cancellation = _debounceCancellation = new CancellationTokenSource();
            SetStateLocked(DebouncedSaveState.Unsaved);
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = DebounceAndSaveAsync(cancellation);
    }

    public async Task FlushAsync()
    {
        CancellationTokenSource? pending;
        lock (_sync)
        {
            ThrowIfDisposed();
            pending = _debounceCancellation;
            _debounceCancellation = null;
        }

        if (pending is not null)
        {
            await pending.CancelAsync();
            pending.Dispose();
        }
        await SaveLatestAsync();
    }

    public void Dispose()
    {
        CancellationTokenSource? pending;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            pending = _debounceCancellation;
            _debounceCancellation = null;
        }

        pending?.Cancel();
        pending?.Dispose();
    }

    private async Task DebounceAndSaveAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await _delayAsync(cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                await SaveLatestAsync();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer edit replaced this pending debounce operation.
        }
        catch
        {
            // The state is set by SaveLatestAsync. Debounced saves are surfaced in the editor rather than faulting unobserved tasks.
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_debounceCancellation, cancellation))
                {
                    _debounceCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private async Task SaveLatestAsync()
    {
        await _saveGate.WaitAsync(CancellationToken.None);
        try
        {
            while (true)
            {
                long revision;
                lock (_sync)
                {
                    if (_disposed || _savedRevision >= _latestRevision)
                    {
                        return;
                    }

                    revision = _latestRevision;
                    SetStateLocked(DebouncedSaveState.Saving);
                }

                try
                {
                    await _saveAsync();
                }
                catch
                {
                    lock (_sync)
                    {
                        if (!_disposed)
                        {
                            SetStateLocked(DebouncedSaveState.SaveFailed);
                        }
                    }

                    throw;
                }

                lock (_sync)
                {
                    _savedRevision = Math.Max(_savedRevision, revision);
                    if (_savedRevision >= _latestRevision)
                    {
                        SetStateLocked(DebouncedSaveState.Saved);
                        return;
                    }
                }
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void SetStateLocked(DebouncedSaveState value)
    {
        if (_state == value || _disposed)
        {
            return;
        }

        _state = value;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
