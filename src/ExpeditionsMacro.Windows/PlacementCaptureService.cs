using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed record PlacementInputTrace(
    DateTimeOffset TimestampUtc,
    string Action,
    int? VirtualKey = null,
    int? UnitKey = null,
    int? ScreenX = null,
    int? ScreenY = null,
    int? ClientX = null,
    int? ClientY = null);

public sealed class PlacementCaptureService : IPlacementCaptureService
{
    private readonly IRobloxAutomation _automation;

    public PlacementCaptureService(IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public event Action<PlacementInputTrace>? InputTrace;

    public Func<bool>? TraceEnabled { get; set; }

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
            if (code == NativeMethods.HcAction && (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown or NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
            {
                NativeMethods.KeyboardHookData data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
                int? key = UnitKeyFromVirtualKey((int)data.VirtualKey);
                bool isRobloxForeground = NativeMethods.GetForegroundWindow() == window.Handle;
                if (isRobloxForeground)
                {
                    EmitTrace(new PlacementInputTrace(
                        DateTimeOffset.UtcNow,
                        (uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown
                            ? "observed_key_down"
                            : "observed_key_up",
                        VirtualKey: (int)data.VirtualKey,
                        UnitKey: key));
                }
                if ((uint)wParam is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown &&
                    key is not null &&
                    isRobloxForeground)
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
            if (code == NativeMethods.HcAction &&
                (uint)wParam == NativeMethods.WmMouseMove &&
                TraceEnabled?.Invoke() == false)
            {
                return NativeMethods.CallNextHookEx(nint.Zero, code, wParam, lParam);
            }
            if (code == NativeMethods.HcAction && (uint)wParam is NativeMethods.WmMouseMove or NativeMethods.WmLButtonDown or NativeMethods.WmLButtonUp)
            {
                if (NativeMethods.GetForegroundWindow() == window.Handle)
                {
                    try
                    {
                        NativeMethods.MouseHookData data = Marshal.PtrToStructure<NativeMethods.MouseHookData>(lParam);
                        if ((uint)wParam != NativeMethods.WmLButtonDown)
                        {
                            EmitTrace(new PlacementInputTrace(
                                DateTimeOffset.UtcNow,
                                (uint)wParam == NativeMethods.WmMouseMove ? "observed_mouse_move" : "observed_left_button_up",
                                UnitKey: selectedKey,
                                ScreenX: data.Position.X,
                                ScreenY: data.Position.Y));
                            return NativeMethods.CallNextHookEx(nint.Zero, code, wParam, lParam);
                        }

                        ClientBounds current = _automation.GetClientBounds(window);
                        (int X, int Y)? relative = current.ToRelative(data.Position.X, data.Position.Y);
                        EmitTrace(new PlacementInputTrace(
                            DateTimeOffset.UtcNow,
                            "observed_left_button_down",
                            UnitKey: selectedKey,
                            ScreenX: data.Position.X,
                            ScreenY: data.Position.Y,
                            ClientX: relative?.X,
                            ClientY: relative?.Y));
                        if (current.Width != initial.Width || current.Height != initial.Height)
                        {
                            failures.Add(new InvalidOperationException("The Roblox client size changed while recording. Nothing was saved."));
                        }
                        else if (selectedKey is null)
                        {
                            status?.Invoke("Click ignored. Press a number key before placing a unit.");
                        }
                        else if (relative is { } client)
                        {
                            PlacementCapture capture = new(selectedKey.Value, client.X, client.Y, selectedAt, checked((int)stopwatch.ElapsedMilliseconds));
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

        status?.Invoke("Recording. Press a number and click each location. Press the macro hotkey to finish.");
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

    private void EmitTrace(PlacementInputTrace trace)
    {
        try
        {
            InputTrace?.Invoke(trace);
        }
        catch
        {
            // Diagnostic observers must never interrupt the low-level recording hooks.
        }
    }
}
