namespace CutFlow.Utilities;

internal sealed class EventGenerationGate
{
    private long _generation;

    public long Current => Interlocked.Read(ref _generation);

    public long Advance() => Interlocked.Increment(ref _generation);

    public bool IsCurrent(long generation) => generation == Current;

    public bool TryRun(long generation, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!IsCurrent(generation))
        {
            return false;
        }

        callback();
        return true;
    }
}
