using System.ComponentModel;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal static class RegisteredCursorMotion
{
    public static void Move(
        int x,
        int y,
        int nudgeX,
        string failureMessage,
        Action<WindowsAutomationTrace>? trace = null)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            throw new Win32Exception(
                failureMessage);
        }
        trace?.Invoke(
            new WindowsAutomationTrace(
                DateTimeOffset.UtcNow,
                "mouse",
                "set_cursor",
                X: x,
                Y: y));

        int delta = nudgeX < 0 ? -1 : 1;
        NativeMethods.mouse_event(
            NativeMethods.MouseeventfMove,
            delta,
            0,
            0,
            0);
        trace?.Invoke(
            new WindowsAutomationTrace(
                DateTimeOffset.UtcNow,
                "mouse",
                "move",
                DeltaX: delta,
                DeltaY: 0,
                Flags:
                    NativeMethods.MouseeventfMove));
        NativeMethods.mouse_event(
            NativeMethods.MouseeventfMove,
            -delta,
            0,
            0,
            0);
        trace?.Invoke(
            new WindowsAutomationTrace(
                DateTimeOffset.UtcNow,
                "mouse",
                "move",
                DeltaX: -delta,
                DeltaY: 0,
                Flags:
                    NativeMethods.MouseeventfMove));
    }
}
