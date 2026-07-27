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
    private PinnedRobloxWindowState? _state;
    private PinnedRobloxWindowState? _autoMinimizedState;

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

    public bool IsForegroundSession(
        nint owner)
    {
        lock (_gate)
        {
            nint foreground =
                NativeMethods.GetForegroundWindow();
            if (owner == nint.Zero ||
                foreground == nint.Zero ||
                !NativeMethods.IsWindow(owner) ||
                !NativeMethods.IsWindow(foreground))
            {
                return false;
            }

            nint source =
                _state is { } state &&
                NativeMethods.IsWindow(state.Source)
                    ? state.Source
                    : nint.Zero;
            return IsForegroundWindowAllowed(
                owner,
                source,
                foreground);
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
                !TryUnpinCore(
                    PinnedWindowReleaseDisposition
                        .Restore,
                    out string error))
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
            PinnedRobloxWindowState state;
            if (_autoMinimizedState is { } minimized &&
                minimized.Source == source)
            {
                state = minimized;
                _ = NativeMethods.ShowWindowAsync(
                    source,
                    NativeMethods.SwShowNoActivate);
            }
            else
            {
                _autoMinimizedState = null;
                state = new PinnedRobloxWindowState(
                    source,
                    originalStyle,
                    NativeWindowProperties.Read(
                        source,
                        NativeMethods.GwlExStyle),
                    ReadBounds(source));
            }
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
                _autoMinimizedState = null;
            }
            catch
            {
                WindowsPinnedWindowRelease
                    .TryRestoreAfterPinFailure(
                        state);
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
            return TryUnpinCore(
                PinnedWindowReleaseDisposition
                    .Restore,
                out error);
        }
    }

    public bool TryUnpinAndMinimize(
        out string error)
    {
        lock (_gate)
        {
            return TryUnpinCore(
                PinnedWindowReleaseDisposition
                    .MinimizeRetained,
                out error);
        }
    }

    public bool TrySuspend(out string error)
    {
        lock (_gate)
        {
            if (_state is not { } state)
            {
                error = string.Empty;
                return true;
            }

            nint foreground =
                NativeMethods.GetForegroundWindow();
            nint insertAfter =
                foreground != nint.Zero &&
                foreground != state.Source &&
                NativeMethods.IsWindow(foreground)
                    ? foreground
                    : HwndNotTopmost;
            return TryUnpinCore(
                insertAfter,
                PinnedWindowReleaseDisposition
                    .SuspendBehindForeground,
                out error);
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

    internal static bool IsForegroundWindowAllowed(
        nint owner,
        nint source,
        nint foreground) =>
        owner != nint.Zero &&
        foreground != nint.Zero &&
        (foreground == owner ||
         (source != nint.Zero &&
          foreground == source));

    private bool TryUnpinCore(
        PinnedWindowReleaseDisposition disposition,
        out string error) =>
        TryUnpinCore(
            HwndNotTopmost,
            disposition,
            out error);

    private bool TryUnpinCore(
        nint insertAfter,
        PinnedWindowReleaseDisposition disposition,
        out string error)
    {
        if (_state is not { } state)
        {
            if (disposition ==
                    PinnedWindowReleaseDisposition
                        .Restore &&
                _autoMinimizedState is { } minimized &&
                NativeMethods.IsWindow(minimized.Source) &&
                !WindowsPinnedWindowRelease.TryRestoreNormal(
                    minimized,
                    out error))
            {
                return false;
            }
            if (disposition ==
                PinnedWindowReleaseDisposition.Restore)
            {
                _autoMinimizedState = null;
            }
            error = string.Empty;
            return true;
        }
        if (!NativeMethods.IsWindow(state.Source))
        {
            _state = null;
            _autoMinimizedState = null;
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
            WindowsPinnedWindowRelease.Restore(
                state,
                insertAfter,
                disposition);
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
            _autoMinimizedState =
                disposition ==
                    PinnedWindowReleaseDisposition
                        .MinimizeRetained
                    ? state
                    : null;
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
        PinnedRobloxWindowState? state)
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
            NativeMethods.SwShowNoActivate);
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

}
