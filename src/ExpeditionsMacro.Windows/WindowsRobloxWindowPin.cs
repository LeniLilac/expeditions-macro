using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class WindowsRobloxWindowPin : IDisposable
{
    public const int ClientWidth = 808;
    public const int ClientHeight = 611;

    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSystemMenu = 0x00080000L;
    private const long WsPopup = 0x80000000L;
    private const long WsExTopmost = 0x00000008L;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExAppWindow = 0x00040000L;
    private static readonly nint HwndTopmost =
        new(-1);
    private static readonly nint HwndNotTopmost =
        new(-2);

    private readonly object _gate = new();
    private PinnedWindowState? _state;

    private sealed record PinnedWindowState(
        nint Source,
        nint OriginalStyle,
        nint OriginalExtendedStyle,
        WindowBounds OriginalBounds);

    public bool IsPinned
    {
        get
        {
            lock (_gate)
            {
                return TryIsPinnedCore(_state);
            }
        }
    }

    public nint SourceHandle
    {
        get
        {
            lock (_gate)
            {
                return _state?.Source ??
                    nint.Zero;
            }
        }
    }

    public void Pin(
        nint source,
        WindowBounds screenBounds)
    {
        lock (_gate)
        {
            if (_state is { } current &&
                current.Source == source &&
                TryIsPinnedCore(current))
            {
                UpdateBoundsCore(
                    source,
                    screenBounds);
                return;
            }

            if (_state is not null &&
                !TryUnpinCore(out string error))
            {
                throw new InvalidOperationException(error);
            }

            ValidateSource(source);
            nint originalStyle =
                NativeWindowProperties.Read(
                    source,
                    NativeMethods.GwlStyle);
            if ((originalStyle.ToInt64() &
                    NativeMethods.WsChild) != 0)
            {
                throw new InvalidOperationException(
                    "Roblox is still embedded by an older macro instance. " +
                    "Close that macro, restart Roblox, and try again.");
            }
            PinnedWindowState state = new(
                source,
                originalStyle,
                NativeWindowProperties.Read(
                    source,
                    NativeMethods.GwlExStyle),
                ReadBounds(source));
            try
            {
                NativeWindowProperties.Write(
                    source,
                    NativeMethods.GwlStyle,
                    new nint(
                        BuildPinnedStyle(
                            state.OriginalStyle
                                .ToInt64())));
                NativeWindowProperties.Write(
                    source,
                    NativeMethods.GwlExStyle,
                    new nint(
                        BuildPinnedExtendedStyle(
                            state.OriginalExtendedStyle
                                .ToInt64())));
                UpdateBoundsCore(
                    source,
                    screenBounds,
                    frameChanged: true);
                _state = state;
            }
            catch
            {
                TryRestoreState(state);
                throw;
            }
        }
    }

    public void UpdateBounds(
        WindowBounds screenBounds)
    {
        lock (_gate)
        {
            if (_state is not { } state ||
                !TryIsPinnedCore(state))
            {
                return;
            }

            UpdateBoundsCore(
                state.Source,
                screenBounds);
        }
    }

    public bool TryUnpin(out string error)
    {
        lock (_gate)
        {
            return TryUnpinCore(out error);
        }
    }

    public void Dispose()
    {
        _ = TryUnpin(out _);
    }

    internal static long BuildPinnedStyle(
        long originalStyle)
    {
        long normalized =
            unchecked((uint)originalStyle);
        long removed =
            WsCaption |
            WsThickFrame |
            WsMinimizeBox |
            WsMaximizeBox |
            WsSystemMenu |
            NativeMethods.WsChild;
        return (normalized & ~removed) |
            WsPopup |
            NativeMethods.WsVisible;
    }

    internal static long BuildPinnedExtendedStyle(
        long originalStyle)
    {
        long normalized =
            unchecked((uint)originalStyle);
        return (normalized &
                ~(WsExNoActivate |
                  WsExAppWindow)) |
            WsExTopmost;
    }

    private bool TryUnpinCore(
        out string error)
    {
        if (_state is not { } state)
        {
            error = string.Empty;
            return true;
        }
        if (!NativeMethods.IsWindow(state.Source))
        {
            _state = null;
            error = string.Empty;
            return true;
        }

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
            RestoreBounds(state);
            if (!TryReadTopmost(
                    state.Source,
                    out bool topmost) ||
                topmost)
            {
                error =
                    "Windows did not remove Roblox from the topmost " +
                    "Dashboard pin after restoring its normal window.";
                return false;
            }
            _state = null;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
            when (exception is Win32Exception or
                InvalidOperationException)
        {
            error =
                "Windows could not return Roblox to its normal " +
                $"standalone window: {exception.Message}";
            return false;
        }
    }

    private static bool TryIsPinnedCore(
        PinnedWindowState? state)
    {
        if (state is null ||
            !NativeMethods.IsWindow(state.Source) ||
            !NativeWindowProperties.TryRead(
                state.Source,
                NativeMethods.GwlStyle,
                out nint styleValue) ||
            !TryReadTopmost(
                state.Source,
                out bool topmost))
        {
            return false;
        }

        return topmost &&
            (styleValue.ToInt64() &
                NativeMethods.WsChild) == 0;
    }

    private static bool TryReadTopmost(
        nint source,
        out bool topmost)
    {
        if (!NativeWindowProperties.TryRead(
            source,
            NativeMethods.GwlExStyle,
            out nint extendedStyle))
        {
            topmost = false;
            return false;
        }

        topmost =
            (extendedStyle.ToInt64() &
                WsExTopmost) != 0;
        return true;
    }

    private static void ValidateSource(
        nint source)
    {
        if (source == nint.Zero ||
            !NativeMethods.IsWindow(source) ||
            NativeMethods.GetWindowThreadProcessId(
                source,
                out uint processId) == 0 ||
            processId == 0)
        {
            throw new InvalidOperationException(
                "The selected Roblox window is no longer available.");
        }

        try
        {
            using Process process =
                Process.GetProcessById(
                    checked((int)processId));
            if (!WindowsRobloxAutomation
                .IsSupportedRobloxProcessName(
                    process.ProcessName))
            {
                throw new InvalidOperationException(
                    "Only a verified Roblox player window can be pinned.");
            }
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                InvalidOperationException or
                Win32Exception)
        {
            throw new InvalidOperationException(
                "The selected Roblox player process could not be verified.",
                exception);
        }
    }

    private static void UpdateBoundsCore(
        nint source,
        WindowBounds screenBounds,
        bool frameChanged = false)
    {
        uint flags =
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpShowWindow;
        if (frameChanged)
        {
            flags |=
                NativeMethods.SwpFrameChanged;
        }
        if (!NativeMethods.SetWindowPos(
            source,
            HwndTopmost,
            screenBounds.X,
            screenBounds.Y,
            ClientWidth,
            ClientHeight,
            flags))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not position pinned Roblox.");
        }
        _ = NativeMethods.ShowWindowAsync(
            source,
            NativeMethods.SwShow);
    }

    private static WindowBounds ReadBounds(
        nint window)
    {
        if (!NativeMethods.GetWindowRect(
            window,
            out NativeMethods.Rect bounds))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not read the Roblox window bounds.");
        }
        return new WindowBounds(
            bounds.Left,
            bounds.Top,
            bounds.Right - bounds.Left,
            bounds.Bottom - bounds.Top);
    }

    private static void TryRestoreState(
        PinnedWindowState state)
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
            RestoreBounds(state);
        }
        catch
        {
            // The original pin failure is more actionable.
        }
    }

    private static void RestoreBounds(
        PinnedWindowState state)
    {
        if (!NativeMethods.SetWindowPos(
            state.Source,
            HwndNotTopmost,
            state.OriginalBounds.X,
            state.OriginalBounds.Y,
            state.OriginalBounds.Width,
            state.OriginalBounds.Height,
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged |
            NativeMethods.SwpShowWindow))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Windows could not restore the Roblox window bounds.");
        }
        _ = NativeMethods.ShowWindowAsync(
            state.Source,
            NativeMethods.SwRestore);
    }
}
