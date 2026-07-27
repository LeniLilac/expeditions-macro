using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class WindowsManualInputRecorder : IManualInputRecorder
{
    private const int TargetCheckIntervalMilliseconds = 100;
    private const int StopFocusTransitionGraceMilliseconds = 500;
    private readonly IRobloxAutomation _automation;

    public WindowsManualInputRecorder(IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public Task<ManualInputRecording> RecordAsync(
        RobloxWindow window,
        ManualInputCaptureOptions options,
        CancellationToken stopToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return Task.Run(
            () => Record(window, options, stopToken),
            CancellationToken.None);
    }

    private ManualInputRecording Record(
        RobloxWindow window,
        ManualInputCaptureOptions options,
        CancellationToken stopToken)
    {
        stopToken.ThrowIfCancellationRequested();
        ClientBounds bounds = ValidateTarget(window, focus: true);
        EnsureInitialPointerInside(
            window,
            bounds,
            stopToken);
        bounds = ValidateTarget(window, focus: false);
        HashSet<int> ignoredVirtualKeys =
            options.IgnoredVirtualKeys.ToHashSet();
        List<ManualInputEvent> inputs = [];
        object inputsGate = new();
        ConcurrentQueue<Exception> failures = new();
        Stopwatch stopwatch = new();

        NativeMethods.HookProc keyboardCallback =
            (code, wParam, lParam) =>
            {
                if (!stopwatch.IsRunning ||
                    code != NativeMethods.HcAction ||
                    !IsTargetForeground(window))
                {
                    return NativeMethods.CallNextHookEx(
                        nint.Zero,
                        code,
                        wParam,
                        lParam);
                }
                try
                {
                    NativeMethods.KeyboardHookData data =
                        Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(
                            lParam);
                    if (ManualInputEventFactory.TryCreateKeyboard(
                        checked((uint)wParam),
                        data,
                        ElapsedMicroseconds(stopwatch),
                        ignoredVirtualKeys,
                        out ManualInputEvent? input))
                    {
                        lock (inputsGate)
                        {
                            inputs.Add(input!);
                        }
                    }
                }
                catch (Exception error)
                {
                    failures.Enqueue(error);
                }
                return NativeMethods.CallNextHookEx(
                    nint.Zero,
                    code,
                    wParam,
                    lParam);
            };

        NativeMethods.HookProc mouseCallback =
            (code, wParam, lParam) =>
            {
                if (!stopwatch.IsRunning ||
                    code != NativeMethods.HcAction ||
                    !IsTargetForeground(window))
                {
                    return NativeMethods.CallNextHookEx(
                        nint.Zero,
                        code,
                        wParam,
                        lParam);
                }
                try
                {
                    NativeMethods.MouseHookData data =
                        Marshal.PtrToStructure<NativeMethods.MouseHookData>(
                            lParam);
                    MouseObservation observation =
                        ManualInputEventFactory.CreateMouse(
                            checked((uint)wParam),
                            data,
                            bounds,
                            ElapsedMicroseconds(stopwatch));
                    if (observation.IsOutsideClient)
                    {
                        failures.Enqueue(
                            new PointerLeftClientException(
                                "The pointer left the Roblox client while recording."));
                    }
                    else if (observation.Input is not null)
                    {
                        lock (inputsGate)
                        {
                            inputs.Add(observation.Input);
                        }
                    }
                }
                catch (Exception error)
                {
                    failures.Enqueue(error);
                }
                return NativeMethods.CallNextHookEx(
                    nint.Zero,
                    code,
                    wParam,
                    lParam);
            };

        (nint keyboardHook, nint mouseHook) =
            InstallHooks(
                keyboardCallback,
                mouseCallback);
        try
        {
            (int X, int Y) initialPointer =
                ReadClientPointer(bounds);
            stopwatch.Start();
            long nextTargetCheck = 0;
            while (!stopToken.IsCancellationRequested &&
                failures.IsEmpty)
            {
                while (NativeMethods.PeekMessage(
                    out _,
                    nint.Zero,
                    0,
                    0,
                    NativeMethods.PmRemove))
                {
                }

                long elapsedMilliseconds =
                    stopwatch.ElapsedMilliseconds;
                if (elapsedMilliseconds >= nextTargetCheck)
                {
                    ClientBounds current;
                    try
                    {
                        current = ValidateTarget(
                            window,
                            focus: false);
                    }
                    catch (InvalidOperationException)
                    {
                        if (WaitForStopTransition(
                                stopToken))
                        {
                            break;
                        }
                        throw;
                    }
                    if (current != bounds)
                    {
                        throw new InvalidOperationException(
                            "The Roblox client moved or changed size while recording.");
                    }
                    nextTargetCheck =
                        elapsedMilliseconds +
                        TargetCheckIntervalMilliseconds;
                }
                Thread.Sleep(5);
            }
            if (failures.TryDequeue(out Exception? failure))
            {
                if (failure is not
                        PointerLeftClientException ||
                    !WaitForStopTransition(
                        stopToken))
                {
                    throw failure;
                }
            }

            ManualInputEvent[] snapshot;
            lock (inputsGate)
            {
                snapshot = inputs.ToArray();
            }
            ManualInputRecording recording = new()
            {
                Id = options.RecordingId,
                Name = options.RecordingName.Trim(),
                InitialClientX =
                    initialPointer.X,
                InitialClientY =
                    initialPointer.Y,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                DurationMicroseconds =
                    ElapsedMicroseconds(stopwatch),
                Events = snapshot,
            };
            if (snapshot.Length > 0)
            {
                recording.Validate();
            }
            return recording;
        }
        finally
        {
            _ = NativeMethods.UnhookWindowsHookEx(mouseHook);
            _ = NativeMethods.UnhookWindowsHookEx(keyboardHook);
            GC.KeepAlive(mouseCallback);
            GC.KeepAlive(keyboardCallback);
        }
    }

    internal static bool WaitForStopTransition(
        CancellationToken stopToken,
        int graceMilliseconds =
            StopFocusTransitionGraceMilliseconds)
    {
        if (graceMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(graceMilliseconds));
        }
        return stopToken.IsCancellationRequested ||
            stopToken.WaitHandle.WaitOne(
                graceMilliseconds);
    }

    private sealed class PointerLeftClientException :
        InvalidOperationException
    {
        public PointerLeftClientException(string message) :
            base(message)
        {
        }
    }

    private ClientBounds ValidateTarget(
        RobloxWindow window,
        bool focus)
    {
        if (window.ProcessId <= 0 ||
            !WindowsRobloxAutomation.IsSupportedRobloxProcessName(
                window.ProcessName))
        {
            throw new InvalidOperationException(
                "Manual recording requires a verified Roblox player window.");
        }
        if (focus && !_automation.Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox for manual recording.");
        }
        if (!IsTargetForeground(window))
        {
            throw new InvalidOperationException(
                "Roblox must remain the foreground window while recording.");
        }

        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != RobloxClientProfile.Width ||
            bounds.Height != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException(
                $"Manual recording requires the {RobloxClientProfile.Width} by {RobloxClientProfile.Height} Roblox client.");
        }
        return bounds;
    }

    private void EnsureInitialPointerInside(
        RobloxWindow window,
        ClientBounds bounds,
        CancellationToken cancellationToken)
    {
        if (TryReadClientPointer(
                bounds,
                out _))
        {
            return;
        }
        _automation.MoveCursorToClientCenterAsync(
                window,
                cancellationToken)
            .GetAwaiter()
            .GetResult();
        _ = ReadClientPointer(bounds);
    }

    private static (int X, int Y) ReadClientPointer(
        ClientBounds bounds)
    {
        if (TryReadClientPointer(
                bounds,
                out (int X, int Y) pointer))
        {
            return pointer;
        }
        throw new InvalidOperationException(
            "Windows could not establish the manual recording pointer inside Roblox.");
    }

    private static bool TryReadClientPointer(
        ClientBounds bounds,
        out (int X, int Y) pointer)
    {
        pointer = default;
        if (!NativeMethods.GetCursorPos(
                out NativeMethods.Point screen))
        {
            return false;
        }
        (int X, int Y)? relative =
            bounds.ToRelative(
                screen.X,
                screen.Y);
        if (relative is null)
        {
            return false;
        }
        pointer = relative.Value;
        return true;
    }

    private static bool IsTargetForeground(
        RobloxWindow window)
    {
        nint foreground =
            NativeMethods.GetForegroundWindow();
        return foreground != nint.Zero &&
            NativeMethods.GetWindowThreadProcessId(
                foreground,
                out uint processId) != 0 &&
            processId == checked((uint)window.ProcessId);
    }

    private static (nint Keyboard, nint Mouse) InstallHooks(
        NativeMethods.HookProc keyboardCallback,
        NativeMethods.HookProc mouseCallback)
    {
        nint module = NativeMethods.GetModuleHandle(null);
        nint keyboard = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            keyboardCallback,
            module,
            0);
        if (keyboard == nint.Zero)
        {
            throw new Win32Exception(
                "Windows could not start the manual keyboard observer.");
        }

        nint mouse = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            mouseCallback,
            module,
            0);
        if (mouse != nint.Zero)
        {
            return (keyboard, mouse);
        }

        _ = NativeMethods.UnhookWindowsHookEx(keyboard);
        throw new Win32Exception(
            "Windows could not start the manual mouse observer.");
    }

    internal static long ElapsedMicroseconds(
        Stopwatch stopwatch)
    {
        long ticks = stopwatch.ElapsedTicks;
        long seconds = ticks / Stopwatch.Frequency;
        long remainder = ticks % Stopwatch.Frequency;
        return checked(
            seconds * 1_000_000 +
            remainder * 1_000_000 /
            Stopwatch.Frequency);
    }
}
