using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed record WindowsAutomationTrace(
    DateTimeOffset TimestampUtc,
    string Device,
    string Action,
    int? X = null,
    int? Y = null,
    int? DeltaX = null,
    int? DeltaY = null,
    int? VirtualKey = null,
    int? ScanCode = null,
    int? HoldMilliseconds = null,
    uint? Flags = null,
    uint? Data = null,
    bool? Extended = null);

public sealed class WindowsRobloxAutomation : IRobloxAutomation, IDisposable
{
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsSystemMenu = 0x00080000L;
    private const long WsPopup = 0x80000000L;
    private const long WsExDialogModalFrame = 0x00000001L;
    private const long WsExWindowEdge = 0x00000100L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExStaticEdge = 0x00020000L;
    private const int ForcedResizeAttempts = 5;
    private const int ClickPositionSettleMilliseconds = 75;
    private const int ClickHoldMilliseconds = 20;
    private const int CursorParkingInsetPixels = 24;
    private const int HoverClearPulseCount = 4;
    private const int HoverClearPulseIntervalMilliseconds = 100;
    private const int HoverRenderSettleMilliseconds = 100;
    internal static (char PrimaryKey, bool PrimaryIsExtended, bool UseWheelFallback) ZoomOutInputPolicy => ('O', false, true);
    private static readonly HashSet<string> SupportedRobloxProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "RobloxPlayerBeta",
        "Windows10Universal",
        "Roblox",
    };

    private readonly object _windowStateGate = new();
    private readonly SemaphoreSlim _sizingGate = new(1, 1);
    private readonly Dictionary<nint, nint> _windowAliases = [];
    private readonly Dictionary<nint, ForcedWindowState> _forcedWindows = [];
    private ClientSizeTarget? _activeClientSizeTarget;

    private sealed record ForcedWindowState(
        int ProcessId,
        nint OriginalStyle,
        nint OriginalExtendedStyle,
        WindowBounds OriginalBounds);

    private sealed record ClientSizeTarget(int Width, int Height);

    public event Action<string>? DiagnosticMessage;

    public event Action<WindowsAutomationTrace>? AutomationTrace;

    public RobloxWindow? FindWindow(string titleFragment = "Roblox")
    {
        string fragment = titleFragment.Trim();
        List<RobloxWindow> matches = [];
        NativeMethods.EnumWindowsProc callback = (window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window)) return true;
            string title = WindowTitle(window);
            if (title.Contains(fragment, StringComparison.OrdinalIgnoreCase) && TryDescribeRobloxWindow(window, title, out RobloxWindow match))
            {
                matches.Add(match);
            }
            return true;
        };
        if (!NativeMethods.EnumWindows(callback, nint.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return SelectBestWindow(matches, fragment);
    }

    public RobloxWindow? ForegroundWindow()
    {
        nint handle = NativeMethods.GetForegroundWindow();
        if (handle == nint.Zero) return null;
        string title = WindowTitle(handle);
        return TryDescribeWindow(handle, title, out RobloxWindow window)
            ? window
            : new RobloxWindow(handle, title);
    }

    public ClientBounds GetClientBounds(RobloxWindow window)
    {
        return GetClientBounds(ResolveHandle(window));
    }

    private static ClientBounds GetClientBounds(nint handle)
    {
        if (!NativeMethods.GetClientRect(handle, out NativeMethods.Rect rectangle)) throw new Win32Exception("Could not read the Roblox client rectangle.");
        NativeMethods.Point topLeft = new() { X = rectangle.Left, Y = rectangle.Top };
        NativeMethods.Point bottomRight = new() { X = rectangle.Right, Y = rectangle.Bottom };
        if (!NativeMethods.ClientToScreen(handle, ref topLeft) || !NativeMethods.ClientToScreen(handle, ref bottomRight))
        {
            throw new Win32Exception("Could not locate the Roblox client on screen.");
        }

        int width = bottomRight.X - topLeft.X;
        int height = bottomRight.Y - topLeft.Y;
        if (width <= 0 || height <= 0) throw new InvalidOperationException("The Roblox client has no visible area.");
        return new ClientBounds(topLeft.X, topLeft.Y, width, height);
    }

    public WindowBounds GetWindowBounds(RobloxWindow window)
    {
        return GetWindowBounds(ResolveHandle(window));
    }

    private static WindowBounds GetWindowBounds(nint handle)
    {
        if (!NativeMethods.GetWindowRect(handle, out NativeMethods.Rect rectangle)) throw new Win32Exception("Could not read the Roblox window bounds.");
        return new WindowBounds(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
    }

    public bool Focus(RobloxWindow window)
    {
        nint handle = ResolveHandle(window);
        if (TryFocus(handle)) return true;

        RobloxWindow? refreshed = FindWindow();
        if (refreshed is null) return false;
        RegisterAlias(window.Handle, refreshed.Value.Handle);
        if (handle != refreshed.Value.Handle)
        {
            DiagnosticMessage?.Invoke($"Roblox window refreshed after a focus failure: {refreshed.Value.ProcessDescription}.");
        }
        RevalidateTrackedClientSize(refreshed.Value.Handle);
        return TryFocus(refreshed.Value.Handle);
    }

    public async Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        nint handle = ResolveHandle(window, revalidateTrackedSize: false);
        await _sizingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ResizeClientCoreAsync(handle, width, height, cancellationToken).ConfigureAwait(false);
            lock (_windowStateGate)
            {
                _activeClientSizeTarget = new ClientSizeTarget(width, height);
            }
        }
        finally
        {
            _sizingGate.Release();
        }
    }

    private async Task ResizeClientCoreAsync(nint handle, int width, int height, CancellationToken cancellationToken)
    {
        await RestoreForSizingAsync(handle, cancellationToken).ConfigureAwait(false);
        if (IsForcedWindow(handle))
        {
            await ResizeForcedWindowAsync(handle, width, height, cancellationToken).ConfigureAwait(false);
            return;
        }

        WindowBounds outer = GetWindowBounds(handle);
        ForcedWindowState originalState = new(
            WindowProcessId(handle),
            ReadWindowLong(handle, NativeMethods.GwlStyle),
            ReadWindowLong(handle, NativeMethods.GwlExStyle),
            outer);
        ClientBounds client = GetClientBounds(handle);
        int outerWidth = checked(width + outer.Width - client.Width);
        int outerHeight = checked(height + outer.Height - client.Height);
        (int x, int y) = FitOnMonitor(handle, outer.X, outer.Y, outerWidth, outerHeight);
        if (!NativeMethods.SetWindowPos(handle, nint.Zero, x, y, outerWidth, outerHeight, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
        {
            throw new Win32Exception("Windows could not resize Roblox.");
        }

        if (await WaitForClientSizeAsync(handle, width, height, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false)) return;

        ClientBounds clamped = GetClientBounds(handle);
        DiagnosticMessage?.Invoke($"Roblox clamped normal sizing to {clamped.Width} by {clamped.Height}; enabling forced borderless sizing.");
        await EnableForcedSizingAsync(handle, width, height, originalState, cancellationToken).ConfigureAwait(false);
    }

    public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds)
    {
        lock (_windowStateGate)
        {
            _activeClientSizeTarget = null;
        }
        nint handle = ResolveHandle(window, revalidateTrackedSize: false);
        if (RestoreForcedStyle(handle, bounds, throwOnFailure: true)) return;
        if (!NativeMethods.SetWindowPos(handle, nint.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
        {
            throw new Win32Exception("Windows could not restore the original Roblox bounds.");
        }
    }

    public void Dispose()
    {
        KeyValuePair<nint, ForcedWindowState>[] states;
        lock (_windowStateGate)
        {
            _activeClientSizeTarget = null;
            _windowAliases.Clear();
            states = _forcedWindows.ToArray();
        }

        foreach ((nint handle, ForcedWindowState state) in states)
        {
            RestoreForcedStyle(handle, state.OriginalBounds, throwOnFailure: false);
        }
    }

    public ImageFrame CaptureScreen(ScreenRegion region) => GdiScreenCapture.Capture(region);

    public ImageFrame CaptureClient(RobloxWindow window) => GdiScreenCapture.Capture(GetClientBounds(window).AsRegion());

    public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        MoveCursorWithRegisteredMotion(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 1, "Windows could not move the cursor to Roblox.");
        return Task.CompletedTask;
    }

    public async Task ParkCursorAsync(RobloxWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        await ParkCursorWithAcknowledgedMotionAsync(GetClientBounds(window), cancellationToken).ConfigureAwait(false);
    }

    public async Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        if (x < 0 || y < 0 || x >= bounds.Width || y >= bounds.Height) throw new ArgumentOutOfRangeException(nameof(x), "Click falls outside the Roblox client.");
        int clickNudge = x < bounds.Width - 1 ? 1 : -1;
        MoveCursorWithRegisteredMotion(bounds.X + x, bounds.Y + y, clickNudge, "Windows could not move the cursor to the Roblox coordinate.");
        // Low-frame-rate clients can render the new button before their input loop
        // acknowledges the registered cursor move. Give Roblox two typical frames
        // before pressing so the click is hit-tested at the visible target.
        await Task.Delay(ClickPositionSettleMilliseconds, cancellationToken).ConfigureAwait(false);
        NativeMethods.mouse_event(NativeMethods.MouseeventfLeftDown, 0, 0, 0, 0);
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "left_down", X: x, Y: y, Flags: NativeMethods.MouseeventfLeftDown));
        try
        {
            await Task.Delay(ClickHoldMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.mouse_event(NativeMethods.MouseeventfLeftUp, 0, 0, 0, 0);
            EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "left_up", X: x, Y: y, Flags: NativeMethods.MouseeventfLeftUp));
        }
        // SetCursorPos alone can move the Windows pointer without making Roblox
        // process a mouse-motion event. Keep the pointer safely inside the client and
        // send spaced motion pulses so Roblox cannot coalesce the entire hover clear.
        await ParkCursorWithAcknowledgedMotionAsync(bounds, cancellationToken).ConfigureAwait(false);
        await Task.Delay(HoverRenderSettleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScrollClientAsync(RobloxWindow window, int notches, CancellationToken cancellationToken)
    {
        if (notches is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(notches));
        if (notches == 0) return;
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        ClientBounds bounds = GetClientBounds(window);
        MoveCursorWithRegisteredMotion(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 1, "Windows could not move the cursor into Roblox for scrolling.");
        int direction = Math.Sign(notches);
        for (int index = 0; index < Math.Abs(notches); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint data = unchecked((uint)(direction * NativeMethods.WheelDelta));
            SendMouse(NativeMethods.MouseeventfWheel, data: data);
            await Task.Delay(45, cancellationToken).ConfigureAwait(false);
        }
        await ParkCursorWithAcknowledgedMotionAsync(bounds, cancellationToken).ConfigureAwait(false);
        await Task.Delay(HoverRenderSettleMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    public async Task DragCameraAsync(RobloxWindow window, int deltaX, int deltaY, int chunkPixels, CancellationToken cancellationToken)
    {
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        if (deltaX == 0 && deltaY == 0) return;
        bool restoreCursor = NativeMethods.GetCursorPos(out NativeMethods.Point original);
        SendMouse(NativeMethods.MouseeventfRightDown);
        try
        {
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            int sentX = 0;
            int sentY = 0;
            int chunk = Math.Max(1, chunkPixels);
            while (sentX != deltaX || sentY != deltaY)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int remaining = Math.Max(Math.Abs(deltaX - sentX), Math.Abs(deltaY - sentY));
                double fraction = Math.Min(1d, (double)chunk / remaining);
                int nextX = fraction >= 1 ? deltaX : (int)Math.Round(sentX + (deltaX - sentX) * fraction);
                int nextY = fraction >= 1 ? deltaY : (int)Math.Round(sentY + (deltaY - sentY) * fraction);
                SendMouse(NativeMethods.MouseeventfMove, nextX - sentX, nextY - sentY);
                sentX = nextX;
                sentY = nextY;
                if (sentX != deltaX || sentY != deltaY) await Task.Delay(12, cancellationToken).ConfigureAwait(false);
            }
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SendMouse(NativeMethods.MouseeventfRightUp);
            if (restoreCursor) MoveCursorWithRegisteredMotion(original.X, original.Y, 1, "Windows could not restore the cursor after camera movement.");
        }
    }

    public async Task PulseCameraYawAsync(
        RobloxWindow window,
        CameraYawDirection direction,
        int holdMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        if (holdMilliseconds is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(holdMilliseconds));
        ushort scanCode = direction switch
        {
            CameraYawDirection.Left => 0x4B,
            CameraYawDirection.Right => 0x4D,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        SendKeyboard(scanCode, keyUp: false);
        try
        {
            await Task.Delay(holdMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Arrow keys are extended scan-code inputs. Releasing through the same
            // SendInput path prevents a canceled setup from leaving Roblox rotating.
            SendKeyboard(scanCode, keyUp: true);
        }
    }

    public async Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken)
    {
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        int pulseCount = Math.Max(0, ticks);
        (char primaryKey, bool primaryIsExtended, bool useWheelFallback) = ZoomOutInputPolicy;
        ushort zoomOutScanCode = checked((ushort)NativeMethods.MapVirtualKey(primaryKey, 0));
        try
        {
            for (int index = 0; index < pulseCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SendKeyboard(zoomOutScanCode, keyUp: false, extended: primaryIsExtended);
                try
                {
                    await Task.Delay(35, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    SendKeyboard(zoomOutScanCode, keyUp: true, extended: primaryIsExtended);
                }
                await Task.Delay(15, cancellationToken).ConfigureAwait(false);
            }
            return;
        }
        catch (Win32Exception) when (useWheelFallback)
        {
            // O is Roblox's built-in Zoom Out binding and avoids cursor-dependent
            // wheel handling. Keep the former wheel path as an OS-input fallback
            // for systems where Windows rejects the keyboard injection.
        }

        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox for the zoom fallback.");
        for (int index = 0; index < pulseCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendMouse(NativeMethods.MouseeventfWheel, data: unchecked((uint)-NativeMethods.WheelDelta));
            await Task.Delay(35, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken) => PulseKeyAsync(window, NativeMethods.VkLeftControl, 0x1D, 70, cancellationToken);

    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken)
    {
        char normalized = char.ToUpperInvariant(key);
        if (!char.IsAsciiLetter(normalized)) throw new ArgumentOutOfRangeException(nameof(key), "The Roblox key must be A through Z.");
        int virtualKey = normalized;
        int scanCode = checked((int)NativeMethods.MapVirtualKey((uint)virtualKey, 0));
        return PulseKeyAsync(window, virtualKey, scanCode, 70, cancellationToken);
    }

    public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken)
    {
        if (unitKey is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(unitKey));
        int scanCode = unitKey == 0 ? 0x0B : 0x01 + unitKey;
        return PulseKeyAsync(window, 0x30 + unitKey, scanCode, holdMilliseconds, cancellationToken);
    }

    private async Task PulseKeyAsync(RobloxWindow window, int virtualKey, int scanCode, int holdMilliseconds, CancellationToken cancellationToken)
    {
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        NativeMethods.keybd_event((byte)virtualKey, (byte)scanCode, 0, 0);
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "keyboard", "key_down", VirtualKey: virtualKey, ScanCode: scanCode, HoldMilliseconds: holdMilliseconds, Flags: 0));
        try
        {
            await Task.Delay(holdMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.keybd_event((byte)virtualKey, (byte)scanCode, NativeMethods.KeyeventfKeyUp, 0);
            EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "keyboard", "key_up", VirtualKey: virtualKey, ScanCode: scanCode, HoldMilliseconds: holdMilliseconds, Flags: NativeMethods.KeyeventfKeyUp));
        }
    }

    private void SendMouse(uint flags, int dx = 0, int dy = 0, uint data = 0)
    {
        NativeMethods.Input[] inputs =
        [
            new NativeMethods.Input
            {
                Type = NativeMethods.InputMouse,
                Value = new NativeMethods.InputUnion
                {
                    Mouse = new NativeMethods.MouseInput { Dx = dx, Dy = dy, MouseData = data, Flags = flags },
                },
            },
        ];
        if (NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>()) != 1) throw new Win32Exception("Windows rejected a simulated mouse input event.");
        EmitTrace(new WindowsAutomationTrace(
            DateTimeOffset.UtcNow,
            "mouse",
            MouseAction(flags),
            DeltaX: dx,
            DeltaY: dy,
            Flags: flags,
            Data: data));
    }

    private void SendKeyboard(ushort scanCode, bool keyUp, bool extended = true)
    {
        uint flags = NativeMethods.KeyeventfScanCode;
        if (extended) flags |= NativeMethods.KeyeventfExtendedKey;
        if (keyUp) flags |= NativeMethods.KeyeventfKeyUp;
        NativeMethods.Input[] inputs =
        [
            new NativeMethods.Input
            {
                Type = NativeMethods.InputKeyboard,
                Value = new NativeMethods.InputUnion
                {
                    Keyboard = new NativeMethods.KeyboardInput { ScanCode = scanCode, Flags = flags },
                },
            },
        ];
        if (NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.Input>()) != 1)
        {
            throw new Win32Exception("Windows rejected a simulated camera key event.");
        }
        EmitTrace(new WindowsAutomationTrace(
            DateTimeOffset.UtcNow,
            "keyboard",
            keyUp ? "key_up" : "key_down",
            ScanCode: scanCode,
            Flags: flags,
            Extended: extended));
    }

    private void MoveCursorWithRegisteredMotion(int x, int y, int nudgeX, string failureMessage)
    {
        if (!NativeMethods.SetCursorPos(x, y)) throw new Win32Exception(failureMessage);
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "set_cursor", X: x, Y: y));
        int delta = nudgeX < 0 ? -1 : 1;
        NativeMethods.mouse_event(NativeMethods.MouseeventfMove, delta, 0, 0, 0);
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "move", DeltaX: delta, DeltaY: 0, Flags: NativeMethods.MouseeventfMove));
        NativeMethods.mouse_event(NativeMethods.MouseeventfMove, -delta, 0, 0, 0);
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "move", DeltaX: -delta, DeltaY: 0, Flags: NativeMethods.MouseeventfMove));
    }

    private async Task ParkCursorWithAcknowledgedMotionAsync(ClientBounds bounds, CancellationToken cancellationToken)
    {
        int horizontalInset = Math.Min(CursorParkingInsetPixels, Math.Max(0, bounds.Width - 2));
        int verticalInset = Math.Min(CursorParkingInsetPixels, Math.Max(0, bounds.Height - 2));
        int parkingX = bounds.X + Math.Max(0, bounds.Width - 1 - horizontalInset);
        int parkingY = bounds.Y + Math.Max(0, bounds.Height - 1 - verticalInset);
        if (!NativeMethods.SetCursorPos(parkingX, parkingY))
        {
            throw new Win32Exception("Windows could not park the cursor away from the Roblox control.");
        }
        EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "set_cursor", X: parkingX, Y: parkingY));

        for (int pulse = 0; pulse < HoverClearPulseCount; pulse++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int delta = pulse % 2 == 0 ? -1 : 1;
            NativeMethods.mouse_event(NativeMethods.MouseeventfMove, delta, 0, 0, 0);
            EmitTrace(new WindowsAutomationTrace(DateTimeOffset.UtcNow, "mouse", "move", DeltaX: delta, DeltaY: 0, Flags: NativeMethods.MouseeventfMove));
            if (pulse + 1 < HoverClearPulseCount)
            {
                await Task.Delay(HoverClearPulseIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void EmitTrace(WindowsAutomationTrace trace)
    {
        try
        {
            AutomationTrace?.Invoke(trace);
        }
        catch
        {
            // Diagnostic observers must never prevent key or mouse release.
        }
    }

    private static string MouseAction(uint flags) => flags switch
    {
        NativeMethods.MouseeventfMove => "move",
        NativeMethods.MouseeventfLeftDown => "left_down",
        NativeMethods.MouseeventfLeftUp => "left_up",
        NativeMethods.MouseeventfRightDown => "right_down",
        NativeMethods.MouseeventfRightUp => "right_up",
        NativeMethods.MouseeventfWheel => "wheel",
        _ => "input",
    };

    internal static bool IsSupportedRobloxProcessName(string processName) => SupportedRobloxProcesses.Contains(processName);

    internal static RobloxWindow? SelectBestWindow(IEnumerable<RobloxWindow> matches, string titleFragment) =>
        matches
            .OrderBy(match => string.Equals(match.Title, titleFragment, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(match => ProcessPreference(match.ProcessName))
            .ThenBy(match => match.Title.Length)
            .ThenBy(match => match.ProcessId)
            .Cast<RobloxWindow?>()
            .FirstOrDefault();

    internal static long BuildForcedWindowStyle(long originalStyle)
    {
        long normalized = unchecked((uint)originalStyle);
        long removed = WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSystemMenu;
        return (normalized & ~removed) | WsPopup;
    }

    internal static long BuildForcedExtendedWindowStyle(long originalStyle)
    {
        long normalized = unchecked((uint)originalStyle);
        long removed = WsExDialogModalFrame | WsExWindowEdge | WsExClientEdge | WsExStaticEdge;
        return normalized & ~removed;
    }

    private static int ProcessPreference(string processName) => processName.ToUpperInvariant() switch
    {
        "ROBLOXPLAYERBETA" => 0,
        "WINDOWS10UNIVERSAL" => 1,
        "ROBLOX" => 2,
        _ => 3,
    };

    private static bool TryDescribeRobloxWindow(nint handle, string title, out RobloxWindow window)
    {
        if (!TryDescribeWindow(handle, title, out window)) return false;
        return IsSupportedRobloxProcessName(window.ProcessName);
    }

    private static bool TryDescribeWindow(nint handle, string title, out RobloxWindow window)
    {
        window = default;
        if (NativeMethods.GetWindowThreadProcessId(handle, out uint processId) == 0 || processId == 0) return false;
        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            string processName = process.ProcessName;
            if (string.IsNullOrWhiteSpace(processName)) return false;
            window = new RobloxWindow(handle, title, checked((int)processId), processName);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private static bool IsVerifiedRobloxHandle(nint handle)
    {
        if (handle == nint.Zero || !NativeMethods.IsWindow(handle)) return false;
        return TryDescribeRobloxWindow(handle, WindowTitle(handle), out _);
    }

    private nint ResolveHandle(RobloxWindow window, bool revalidateTrackedSize = true)
    {
        nint handle = window.Handle;
        lock (_windowStateGate)
        {
            HashSet<nint> visited = [];
            while (_windowAliases.TryGetValue(handle, out nint next) && visited.Add(handle)) handle = next;
        }
        if (IsVerifiedRobloxHandle(handle))
        {
            if (revalidateTrackedSize) RevalidateTrackedClientSize(handle);
            return handle;
        }

        RobloxWindow? refreshed = FindWindow();
        if (refreshed is null) return handle;
        RegisterAlias(window.Handle, refreshed.Value.Handle);
        if (handle != refreshed.Value.Handle)
        {
            DiagnosticMessage?.Invoke($"Roblox window handle refreshed: {refreshed.Value.ProcessDescription}.");
        }
        if (revalidateTrackedSize) RevalidateTrackedClientSize(refreshed.Value.Handle);
        return refreshed.Value.Handle;
    }

    private void RegisterAlias(nint original, nint replacement)
    {
        if (original == nint.Zero || replacement == nint.Zero || original == replacement) return;
        lock (_windowStateGate)
        {
            _windowAliases[original] = replacement;
        }
    }

    private static bool TryFocus(nint handle)
    {
        if (handle == nint.Zero || !NativeMethods.IsWindow(handle)) return false;
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindowAsync(handle, NativeMethods.SwRestore);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            NativeMethods.BringWindowToTop(handle);
            if (NativeMethods.SetForegroundWindow(handle) || NativeMethods.GetForegroundWindow() == handle) return true;
            if (attempt < 2) Thread.Sleep(25);
        }
        return false;
    }

    private bool IsForcedWindow(nint handle)
    {
        lock (_windowStateGate)
        {
            return _forcedWindows.ContainsKey(handle);
        }
    }

    private async Task EnableForcedSizingAsync(
        nint handle,
        int width,
        int height,
        ForcedWindowState state,
        CancellationToken cancellationToken)
    {
        bool styleChanged = false;
        try
        {
            WriteWindowLong(handle, NativeMethods.GwlStyle, new nint(BuildForcedWindowStyle(state.OriginalStyle.ToInt64())));
            styleChanged = true;
            WriteWindowLong(handle, NativeMethods.GwlExStyle, new nint(BuildForcedExtendedWindowStyle(state.OriginalExtendedStyle.ToInt64())));
            lock (_windowStateGate)
            {
                _forcedWindows[handle] = state;
            }
            await ResizeForcedWindowAsync(handle, width, height, cancellationToken).ConfigureAwait(false);
            if (TryDescribeWindow(handle, WindowTitle(handle), out RobloxWindow window))
            {
                DiagnosticMessage?.Invoke($"Forced borderless sizing is active for {window.ProcessDescription}.");
            }
        }
        catch
        {
            lock (_windowStateGate)
            {
                _forcedWindows.Remove(handle);
            }
            if (styleChanged)
            {
                TryRestoreWindowState(handle, state, state.OriginalBounds);
            }
            throw;
        }
    }

    private async Task RestoreForSizingAsync(nint handle, CancellationToken cancellationToken)
    {
        if (!NativeMethods.IsIconic(handle) && !NativeMethods.IsZoomed(handle)) return;
        NativeMethods.ShowWindowAsync(handle, NativeMethods.SwRestore);

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!NativeMethods.IsIconic(handle) && !NativeMethods.IsZoomed(handle))
            {
                DiagnosticMessage?.Invoke("Roblox was restored from its minimized or maximized state before sizing.");
                return;
            }
            await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new InvalidOperationException("Windows could not restore Roblox to a resizable window state.");
    }

    private void RevalidateTrackedClientSize(nint handle)
    {
        ClientSizeTarget? target;
        lock (_windowStateGate)
        {
            target = _activeClientSizeTarget;
        }
        if (target is null || HasClientSize(handle, target.Width, target.Height)) return;

        _sizingGate.Wait();
        try
        {
            lock (_windowStateGate)
            {
                target = _activeClientSizeTarget;
            }
            if (target is null || HasClientSize(handle, target.Width, target.Height)) return;

            DiagnosticMessage?.Invoke($"Roblox client geometry changed after a window refresh or teleport; reapplying {target.Width} by {target.Height}.");
            ResizeClientCoreAsync(handle, target.Width, target.Height, CancellationToken.None).GetAwaiter().GetResult();
            DiagnosticMessage?.Invoke($"Roblox client geometry revalidated at {target.Width} by {target.Height}.");
        }
        finally
        {
            _sizingGate.Release();
        }
    }

    private static bool HasClientSize(nint handle, int width, int height) =>
        NativeMethods.GetClientRect(handle, out NativeMethods.Rect rectangle) &&
        rectangle.Right - rectangle.Left == width &&
        rectangle.Bottom - rectangle.Top == height;

    private static async Task ResizeForcedWindowAsync(nint handle, int width, int height, CancellationToken cancellationToken)
    {
        int outerWidth = width;
        int outerHeight = height;
        WindowBounds startingBounds = GetWindowBounds(handle);
        int x = startingBounds.X;
        int y = startingBounds.Y;
        for (int attempt = 0; attempt < ForcedResizeAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (x, y) = FitOnMonitor(handle, x, y, outerWidth, outerHeight);
            if (!NativeMethods.SetWindowPos(
                handle,
                nint.Zero,
                x,
                y,
                outerWidth,
                outerHeight,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow))
            {
                throw new Win32Exception("Windows could not force Roblox to the standard client size.");
            }

            if (await WaitForClientSizeAsync(handle, width, height, TimeSpan.FromMilliseconds(350), cancellationToken).ConfigureAwait(false)) return;
            WindowBounds outer = GetWindowBounds(handle);
            ClientBounds client = GetClientBounds(handle);
            outerWidth = checked(outer.Width + width - client.Width);
            outerHeight = checked(outer.Height + height - client.Height);
        }

        ClientBounds actual = GetClientBounds(handle);
        throw new InvalidOperationException($"Roblox did not accept the required {width} × {height} client size, including forced borderless sizing (actual: {actual.Width} × {actual.Height}).");
    }

    private static async Task<bool> WaitForClientSizeAsync(
        nint handle,
        int width,
        int height,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientBounds current = GetClientBounds(handle);
            if (current.Width == width && current.Height == height) return true;
            await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);
        return false;
    }

    private bool RestoreForcedStyle(nint handle, WindowBounds bounds, bool throwOnFailure)
    {
        ForcedWindowState? state;
        lock (_windowStateGate)
        {
            if (!_forcedWindows.Remove(handle, out state)) return false;
        }
        if (!IsSameWindowProcess(handle, state.ProcessId)) return true;
        if (TryRestoreWindowState(handle, state, bounds)) return true;
        if (throwOnFailure) throw new Win32Exception("Windows could not restore the original Roblox window style.");
        DiagnosticMessage?.Invoke("Windows could not restore the original Roblox window style during shutdown.");
        return true;
    }

    private static bool TryRestoreWindowState(nint handle, ForcedWindowState state, WindowBounds bounds)
    {
        if (!IsSameWindowProcess(handle, state.ProcessId)) return false;
        try
        {
            WriteWindowLong(handle, NativeMethods.GwlStyle, state.OriginalStyle);
            WriteWindowLong(handle, NativeMethods.GwlExStyle, state.OriginalExtendedStyle);
            return NativeMethods.SetWindowPos(
                handle,
                nint.Zero,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged | NativeMethods.SwpShowWindow);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static int WindowProcessId(nint handle)
    {
        if (NativeMethods.GetWindowThreadProcessId(handle, out uint processId) == 0 || processId == 0)
        {
            throw new Win32Exception("Windows could not identify the Roblox process that owns the window.");
        }
        return checked((int)processId);
    }

    private static bool IsSameWindowProcess(nint handle, int processId) =>
        NativeMethods.IsWindow(handle) &&
        NativeMethods.GetWindowThreadProcessId(handle, out uint currentProcessId) != 0 &&
        currentProcessId == (uint)processId;

    private static nint ReadWindowLong(nint handle, int index)
    {
        Marshal.SetLastPInvokeError(0);
        nint value = NativeMethods.GetWindowLongPtr(handle, index);
        int error = Marshal.GetLastPInvokeError();
        if (value == nint.Zero && error != 0) throw new Win32Exception(error);
        return value;
    }

    private static void WriteWindowLong(nint handle, int index, nint value)
    {
        Marshal.SetLastPInvokeError(0);
        nint previous = NativeMethods.SetWindowLongPtr(handle, index, value);
        int error = Marshal.GetLastPInvokeError();
        if (previous == nint.Zero && error != 0) throw new Win32Exception(error);
    }

    private static (int X, int Y) FitOnMonitor(nint handle, int x, int y, int width, int height)
    {
        nint monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
        NativeMethods.MonitorInfo info = new() { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (monitor == nint.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info)) return (x, y);
        NativeMethods.Rect work = info.Work;
        int fittedX = width > work.Right - work.Left ? work.Left : Math.Min(Math.Max(x, work.Left), work.Right - width);
        int fittedY = height > work.Bottom - work.Top ? work.Top : Math.Min(Math.Max(y, work.Top), work.Bottom - height);
        return (fittedX, fittedY);
    }

    private static string WindowTitle(nint handle)
    {
        int length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0) return string.Empty;
        char[] buffer = new char[length + 1];
        int written = NativeMethods.GetWindowText(handle, buffer, buffer.Length);
        return written <= 0 ? string.Empty : new string(buffer, 0, written);
    }
}
