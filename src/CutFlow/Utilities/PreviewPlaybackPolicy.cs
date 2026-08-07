namespace CutFlow.Utilities;

public static class PreviewPlaybackPolicy
{
    public static bool ShouldRestartAtEnd(bool loopEnabled) => loopEnabled;
}
