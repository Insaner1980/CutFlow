namespace CutFlow.Utilities;

internal sealed class HomeProjectOpenGate
{
    private int _active;

    public bool IsActive => Volatile.Read(ref _active) != 0;

    public async Task<bool> RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            await action();
            return true;
        }
        finally
        {
            Volatile.Write(ref _active, 0);
        }
    }
}
