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
        try
        {
            pause();
        }
        finally
        {
            try
            {
                clearSource();
            }
            finally
            {
                try
                {
                    detachElement();
                }
                finally
                {
                    try
                    {
                        disposeSource();
                    }
                    finally
                    {
                        disposePlayer();
                    }
                }
            }
        }
    }
}
