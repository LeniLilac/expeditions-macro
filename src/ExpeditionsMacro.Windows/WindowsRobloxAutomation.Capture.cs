using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public ImageFrame CaptureScreen(ScreenRegion region) => GdiScreenCapture.Capture(region);

    public ImageFrame CaptureClient(RobloxWindow window)
    {
        try
        {
            return CaptureWithSurfaceRecovery(
                () =>
                {
                    nint handle = ResolveHandle(window);
                    ClientBounds client = GetClientBounds(handle);
                    WindowBounds bounds = GetWindowBounds(handle);
                    WindowBounds extended =
                        GetExtendedFrameBounds(handle) ?? bounds;
                    return _windowCapture.CaptureClient(
                        handle,
                        client,
                        bounds,
                        extended,
                        _ => DiagnosticMessage?.Invoke(
                            "Windows capture paused during a Roblox transition; rebuilding the capture session and waiting for a new frame."));
                },
                (attempt, error) =>
                {
                    DiagnosticMessage?.Invoke(
                        $"Windows changed the Roblox capture surface from {error.ExpectedWidth} by {error.ExpectedHeight} to {error.ActualWidth} by {error.ActualHeight}; re-reading the live window geometry (attempt {attempt + 1}/{CaptureSurfaceRecoveryAttempts}).");
                    Thread.Sleep(
                        CaptureSurfaceRecoveryDelayMilliseconds);
                },
                CaptureSurfaceRecoveryAttempts);
        }
        catch (RobloxSessionUnavailableException)
        {
            throw;
        }
        catch (Exception error) when (
            error is InvalidOperationException or
            TimeoutException or
            COMException)
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not capture the Roblox window.",
                error);
        }
    }

    internal static T CaptureWithSurfaceRecovery<T>(
        Func<T> capture,
        Action<int, CaptureSurfaceChangedException>? beforeRetry = null,
        int maximumAttempts = CaptureSurfaceRecoveryAttempts)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                return capture();
            }
            catch (CaptureSurfaceChangedException error) when (attempt < maximumAttempts)
            {
                beforeRetry?.Invoke(attempt, error);
            }
            catch (CaptureSurfaceChangedException error)
            {
                throw new InvalidOperationException(
                    $"Windows could not stabilize the Roblox capture surface after {maximumAttempts} attempts. Keep Roblox visible and try the operation again.",
                    error);
            }
        }

        throw new UnreachableException();
    }

    private static WindowBounds? GetExtendedFrameBounds(nint handle)
    {
        uint size = (uint)Marshal.SizeOf<NativeMethods.Rect>();
        int result = NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DwmwaExtendedFrameBounds, out NativeMethods.Rect rectangle, size);
        if (result != 0 || rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top) return null;
        return new WindowBounds(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
    }
}
