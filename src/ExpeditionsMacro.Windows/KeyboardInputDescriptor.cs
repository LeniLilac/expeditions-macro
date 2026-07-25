using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal readonly record struct KeyboardInputDescriptor(int VirtualKey, int ScanCode, bool Extended)
{
    private const uint MapVirtualKeyToScanCodeExtended = 4;

    public static KeyboardInputDescriptor FromRobloxKey(
        RobloxKeyboardKey key) => key switch
        {
            RobloxKeyboardKey.Backspace =>
                new KeyboardInputDescriptor(0x08, 0x0E, false),
            RobloxKeyboardKey.Enter =>
                new KeyboardInputDescriptor(0x0D, 0x1C, false),
            RobloxKeyboardKey.Backslash =>
                new KeyboardInputDescriptor(0xDC, 0x2B, false),
            RobloxKeyboardKey.Digit0 =>
                new KeyboardInputDescriptor(0x30, 0x0B, false),
            RobloxKeyboardKey.Digit1 =>
                new KeyboardInputDescriptor(0x31, 0x02, false),
            RobloxKeyboardKey.Digit2 =>
                new KeyboardInputDescriptor(0x32, 0x03, false),
            RobloxKeyboardKey.Digit3 =>
                new KeyboardInputDescriptor(0x33, 0x04, false),
            RobloxKeyboardKey.Digit4 =>
                new KeyboardInputDescriptor(0x34, 0x05, false),
            RobloxKeyboardKey.Digit5 =>
                new KeyboardInputDescriptor(0x35, 0x06, false),
            RobloxKeyboardKey.Digit6 =>
                new KeyboardInputDescriptor(0x36, 0x07, false),
            RobloxKeyboardKey.Digit7 =>
                new KeyboardInputDescriptor(0x37, 0x08, false),
            RobloxKeyboardKey.Digit8 =>
                new KeyboardInputDescriptor(0x38, 0x09, false),
            RobloxKeyboardKey.Digit9 =>
                new KeyboardInputDescriptor(0x39, 0x0A, false),
            RobloxKeyboardKey.Period =>
                new KeyboardInputDescriptor(0xBE, 0x34, false),
            RobloxKeyboardKey.LeftArrow =>
                new KeyboardInputDescriptor(0x25, 0x4B, true),
            RobloxKeyboardKey.RightArrow =>
                new KeyboardInputDescriptor(0x27, 0x4D, true),
            RobloxKeyboardKey.DownArrow =>
                new KeyboardInputDescriptor(0x28, 0x50, true),
            _ => throw new ArgumentOutOfRangeException(
                nameof(key),
                key,
                "The Roblox keyboard key is not supported."),
        };

    public static KeyboardInputDescriptor FromShiftLockVirtualKey(int virtualKey)
    {
        if (!KeyboardKey.IsSupportedShiftLockKey(virtualKey))
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey), "The configured Shift Lock key is not supported.");
        }

        uint mapped = NativeMethods.MapVirtualKey((uint)virtualKey, MapVirtualKeyToScanCodeExtended);
        int scanCode = (int)(mapped & 0xFF);
        if (scanCode == 0) throw new InvalidOperationException("Windows could not resolve the configured Shift Lock key to a physical scan code.");
        bool extended = (mapped & 0xFF00) is 0xE000 or 0xE100;
        return new KeyboardInputDescriptor(virtualKey, scanCode, extended);
    }
}
