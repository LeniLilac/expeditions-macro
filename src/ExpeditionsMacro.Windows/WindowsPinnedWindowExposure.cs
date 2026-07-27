using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal readonly record struct PinnedWindowExposureObservation(
    nint Owner,
    nint Source,
    nint Foreground,
    bool ForegroundVisible,
    bool ForegroundMinimized,
    bool ForegroundOwnedByOwner,
    bool BoundsAvailable,
    WindowBounds DashboardBounds,
    WindowBounds ForegroundBounds);

internal static class WindowsPinnedWindowExposure
{
    internal static bool IsDashboardExposed(
        nint owner,
        nint source)
    {
        if (owner == nint.Zero ||
            !NativeMethods.IsWindow(owner) ||
            !NativeMethods.IsWindowVisible(owner) ||
            NativeMethods.IsIconic(owner))
        {
            return false;
        }

        nint foreground =
            NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero ||
            !NativeMethods.IsWindow(foreground))
        {
            return true;
        }

        WindowBounds dashboardBounds = default;
        WindowBounds foregroundBounds = default;
        bool boundsAvailable =
            TryReadVisibleBounds(
                owner,
                out dashboardBounds) &&
            TryReadVisibleBounds(
                foreground,
                out foregroundBounds);
        PinnedWindowExposureObservation observation =
            new(
                owner,
                source,
                foreground,
                NativeMethods.IsWindowVisible(foreground),
                NativeMethods.IsIconic(foreground),
                IsOwnedBy(foreground, owner),
                boundsAvailable,
                dashboardBounds,
                foregroundBounds);
        return IsDashboardExposed(observation);
    }

    internal static bool IsDashboardExposed(
        PinnedWindowExposureObservation observation)
    {
        if (observation.Owner == nint.Zero)
        {
            return false;
        }
        if (observation.Foreground == nint.Zero ||
            observation.Foreground == observation.Owner ||
            (observation.Source != nint.Zero &&
             observation.Foreground == observation.Source) ||
            !observation.ForegroundVisible ||
            observation.ForegroundMinimized)
        {
            return true;
        }
        if (observation.ForegroundOwnedByOwner)
        {
            return false;
        }
        if (!observation.BoundsAvailable)
        {
            // Focus without measurable overlap is not proof of occlusion.
            return true;
        }

        return !Intersects(
            observation.DashboardBounds,
            observation.ForegroundBounds);
    }

    internal static bool Intersects(
        WindowBounds first,
        WindowBounds second)
    {
        long firstRight =
            (long)first.X + first.Width;
        long firstBottom =
            (long)first.Y + first.Height;
        long secondRight =
            (long)second.X + second.Width;
        long secondBottom =
            (long)second.Y + second.Height;
        return first.Width > 0 &&
            first.Height > 0 &&
            second.Width > 0 &&
            second.Height > 0 &&
            Math.Max(first.X, second.X) <
                Math.Min(firstRight, secondRight) &&
            Math.Max(first.Y, second.Y) <
                Math.Min(firstBottom, secondBottom);
    }

    private static bool TryReadVisibleBounds(
        nint window,
        out WindowBounds bounds)
    {
        NativeMethods.Rect rectangle;
        int result =
            NativeMethods.DwmGetWindowAttribute(
                window,
                NativeMethods.DwmwaExtendedFrameBounds,
                out rectangle,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf<
                    NativeMethods.Rect>());
        if (result != 0 &&
            !NativeMethods.GetWindowRect(
                window,
                out rectangle))
        {
            bounds = default;
            return false;
        }

        int width =
            rectangle.Right - rectangle.Left;
        int height =
            rectangle.Bottom - rectangle.Top;
        if (width <= 0 ||
            height <= 0)
        {
            bounds = default;
            return false;
        }

        bounds = new WindowBounds(
            rectangle.Left,
            rectangle.Top,
            width,
            height);
        return true;
    }

    private static bool IsOwnedBy(
        nint window,
        nint possibleOwner)
    {
        nint current = window;
        for (int depth = 0;
             depth < 32;
             depth++)
        {
            nint owner =
                NativeMethods.GetWindow(
                    current,
                    NativeMethods.GwOwner);
            if (owner == nint.Zero ||
                owner == current)
            {
                return false;
            }
            if (owner == possibleOwner)
            {
                return true;
            }
            current = owner;
        }
        return false;
    }
}
