using System.ComponentModel;
using System.Runtime.InteropServices;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

public sealed class WindowsManualInputPlayback :
    IManualInputPlayback
{
    private readonly IRobloxAutomation _automation;
    private readonly ManualInputPlaybackEngine _engine = new();

    public WindowsManualInputPlayback(
        IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public event Action<ManualInputPlaybackTiming>? TimingObserved;

    public event Action<ManualInputPlaybackSummary>? SummaryObserved;

    public async Task PlayAsync(
        RobloxWindow window,
        ManualInputRecording recording,
        CancellationToken cancellationToken,
        Action? playbackStarting = null)
    {
        ArgumentNullException.ThrowIfNull(recording);
        cancellationToken.ThrowIfCancellationRequested();
        recording.Validate();
        ClientBounds bounds =
            ValidateTarget(window, focus: true);
        WindowsManualInputSink sink = new(
            _automation,
            window,
            bounds);
        int sentEvents = 0;
        long maximumDriftMicroseconds = 0;
        bool succeeded = false;
        try
        {
            await _engine.PlayAsync(
                    recording,
                    new StopwatchManualInputClock(),
                    sink,
                    playbackStarting,
                    timing =>
                    {
                        sentEvents++;
                        maximumDriftMicroseconds =
                            Math.Max(
                                maximumDriftMicroseconds,
                                timing.DriftMicroseconds);
                        EmitTiming(timing);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            succeeded = true;
        }
        finally
        {
            EmitSummary(
                new ManualInputPlaybackSummary(
                    recording.Id,
                    sentEvents,
                    recording.Events.Count,
                    maximumDriftMicroseconds,
                    succeeded));
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
                "Manual playback requires a verified Roblox player window.");
        }
        if (focus && !_automation.Focus(window))
        {
            throw new InvalidOperationException(
                "Windows could not focus Roblox for manual playback.");
        }
        if (!WindowsManualInputSink.IsTargetForeground(window))
        {
            throw new InvalidOperationException(
                "Roblox must be the foreground window for manual playback.");
        }

        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width != RobloxClientProfile.Width ||
            bounds.Height != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException(
                $"Manual playback requires the {RobloxClientProfile.Width} by {RobloxClientProfile.Height} Roblox client.");
        }
        return bounds;
    }

    private void EmitTiming(
        ManualInputPlaybackTiming timing)
    {
        try
        {
            TimingObserved?.Invoke(timing);
        }
        catch
        {
            // Timing observers must never interfere with input or cleanup.
        }
    }

    private void EmitSummary(
        ManualInputPlaybackSummary summary)
    {
        try
        {
            SummaryObserved?.Invoke(summary);
        }
        catch
        {
            // Diagnostic observers must never interfere with input cleanup.
        }
    }
}

internal sealed class WindowsManualInputSink :
    IManualInputSink
{
    private const uint MouseeventfMiddleDown = 0x0020;
    private const uint MouseeventfMiddleUp = 0x0040;
    private const uint MouseeventfXDown = 0x0080;
    private const uint MouseeventfXUp = 0x0100;
    private const uint MouseeventfHorizontalWheel = 0x1000;
    private const uint MouseeventfMoveNoCoalesce = 0x2000;
    private const uint MouseeventfVirtualDesk = 0x4000;
    private const uint MouseeventfAbsolute = 0x8000;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;
    private const int BoundsRefreshMilliseconds = 100;
    private const int PointerPreflightSettleMilliseconds = 75;

    private readonly IRobloxAutomation _automation;
    private readonly RobloxWindow _window;
    private readonly VirtualDesktop _desktop;
    private readonly HashSet<ManualKeyboardIdentity> _heldKeys = [];
    private readonly HashSet<ManualMouseButton> _heldButtons = [];
    private readonly System.Diagnostics.Stopwatch _lifetime =
        System.Diagnostics.Stopwatch.StartNew();
    private ClientBounds _bounds;
    private long _nextBoundsRefreshMilliseconds;
    private (int X, int Y)? _lastClientPosition;

    public WindowsManualInputSink(
        IRobloxAutomation automation,
        RobloxWindow window,
        ClientBounds bounds)
    {
        _automation = automation;
        _window = window;
        _bounds = bounds;
        _desktop = VirtualDesktop.Read();
        _nextBoundsRefreshMilliseconds =
            BoundsRefreshMilliseconds;
    }

    public void Send(ManualInputEvent input)
    {
        EnsureTarget();
        switch (input.Kind)
        {
            case ManualInputEventKind.KeyDown:
                SendKey(input, keyUp: false);
                _heldKeys.Add(
                    new ManualKeyboardIdentity(
                        checked((ushort)input.ScanCode),
                        input.ExtendedKey));
                break;
            case ManualInputEventKind.KeyUp:
                SendKey(input, keyUp: true);
                _heldKeys.Remove(
                    new ManualKeyboardIdentity(
                        checked((ushort)input.ScanCode),
                        input.ExtendedKey));
                break;
            case ManualInputEventKind.MouseMove:
                MoveTo(input);
                break;
            case ManualInputEventKind.MouseButtonDown:
                RequirePointer(input);
                SendMouseButton(input.MouseButton, keyUp: false);
                _heldButtons.Add(input.MouseButton);
                break;
            case ManualInputEventKind.MouseButtonUp:
                RequirePointer(input);
                SendMouseButton(input.MouseButton, keyUp: true);
                _heldButtons.Remove(input.MouseButton);
                break;
            case ManualInputEventKind.MouseWheel:
                RequirePointer(input);
                SendMouse(
                    NativeMethods.MouseeventfWheel,
                    unchecked((uint)input.WheelDelta));
                break;
            case ManualInputEventKind.MouseHorizontalWheel:
                RequirePointer(input);
                SendMouse(
                    MouseeventfHorizontalWheel,
                    unchecked((uint)input.WheelDelta));
                break;
            default:
                throw new InvalidDataException(
                    "The recording contains an unsupported input event.");
        }
    }

    public void Prepare(ManualInputRecording recording)
    {
        EnsureTarget();
        int clientX = recording.InitialClientX;
        int clientY = recording.InitialClientY;
        int screenX = checked(
            _bounds.X + clientX);
        int screenY = checked(
            _bounds.Y + clientY);
        int nudgeX =
            clientX < _bounds.Width - 1
                ? 1
                : -1;
        RegisteredCursorMotion.Move(
            screenX,
            screenY,
            nudgeX,
            "Windows could not establish the recorded pointer start.");
        _lastClientPosition =
            (clientX, clientY);
        VerifyPointer(
            clientX,
            clientY);
        Thread.Sleep(PointerPreflightSettleMilliseconds);
    }

    public void ReleaseHeldInputs()
    {
        Exception? firstFailure = null;
        foreach (ManualKeyboardIdentity key in _heldKeys.ToArray())
        {
            try
            {
                SendKeyboard(
                    key.ScanCode,
                    key.Extended,
                    keyUp: true);
            }
            catch (Exception error)
            {
                firstFailure ??= error;
            }
        }
        _heldKeys.Clear();

        foreach (ManualMouseButton button in _heldButtons.ToArray())
        {
            try
            {
                SendMouseButton(button, keyUp: true);
            }
            catch (Exception error)
            {
                firstFailure ??= error;
            }
        }
        _heldButtons.Clear();
        if (firstFailure is not null)
        {
            throw firstFailure;
        }
    }

    private void EnsureTarget()
    {
        if (!IsTargetForeground(_window))
        {
            throw new InvalidOperationException(
                "Roblox lost foreground focus during manual playback.");
        }
        if (_lifetime.ElapsedMilliseconds <
            _nextBoundsRefreshMilliseconds)
        {
            return;
        }

        ClientBounds current =
            _automation.GetClientBounds(_window);
        if (current.Width != RobloxClientProfile.Width ||
            current.Height != RobloxClientProfile.Height)
        {
            throw new InvalidOperationException(
                "The Roblox client changed size during manual playback.");
        }
        _bounds = current;
        _nextBoundsRefreshMilliseconds =
            _lifetime.ElapsedMilliseconds +
            BoundsRefreshMilliseconds;
    }

    internal static bool IsTargetForeground(
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

    private void RequirePointer(ManualInputEvent input)
    {
        (int X, int Y) target = (
            input.ClientX!.Value,
            input.ClientY!.Value);
        if (_lastClientPosition != target)
        {
            throw new InvalidOperationException(
                "Recorded pointer movement was missing before a mouse action.");
        }
        VerifyPointer(
            target.X,
            target.Y);
    }

    private void VerifyPointer(
        int clientX,
        int clientY)
    {
        if (!NativeMethods.GetCursorPos(
                out NativeMethods.Point pointer) ||
            Math.Abs(
                pointer.X -
                (_bounds.X + clientX)) > 1 ||
            Math.Abs(
                pointer.Y -
                (_bounds.Y + clientY)) > 1)
        {
            throw new InvalidOperationException(
                "Windows did not preserve the recorded pointer path before a mouse action.");
        }
    }

    private void MoveTo(ManualInputEvent input)
    {
        int screenX = checked(
            _bounds.X +
            input.ClientX!.Value);
        int screenY = checked(
            _bounds.Y +
            input.ClientY!.Value);
        int normalizedX = _desktop.NormalizeX(screenX);
        int normalizedY = _desktop.NormalizeY(screenY);
        SendMouse(
            NativeMethods.MouseeventfMove |
                MouseeventfMoveNoCoalesce |
                MouseeventfAbsolute |
                MouseeventfVirtualDesk,
            data: 0,
            normalizedX,
            normalizedY);
        _lastClientPosition = (
            input.ClientX.Value,
            input.ClientY.Value);
    }

    private static void SendKey(
        ManualInputEvent input,
        bool keyUp) =>
        SendKeyboard(
            checked((ushort)input.ScanCode),
            input.ExtendedKey,
            keyUp);

    private static void SendKeyboard(
        ushort scanCode,
        bool extended,
        bool keyUp)
    {
        uint flags = NativeMethods.KeyeventfScanCode;
        if (extended)
        {
            flags |= NativeMethods.KeyeventfExtendedKey;
        }
        if (keyUp)
        {
            flags |= NativeMethods.KeyeventfKeyUp;
        }
        NativeMethods.Input input = new()
        {
            Type = NativeMethods.InputKeyboard,
            Value = new NativeMethods.InputUnion
            {
                Keyboard = new NativeMethods.KeyboardInput
                {
                    ScanCode = scanCode,
                    Flags = flags,
                },
            },
        };
        Send(input);
    }

    private static void SendMouseButton(
        ManualMouseButton button,
        bool keyUp)
    {
        (uint flags, uint data) = button switch
        {
            ManualMouseButton.Left =>
                (keyUp
                    ? NativeMethods.MouseeventfLeftUp
                    : NativeMethods.MouseeventfLeftDown, 0u),
            ManualMouseButton.Right =>
                (keyUp
                    ? NativeMethods.MouseeventfRightUp
                    : NativeMethods.MouseeventfRightDown, 0u),
            ManualMouseButton.Middle =>
                (keyUp
                    ? MouseeventfMiddleUp
                    : MouseeventfMiddleDown, 0u),
            ManualMouseButton.X1 =>
                (keyUp
                    ? MouseeventfXUp
                    : MouseeventfXDown, XButton1),
            ManualMouseButton.X2 =>
                (keyUp
                    ? MouseeventfXUp
                    : MouseeventfXDown, XButton2),
            _ => throw new InvalidDataException(
                "The recording contains an invalid mouse button."),
        };
        SendMouse(flags, data);
    }

    private static void SendMouse(
        uint flags,
        uint data,
        int x = 0,
        int y = 0)
    {
        NativeMethods.Input input = new()
        {
            Type = NativeMethods.InputMouse,
            Value = new NativeMethods.InputUnion
            {
                Mouse = new NativeMethods.MouseInput
                {
                    Dx = x,
                    Dy = y,
                    MouseData = data,
                    Flags = flags,
                },
            },
        };
        Send(input);
    }

    private static void Send(NativeMethods.Input input)
    {
        NativeMethods.Input[] inputs = [input];
        if (NativeMethods.SendInput(
            1,
            inputs,
            Marshal.SizeOf<NativeMethods.Input>()) != 1)
        {
            throw new Win32Exception(
                "Windows rejected a recorded input event.");
        }
    }
}

internal readonly record struct ManualKeyboardIdentity(
    ushort ScanCode,
    bool Extended);

public readonly record struct ManualInputPlaybackSummary(
    string RecordingId,
    int SentEvents,
    int TotalEvents,
    long MaximumDriftMicroseconds,
    bool Succeeded);
