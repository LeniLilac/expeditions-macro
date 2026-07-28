using System.ComponentModel;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal static class ManualInputTargetProbe
{
    public static ClientBounds ReadClientBounds(
        RobloxWindow window)
    {
        nint handle = window.Handle;
        if (handle == nint.Zero ||
            !NativeMethods.IsWindow(handle) ||
            NativeMethods.GetWindowThreadProcessId(
                handle,
                out uint processId) == 0 ||
            processId != checked((uint)window.ProcessId))
        {
            throw new InvalidOperationException(
                "The Roblox window changed during manual playback.");
        }
        if (!NativeMethods.GetClientRect(
                handle,
                out NativeMethods.Rect rectangle))
        {
            throw new Win32Exception(
                "Windows could not read the Roblox client during manual playback.");
        }

        NativeMethods.Point topLeft = new()
        {
            X = rectangle.Left,
            Y = rectangle.Top,
        };
        NativeMethods.Point bottomRight = new()
        {
            X = rectangle.Right,
            Y = rectangle.Bottom,
        };
        if (!NativeMethods.ClientToScreen(
                handle,
                ref topLeft) ||
            !NativeMethods.ClientToScreen(
                handle,
                ref bottomRight))
        {
            throw new Win32Exception(
                "Windows could not locate the Roblox client during manual playback.");
        }

        return new ClientBounds(
            topLeft.X,
            topLeft.Y,
            bottomRight.X - topLeft.X,
            bottomRight.Y - topLeft.Y);
    }
}
