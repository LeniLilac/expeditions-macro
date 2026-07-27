using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal static class ManualInputEventFactory
{
    private const uint LlkhfExtended = 0x01;
    private const uint LlkhfInjected = 0x10;
    private const uint LlkhfLowerIntegrityInjected = 0x02;
    private const uint LlmhfInjected = 0x01;
    private const uint LlmhfLowerIntegrityInjected = 0x02;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;
    private const uint WmMouseHorizontalWheel = 0x020E;

    public static bool TryCreateKeyboard(
        uint message,
        NativeMethods.KeyboardHookData data,
        long offsetMicroseconds,
        IReadOnlySet<int> ignoredVirtualKeys,
        out ManualInputEvent? input)
    {
        input = null;
        if ((data.Flags &
                (LlkhfInjected |
                    LlkhfLowerIntegrityInjected)) != 0 ||
            ignoredVirtualKeys.Contains(
                checked((int)data.VirtualKey)))
        {
            return false;
        }

        ManualInputEventKind kind = message switch
        {
            NativeMethods.WmKeyDown or
                NativeMethods.WmSysKeyDown =>
                ManualInputEventKind.KeyDown,
            NativeMethods.WmKeyUp or
                NativeMethods.WmSysKeyUp =>
                ManualInputEventKind.KeyUp,
            _ => ManualInputEventKind.Unknown,
        };
        if (kind == ManualInputEventKind.Unknown ||
            data.ScanCode == 0)
        {
            return false;
        }

        input = new ManualInputEvent
        {
            OffsetMicroseconds = offsetMicroseconds,
            Kind = kind,
            VirtualKey = checked((int)data.VirtualKey),
            ScanCode = checked((int)data.ScanCode),
            ExtendedKey = (data.Flags & LlkhfExtended) != 0,
        };
        return true;
    }

    public static MouseObservation CreateMouse(
        uint message,
        NativeMethods.MouseHookData data,
        ClientBounds bounds,
        long offsetMicroseconds)
    {
        if ((data.Flags &
                (LlmhfInjected |
                    LlmhfLowerIntegrityInjected)) != 0)
        {
            return MouseObservation.Ignored;
        }

        ManualInputEventKind kind;
        ManualMouseButton button = ManualMouseButton.None;
        int wheelDelta = 0;
        switch (message)
        {
            case NativeMethods.WmMouseMove:
                kind = ManualInputEventKind.MouseMove;
                break;
            case NativeMethods.WmLButtonDown:
                kind = ManualInputEventKind.MouseButtonDown;
                button = ManualMouseButton.Left;
                break;
            case NativeMethods.WmLButtonUp:
                kind = ManualInputEventKind.MouseButtonUp;
                button = ManualMouseButton.Left;
                break;
            case WmRButtonDown:
                kind = ManualInputEventKind.MouseButtonDown;
                button = ManualMouseButton.Right;
                break;
            case WmRButtonUp:
                kind = ManualInputEventKind.MouseButtonUp;
                button = ManualMouseButton.Right;
                break;
            case WmMButtonDown:
                kind = ManualInputEventKind.MouseButtonDown;
                button = ManualMouseButton.Middle;
                break;
            case WmMButtonUp:
                kind = ManualInputEventKind.MouseButtonUp;
                button = ManualMouseButton.Middle;
                break;
            case WmXButtonDown:
                kind = ManualInputEventKind.MouseButtonDown;
                button = XButton(data.MouseData);
                break;
            case WmXButtonUp:
                kind = ManualInputEventKind.MouseButtonUp;
                button = XButton(data.MouseData);
                break;
            case WmMouseWheel:
                kind = ManualInputEventKind.MouseWheel;
                wheelDelta = SignedHighWord(data.MouseData);
                break;
            case WmMouseHorizontalWheel:
                kind = ManualInputEventKind.MouseHorizontalWheel;
                wheelDelta = SignedHighWord(data.MouseData);
                break;
            default:
                return MouseObservation.Ignored;
        }

        (int X, int Y)? relative =
            bounds.ToRelative(
                data.Position.X,
                data.Position.Y);
        if (relative is null)
        {
            return MouseObservation.OutsideClient;
        }

        return new MouseObservation(
            new ManualInputEvent
            {
                OffsetMicroseconds = offsetMicroseconds,
                Kind = kind,
                ClientX = relative.Value.X,
                ClientY = relative.Value.Y,
                MouseButton = button,
                WheelDelta = wheelDelta,
            },
            false);
    }

    private static ManualMouseButton XButton(uint mouseData) =>
        (mouseData >> 16) switch
        {
            1 => ManualMouseButton.X1,
            2 => ManualMouseButton.X2,
            _ => ManualMouseButton.None,
        };

    private static int SignedHighWord(uint value) =>
        unchecked((short)(value >> 16));
}

internal readonly record struct MouseObservation(
    ManualInputEvent? Input,
    bool IsOutsideClient)
{
    public static MouseObservation Ignored => new(null, false);

    public static MouseObservation OutsideClient => new(null, true);
}
