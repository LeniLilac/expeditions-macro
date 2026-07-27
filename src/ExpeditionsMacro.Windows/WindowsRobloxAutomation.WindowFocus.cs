using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public bool Focus(RobloxWindow window)
    {
        nint handle = ResolveHandle(window);
        WindowsRobloxDisplayScale.EnsureOneHundredPercent(handle);
        if (TryFocus(handle)) return true;

        RobloxWindow? refreshed = FindWindow();
        if (refreshed is null) return false;
        RegisterAlias(window.Handle, refreshed.Value.Handle);
        if (handle != refreshed.Value.Handle)
        {
            DiagnosticMessage?.Invoke(
                "Roblox window refreshed after a focus failure: " +
                $"{refreshed.Value.ProcessDescription}.");
        }
        WindowsRobloxDisplayScale.EnsureOneHundredPercent(
            refreshed.Value.Handle);
        RevalidateTrackedClientSize(refreshed.Value.Handle);
        return TryFocus(refreshed.Value.Handle);
    }

    private static bool TryFocus(nint handle)
    {
        if (handle == nint.Zero ||
            !NativeMethods.IsWindow(handle))
        {
            return false;
        }
        if (NativeMethods.IsIconic(handle))
        {
            _ = NativeMethods.ShowWindowAsync(
                handle,
                NativeMethods.SwRestore);
        }

        nint parent =
            NativeMethods.GetParent(handle);
        if (parent != nint.Zero &&
            HasWindowStyle(
                handle,
                NativeMethods.WsChild))
        {
            return TryFocusChildWindow(
                handle,
                parent);
        }

        for (int attempt = 0;
            attempt < 3;
            attempt++)
        {
            _ = NativeMethods.BringWindowToTop(
                handle);
            if (NativeMethods.SetForegroundWindow(
                    handle) ||
                NativeMethods.GetForegroundWindow() ==
                    handle)
            {
                return true;
            }
            if (attempt < 2)
            {
                Thread.Sleep(25);
            }
        }
        return false;
    }

    private static bool TryFocusChildWindow(
        nint handle,
        nint parent)
    {
        nint foregroundRoot = parent;
        while (true)
        {
            nint next =
                NativeMethods.GetParent(
                    foregroundRoot);
            if (next == nint.Zero)
            {
                break;
            }
            foregroundRoot = next;
        }
        uint currentThread =
            NativeMethods.GetCurrentThreadId();
        uint childThread =
            NativeMethods.GetWindowThreadProcessId(
                handle,
                out _);
        uint parentThread =
            NativeMethods.GetWindowThreadProcessId(
                parent,
                out _);
        bool childAttached =
            childThread != 0 &&
            childThread != currentThread &&
            NativeMethods.AttachThreadInput(
                currentThread,
                childThread,
                true);
        bool parentAttached =
            parentThread != 0 &&
            parentThread != currentThread &&
            parentThread != childThread &&
            NativeMethods.AttachThreadInput(
                currentThread,
                parentThread,
                true);
        try
        {
            _ = NativeMethods.SetForegroundWindow(
                foregroundRoot);
            _ = NativeMethods.BringWindowToTop(
                handle);
            _ = NativeMethods.SetFocus(handle);
            return NativeMethods.GetFocus() ==
                handle;
        }
        finally
        {
            if (parentAttached)
            {
                _ = NativeMethods.AttachThreadInput(
                    currentThread,
                    parentThread,
                    false);
            }
            if (childAttached)
            {
                _ = NativeMethods.AttachThreadInput(
                    currentThread,
                    childThread,
                    false);
            }
        }
    }

    private static bool HasWindowStyle(
        nint handle,
        long style)
    {
        Marshal.SetLastPInvokeError(0);
        nint value =
            NativeMethods.GetWindowLongPtr(
                handle,
                NativeMethods.GwlStyle);
        return Marshal.GetLastPInvokeError() == 0 &&
            (value.ToInt64() & style) != 0;
    }
}
