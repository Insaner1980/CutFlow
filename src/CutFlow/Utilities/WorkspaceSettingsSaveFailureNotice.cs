namespace CutFlow.Utilities;

internal sealed class WorkspaceSettingsSaveFailureNotice
{
    private bool _failureVisible;

    public bool Observe(DebouncedSaveState state)
    {
        if (state == DebouncedSaveState.Saved)
        {
            _failureVisible = false;
            return false;
        }

        if (state != DebouncedSaveState.SaveFailed || _failureVisible)
        {
            return false;
        }

        _failureVisible = true;
        return true;
    }
}
