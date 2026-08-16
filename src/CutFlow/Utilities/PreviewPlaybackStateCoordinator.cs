namespace CutFlow.Utilities;

internal sealed class PreviewPlaybackStateCoordinator
{
    private bool _playIntent;
    private bool? _lastReportedActualState;

    public bool PlayIntent => _playIntent;

    public bool ToggleIntent()
    {
        _playIntent = !_playIntent;
        return _playIntent;
    }

    public void SetIntent(bool playIntent) => _playIntent = playIntent;

    public bool ReportActual(bool isPlaying, Action<bool> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (_playIntent != isPlaying || _lastReportedActualState == isPlaying)
        {
            return false;
        }

        _lastReportedActualState = isPlaying;
        report(isPlaying);
        return true;
    }
}
