namespace ExpeditionsMacro.DetectorViewer.Services;

internal static class ViewerWindowLifecycle
{
    public static void Close(
        CancellationTokenSource? cancellation,
        IDisposable source,
        bool shutdownApplication)
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        source.Dispose();
        if (shutdownApplication)
        {
            System.Windows.Application
                .Current
                .Shutdown();
        }
    }
}
