using OpenCvSharp;

namespace ExpeditionsMacro.Vision.Infrastructure;

public static class OpenCvRuntime
{
    private static readonly object Gate = new();
    private static int _initialized;

    public static void Initialize()
    {
        if (Volatile.Read(ref _initialized) != 0) return;
        lock (Gate)
        {
            if (_initialized != 0) return;
            try
            {
                // Force native loading before the optional tuning calls. This keeps
                // missing native dependencies from surfacing as an opaque type
                // initializer exception on VisionScorer or ImageCodec.
                _ = Cv2.GetVersionString();
                TryOptionalConfiguration(() => Cv2.SetNumThreads(1));
                TryOptionalConfiguration(() => Cv2.SetUseOptimized(true));
                Volatile.Write(ref _initialized, 1);
            }
            catch (Exception error)
            {
                Exception root = RootCause(error);
                throw new InvalidOperationException(
                    "Computer vision could not start. Reinstall the latest Expeditions Macro release. " +
                    "If this is an older or portable build, install the Microsoft Visual C++ 2015-2022 x64 Redistributable. " +
                    $"Details: {root.Message}",
                    error);
            }
        }
    }

    private static void TryOptionalConfiguration(Action configure)
    {
        try
        {
            configure();
        }
        catch (EntryPointNotFoundException)
        {
            // An older compatible native binding may not expose a tuning entry point.
            // Core image operations remain usable, so retain OpenCV defaults.
        }
    }

    private static Exception RootCause(Exception error)
    {
        Exception current = error;
        while (current.InnerException is not null) current = current.InnerException;
        return current;
    }
}
