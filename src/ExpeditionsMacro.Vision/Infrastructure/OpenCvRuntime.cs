using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Infrastructure;

public static class OpenCvRuntime
{
    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;
        Cv2.SetNumThreads(1);
        Cv2.SetUseOptimized(true);
    }
}
