using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal readonly record struct KeyboardInputDescriptor(int VirtualKey, int ScanCode, bool Extended)
{
    private const uint MapVirtualKeyToScanCodeExtended = 4;

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
