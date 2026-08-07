namespace CutFlow.Services;

public sealed class PreviewRebuildGate : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _current;
    private long _version;
    private bool _disposed;

    public PreviewRebuildLease Begin(CancellationToken lifetimeToken = default)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _current?.Cancel();
            _current?.Dispose();
            _current = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
            return new PreviewRebuildLease(this, ++_version, _current.Token);
        }
    }

    internal bool IsCurrent(long version)
    {
        lock (_sync)
        {
            return !_disposed && version == _version && _current is { IsCancellationRequested: false };
        }
    }

    internal bool TryCommit(long version, Action commit)
    {
        ArgumentNullException.ThrowIfNull(commit);
        lock (_sync)
        {
            if (_disposed || version != _version || _current is not { IsCancellationRequested: false })
            {
                return false;
            }

            commit();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _version++;
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }
}

public sealed class PreviewRebuildLease : IDisposable
{
    private readonly PreviewRebuildGate _owner;

    internal PreviewRebuildLease(PreviewRebuildGate owner, long version, CancellationToken token)
    {
        _owner = owner;
        Version = version;
        Token = token;
    }

    public long Version { get; }
    public CancellationToken Token { get; }
    public bool IsCurrent => _owner.IsCurrent(Version);
    public bool TryCommit(Action commit) => _owner.TryCommit(Version, commit);
    public void Dispose() { }
}
