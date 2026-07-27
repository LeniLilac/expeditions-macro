using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal sealed record PinnedRobloxWindowState(
    nint Source,
    nint OriginalStyle,
    nint OriginalExtendedStyle,
    WindowBounds OriginalBounds);

internal enum PinnedWindowReleaseDisposition
{
    Restore,
    MinimizeRetained,
    SuspendBehindForeground,
}

internal static class WindowsPinnedWindowRelease
{
    private static readonly nint HwndNotTopmost =
        new(-2);

    internal static void Restore(
        PinnedRobloxWindowState state,
        nint insertAfter,
        PinnedWindowReleaseDisposition disposition)
    {
        uint flags =
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged;
        if (disposition !=
            PinnedWindowReleaseDisposition
                .MinimizeRetained)
        {
            flags |=
                NativeMethods.SwpShowWindow;
        }
        if (!NativeMethods.SetWindowPos(
            state.Source,
            insertAfter,
            state.OriginalBounds.X,
            state.OriginalBounds.Y,
            state.OriginalBounds.Width,
            state.OriginalBounds.Height,
            flags))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not restore the Roblox window bounds.");
        }
        int? showCommand =
            ResolveShowCommand(disposition);
        if (showCommand is not null)
        {
            _ = NativeMethods.ShowWindowAsync(
                state.Source,
                showCommand.Value);
        }
    }

    internal static void TryRestoreAfterPinFailure(
        PinnedRobloxWindowState state)
    {
        try
        {
            NativeWindowProperties.Write(
                state.Source,
                NativeMethods.GwlStyle,
                state.OriginalStyle);
            NativeWindowProperties.Write(
                state.Source,
                NativeMethods.GwlExStyle,
                state.OriginalExtendedStyle);
            Restore(
                state,
                HwndNotTopmost,
                PinnedWindowReleaseDisposition.Restore);
        }
        catch
        {
            // The original pin failure is more actionable.
        }
    }

    internal static bool TryRestoreNormal(
        PinnedRobloxWindowState state,
        out string error)
    {
        try
        {
            Restore(
                state,
                HwndNotTopmost,
                PinnedWindowReleaseDisposition.Restore);
            error = string.Empty;
            return true;
        }
        catch (Win32Exception exception)
        {
            error =
                "Windows could not restore automatically minimized " +
                $"Roblox: {exception.Message}";
            return false;
        }
    }

    internal static int? ResolveShowCommand(
        PinnedWindowReleaseDisposition disposition) =>
        disposition switch
        {
            PinnedWindowReleaseDisposition.Restore =>
                NativeMethods.SwRestore,
            PinnedWindowReleaseDisposition
                .MinimizeRetained =>
                NativeMethods.SwShowMinNoActive,
            PinnedWindowReleaseDisposition
                .SuspendBehindForeground =>
                null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(disposition)),
        };
}
