namespace CutFlow.Utilities;

public static class PreviewResourceCleanup
{
    public static void Run(
        Action pause,
        Action clearSource,
        Action detachElement,
        Action disposeSource,
        Action disposePlayer)
    {
        pause();
        clearSource();
        detachElement();
        disposeSource();
        disposePlayer();
    }
}
