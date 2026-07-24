using ExpeditionsMacro.Core.Abstractions;

namespace ExpeditionsMacro.Windows;

public sealed partial class WindowsRobloxAutomation
{
    public Task TapShiftLockKeyAsync(
        RobloxWindow window,
        int virtualKey,
        CancellationToken cancellationToken) =>
        _keyboard.TapShiftLockKeyAsync(
            window,
            virtualKey,
            cancellationToken);

    public Task TapLetterKeyAsync(
        RobloxWindow window,
        char key,
        CancellationToken cancellationToken) =>
        _keyboard.TapLetterKeyAsync(
            window,
            key,
            cancellationToken);

    public Task HoldLetterKeyAsync(
        RobloxWindow window,
        char key,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        _keyboard.HoldLetterKeyAsync(
            window,
            key,
            holdMilliseconds,
            cancellationToken);

    public Task TapUnitKeyAsync(
        RobloxWindow window,
        int unitKey,
        int holdMilliseconds,
        CancellationToken cancellationToken) =>
        _keyboard.TapUnitKeyAsync(
            window,
            unitKey,
            holdMilliseconds,
            cancellationToken);
}
