using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal sealed class WindowsKeyboardInput
{
    private readonly Func<RobloxWindow, bool> _focus;
    private readonly Action<WindowsAutomationTrace> _trace;

    public WindowsKeyboardInput(Func<RobloxWindow, bool> focus, Action<WindowsAutomationTrace> trace)
    {
        _focus = focus;
        _trace = trace;
    }

    public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) =>
        PulseKeyAsync(window, KeyboardInputDescriptor.FromShiftLockVirtualKey(virtualKey), 70, cancellationToken);

    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken)
    {
        char normalized = char.ToUpperInvariant(key);
        if (!char.IsAsciiLetter(normalized)) throw new ArgumentOutOfRangeException(nameof(key), "The Roblox key must be A through Z.");
        int virtualKey = normalized;
        int scanCode = checked((int)NativeMethods.MapVirtualKey((uint)virtualKey, 0));
        return PulseKeyAsync(window, new KeyboardInputDescriptor(virtualKey, scanCode, false), 70, cancellationToken);
    }

    public Task TapUnitKeyAsync(RobloxWindow window, int unitKey, int holdMilliseconds, CancellationToken cancellationToken)
    {
        if (unitKey is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(unitKey));
        int scanCode = unitKey == 0 ? 0x0B : 0x01 + unitKey;
        return PulseKeyAsync(window, new KeyboardInputDescriptor(0x30 + unitKey, scanCode, false), holdMilliseconds, cancellationToken);
    }

    private async Task PulseKeyAsync(
        RobloxWindow window,
        KeyboardInputDescriptor key,
        int holdMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!_focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        uint downFlags = key.Extended ? NativeMethods.KeyeventfExtendedKey : 0;
        NativeMethods.keybd_event((byte)key.VirtualKey, (byte)key.ScanCode, downFlags, 0);
        Record(key, "key_down", holdMilliseconds, downFlags);
        try
        {
            await Task.Delay(holdMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            uint upFlags = NativeMethods.KeyeventfKeyUp | downFlags;
            NativeMethods.keybd_event((byte)key.VirtualKey, (byte)key.ScanCode, upFlags, 0);
            Record(key, "key_up", holdMilliseconds, upFlags);
        }
    }

    private void Record(KeyboardInputDescriptor key, string action, int holdMilliseconds, uint flags) =>
        _trace(new WindowsAutomationTrace(
            DateTimeOffset.UtcNow,
            "keyboard",
            action,
            VirtualKey: key.VirtualKey,
            ScanCode: key.ScanCode,
            HoldMilliseconds: holdMilliseconds,
            Flags: flags,
            Extended: key.Extended));
}
