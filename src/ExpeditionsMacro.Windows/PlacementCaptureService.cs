using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class PlacementCaptureService : IPlacementCaptureService
{
    private readonly IRobloxAutomation _automation;

    public PlacementCaptureService(IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public Task<(int ClientWidth, int ClientHeight, IReadOnlyList<PlacementCapture> Captures)> RecordAsync(
        RobloxWindow window,
        Action<PlacementCapture>? captured,
        Action<string>? status,
        CancellationToken cancellationToken) =>
        Task.Run(() => Record(window, captured, status, cancellationToken), CancellationToken.None);

    private (int ClientWidth, int ClientHeight, IReadOnlyList<PlacementCapture> Captures) Record(
        RobloxWindow window,
        Action<PlacementCapture>? captured,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        ClientBounds initial = _automation.GetClientBounds(window);
        List<PlacementCapture> captures = [];
        List<Exception> failures = [];
        Stopwatch stopwatch = Stopwatch.StartNew();
        int? selectedKey = null;
        int selectedAt = 0;

        NativeMethods.HookProc? keyboardCallback = null;
        NativeMethods.HookProc? mouseCallback = null;
        keyboardCallback = (code, wParam, lParam) =>
        {
            if (code == NativeMethods.HcAction && (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
            {
                NativeMethods.KeyboardHookData data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
                int? key = UnitKeyFromVirtualKey((int)data.VirtualKey);
                RobloxWindow? foreground = _automation.ForegroundWindow();
                if (key is not null && foreground?.Handle == window.Handle)
                {
                    selectedKey = key;
                    selectedAt = checked((int)stopwatch.ElapsedMilliseconds);
                    status?.Invoke($"Unit key {key} selected. Click its placement location.");
                }
            }
            return NativeMethods.CallNextHookEx(nint.Zero, code, wParam, lParam);
        };
        mouseCallback = (code, wParam, lParam) =>
        {
            if (code == NativeMethods.HcAction && (uint)wParam == NativeMethods.WmLButtonDown)
            {
                RobloxWindow? foreground = _automation.ForegroundWindow();
                if (foreground?.Handle == window.Handle)
                {
                    try
                    {
                        NativeMethods.MouseHookData data = Marshal.PtrToStructure<NativeMethods.MouseHookData>(lParam);
                        ClientBounds current = _automation.GetClientBounds(window);
                        if (current.Width != initial.Width || current.Height != initial.Height)
                        {
                            failures.Add(new InvalidOperationException("The Roblox client size changed while recording. Nothing was saved."));
                        }
                        else if (selectedKey is null)
                        {
                            status?.Invoke("Click ignored. Press a number key before placing a unit.");
                        }
                        else if (current.ToRelative(data.Position.X, data.Position.Y) is { } relative)
                        {
                            PlacementCapture capture = new(selectedKey.Value, relative.X, relative.Y, selectedAt, checked((int)stopwatch.ElapsedMilliseconds));
                            captures.Add(capture);
                            captured?.Invoke(capture);
                            status?.Invoke($"Recorded placement {captures.Count}: key {capture.UnitKey} at ({capture.X}, {capture.Y}).");
                            selectedKey = null;
                        }
                    }
                    catch (Exception error)
                    {
                        failures.Add(error);
                    }
                }
            }
            return NativeMethods.CallNextHookEx(nint.Zero, code, wParam, lParam);
        };

        nint module = NativeMethods.GetModuleHandle(null);
        nint keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, keyboardCallback, module, 0);
        if (keyboardHook == nint.Zero) throw new Win32Exception("Windows could not start the keyboard observer.");
        nint mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, mouseCallback, module, 0);
        if (mouseHook == nint.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(keyboardHook);
            throw new Win32Exception("Windows could not start the mouse observer.");
        }

        status?.Invoke("Recording. Press a number and click each location. Press F6 to finish.");
        try
        {
            while (!cancellationToken.IsCancellationRequested && failures.Count == 0)
            {
                while (NativeMethods.PeekMessage(out _, nint.Zero, 0, 0, NativeMethods.PmRemove)) { }
                Thread.Sleep(10);
            }
            if (failures.Count != 0) throw failures[0];
            return (initial.Width, initial.Height, captures.ToArray());
        }
        finally
        {
            NativeMethods.UnhookWindowsHookEx(mouseHook);
            NativeMethods.UnhookWindowsHookEx(keyboardHook);
            GC.KeepAlive(mouseCallback);
            GC.KeepAlive(keyboardCallback);
        }
    }

    internal static int? UnitKeyFromVirtualKey(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39) return virtualKey - 0x30;
        if (virtualKey is >= 0x60 and <= 0x69) return virtualKey - 0x60;
        return null;
    }
}
