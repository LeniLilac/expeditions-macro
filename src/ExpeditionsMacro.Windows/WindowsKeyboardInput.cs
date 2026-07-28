using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Windows.Interop;

namespace ExpeditionsMacro.Windows;

internal sealed class WindowsKeyboardInput
{
    private readonly Func<RobloxWindow, bool> _focus;
    private readonly Action<WindowsAutomationTrace> _trace;
    private readonly Action<byte, byte, uint, nuint>
        _sendKeyboardEvent;

    public WindowsKeyboardInput(
        Func<RobloxWindow, bool> focus,
        Action<WindowsAutomationTrace> trace,
        Action<byte, byte, uint, nuint>? sendKeyboardEvent = null)
    {
        _focus = focus;
        _trace = trace;
        _sendKeyboardEvent =
            sendKeyboardEvent ??
            NativeMethods.keybd_event;
    }

    public Task TapShiftLockKeyAsync(RobloxWindow window, int virtualKey, CancellationToken cancellationToken) =>
        PulseKeyAsync(window, KeyboardInputDescriptor.FromShiftLockVirtualKey(virtualKey), 70, cancellationToken);

    public Task TapLetterKeyAsync(RobloxWindow window, char key, CancellationToken cancellationToken)
        => HoldLetterKeyAsync(
            window,
            key,
            holdMilliseconds: 70,
            cancellationToken);

    public Task TapKeyboardKeyAsync(
        RobloxWindow window,
        RobloxKeyboardKey key,
        CancellationToken cancellationToken) =>
        PulseKeyAsync(
            window,
            KeyboardInputDescriptor.FromRobloxKey(key),
            holdMilliseconds: 70,
            cancellationToken);

    public Task HoldLetterKeyAsync(
        RobloxWindow window,
        char key,
        int holdMilliseconds,
        CancellationToken cancellationToken)
    {
        char normalized = char.ToUpperInvariant(key);
        if (!char.IsAsciiLetter(normalized)) throw new ArgumentOutOfRangeException(nameof(key), "The Roblox key must be A through Z.");
        if (holdMilliseconds is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holdMilliseconds));
        }
        int virtualKey = normalized;
        int scanCode = checked((int)NativeMethods.MapVirtualKey((uint)virtualKey, 0));
        return PulseKeyAsync(
            window,
            new KeyboardInputDescriptor(
                virtualKey,
                scanCode,
                false),
            holdMilliseconds,
            cancellationToken);
    }

    public Task HoldKeyAsync(
        RobloxWindow window,
        int virtualKey,
        int holdMilliseconds,
        CancellationToken cancellationToken)
    {
        if (holdMilliseconds is <
                AutomationKeyPress.MinimumHoldMilliseconds or >
                AutomationKeyPress.MaximumHoldMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(holdMilliseconds));
        }
        return PulseKeyAsync(
            window,
            KeyboardInputDescriptor.FromAutomationVirtualKey(
                virtualKey),
            holdMilliseconds,
            cancellationToken);
    }

    public Task<TResult> RunWithKeyHeldAsync<TResult>(
        RobloxWindow window,
        int virtualKey,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunWithKeyHeldAsync(
            window,
            KeyboardInputDescriptor.FromAutomationVirtualKey(
                virtualKey),
            action,
            holdMilliseconds: null,
            cancellationToken);
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
        await RunWithKeyHeldAsync(
                window,
                key,
                async token =>
                {
                    await Task.Delay(
                            holdMilliseconds,
                            token)
                        .ConfigureAwait(false);
                    return true;
                },
                holdMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<TResult> RunWithKeyHeldAsync<TResult>(
        RobloxWindow window,
        KeyboardInputDescriptor key,
        Func<CancellationToken, Task<TResult>> action,
        int? holdMilliseconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_focus(window)) throw new InvalidOperationException("Windows could not focus Roblox.");
        uint downFlags = key.Extended ? NativeMethods.KeyeventfExtendedKey : 0;
        cancellationToken.ThrowIfCancellationRequested();
        _sendKeyboardEvent(
            (byte)key.VirtualKey,
            (byte)key.ScanCode,
            downFlags,
            0);
        Record(key, "key_down", holdMilliseconds, downFlags);
        try
        {
            return await action(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            uint upFlags = NativeMethods.KeyeventfKeyUp | downFlags;
            _sendKeyboardEvent(
                (byte)key.VirtualKey,
                (byte)key.ScanCode,
                upFlags,
                0);
            Record(key, "key_up", holdMilliseconds, upFlags);
        }
    }

    private void Record(KeyboardInputDescriptor key, string action, int? holdMilliseconds, uint flags) =>
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
