using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class WindowsRobloxAutomation : IRobloxAutomation
{
    public RobloxWindow? FindWindow(string titleFragment = "Roblox")
    {
        string fragment = titleFragment.Trim();
        List<RobloxWindow> matches = [];
        NativeMethods.EnumWindowsProc callback = (window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window)) return true;
            string title = WindowTitle(window);
            if (title.Contains(fragment, StringComparison.OrdinalIgnoreCase)) matches.Add(new RobloxWindow(window, title));
            return true;
        };
        if (!NativeMethods.EnumWindows(callback, nint.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return matches
            .OrderBy(match => string.Equals(match.Title, fragment, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(match => match.Title.Length)
            .Cast<RobloxWindow?>()
            .FirstOrDefault();
    }

    public RobloxWindow? ForegroundWindow()
    {
        nint handle = NativeMethods.GetForegroundWindow();
        return handle == nint.Zero ? null : new RobloxWindow(handle, WindowTitle(handle));
    }

    public ClientBounds GetClientBounds(RobloxWindow window)
    {
        if (!NativeMethods.GetClientRect(window.Handle, out NativeMethods.Rect rectangle)) throw new Win32Exception("Could not read the Roblox client rectangle.");
        NativeMethods.Point topLeft = new() { X = rectangle.Left, Y = rectangle.Top };
        NativeMethods.Point bottomRight = new() { X = rectangle.Right, Y = rectangle.Bottom };
        if (!NativeMethods.ClientToScreen(window.Handle, ref topLeft) || !NativeMethods.ClientToScreen(window.Handle, ref bottomRight))
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
        if (!NativeMethods.GetWindowRect(window.Handle, out NativeMethods.Rect rectangle)) throw new Win32Exception("Could not read the Roblox window bounds.");
        return new WindowBounds(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
    }

    public bool Focus(RobloxWindow window)
    {
        if (NativeMethods.IsIconic(window.Handle)) return false;
        NativeMethods.BringWindowToTop(window.Handle);
        return NativeMethods.SetForegroundWindow(window.Handle) || NativeMethods.GetForegroundWindow() == window.Handle;
    }

    public async Task ResizeClientAsync(RobloxWindow window, int width, int height, CancellationToken cancellationToken)
    {
        if (NativeMethods.IsIconic(window.Handle)) throw new InvalidOperationException("Roblox is minimized. Restore it before continuing.");
        if (NativeMethods.IsZoomed(window.Handle)) throw new InvalidOperationException("Roblox is maximized. Restore it to a normal window before continuing.");
        WindowBounds outer = GetWindowBounds(window);
        ClientBounds client = GetClientBounds(window);
        int outerWidth = checked(width + outer.Width - client.Width);
        int outerHeight = checked(height + outer.Height - client.Height);
        (int x, int y) = FitOnMonitor(window, outer.X, outer.Y, outerWidth, outerHeight);
        if (!NativeMethods.SetWindowPos(window.Handle, nint.Zero, x, y, outerWidth, outerHeight, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
        {
            throw new Win32Exception("Windows could not resize Roblox.");
        }

        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClientBounds current = GetClientBounds(window);
            if (current.Width == width && current.Height == height) return;
            await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        }

        ClientBounds actual = GetClientBounds(window);
        throw new InvalidOperationException($"Roblox did not accept the required {width} × {height} client size (actual: {actual.Width} × {actual.Height}).");
    }

    public void RestoreWindowBounds(RobloxWindow window, WindowBounds bounds)
    {
        if (!NativeMethods.SetWindowPos(window.Handle, nint.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged))
        {
            throw new Win32Exception("Windows could not restore the original Roblox bounds.");
        }
    }

    public ImageFrame CaptureScreen(ScreenRegion region) => GdiScreenCapture.Capture(region);

    public ImageFrame CaptureClient(RobloxWindow window) => GdiScreenCapture.Capture(GetClientBounds(window).AsRegion());

    public Task MoveCursorToClientCenterAsync(RobloxWindow window, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        if (!NativeMethods.SetCursorPos(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)) throw new Win32Exception("Windows could not move the cursor to Roblox.");
        return Task.CompletedTask;
    }

    public async Task ClickClientAsync(RobloxWindow window, int x, int y, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClientBounds bounds = GetClientBounds(window);
        if (x < 0 || y < 0 || x >= bounds.Width || y >= bounds.Height) throw new ArgumentOutOfRangeException(nameof(x), "Click falls outside the Roblox client.");
        if (!NativeMethods.SetCursorPos(bounds.X + x, bounds.Y + y)) throw new Win32Exception("Windows could not move the cursor to the Roblox coordinate.");
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        NativeMethods.mouse_event(NativeMethods.MouseeventfMove, 1, 0, 0, 0);
        NativeMethods.mouse_event(NativeMethods.MouseeventfMove, -1, 0, 0, 0);
        NativeMethods.mouse_event(NativeMethods.MouseeventfLeftDown, 0, 0, 0, 0);
        try
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.mouse_event(NativeMethods.MouseeventfLeftUp, 0, 0, 0, 0);
        }
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
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
            if (restoreCursor) NativeMethods.SetCursorPos(original.X, original.Y);
        }
    }

    public async Task ZoomOutFullyAsync(RobloxWindow window, int ticks, CancellationToken cancellationToken)
    {
        if (!Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        for (int index = 0; index < Math.Max(0, ticks); index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendMouse(NativeMethods.MouseeventfWheel, data: unchecked((uint)-NativeMethods.WheelDelta));
            await Task.Delay(35, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task TapLeftControlAsync(RobloxWindow window, CancellationToken cancellationToken) => PulseKeyAsync(window, NativeMethods.VkLeftControl, 0x1D, 70, cancellationToken);

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
        try
        {
            await Task.Delay(holdMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NativeMethods.keybd_event((byte)virtualKey, (byte)scanCode, NativeMethods.KeyeventfKeyUp, 0);
        }
    }

    private static void SendMouse(uint flags, int dx = 0, int dy = 0, uint data = 0)
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
    }

    private static (int X, int Y) FitOnMonitor(RobloxWindow window, int x, int y, int width, int height)
    {
        nint monitor = NativeMethods.MonitorFromWindow(window.Handle, NativeMethods.MonitorDefaultToNearest);
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
