namespace CutFlow.Utilities;

internal sealed class EditorImportGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> prepareAsync,
        Action<T> commit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepareAsync);
        ArgumentNullException.ThrowIfNull(commit);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await prepareAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            commit(result);
        }
        finally
        {
            _gate.Release();
        }
    }
}
