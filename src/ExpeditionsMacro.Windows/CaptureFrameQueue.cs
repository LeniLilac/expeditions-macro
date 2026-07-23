namespace ExpeditionsMacro.Windows;

internal static class CaptureFrameQueue
{
    public static void DiscardAll<T>(Func<T?> tryTake)
        where T : class, IDisposable
    {
        using T? discarded = TakeLatest(tryTake);
    }

    public static T? TakeLatest<T>(Func<T?> tryTake)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(tryTake);

        T? latest = null;
        while (tryTake() is { } next)
        {
            latest?.Dispose();
            latest = next;
        }

        return latest;
    }
}
