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

        await ExecuteAsync(async token =>
        {
            var result = await prepareAsync(token);
            token.ThrowIfCancellationRequested();
            commit(result);
            return true;
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await operation(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
