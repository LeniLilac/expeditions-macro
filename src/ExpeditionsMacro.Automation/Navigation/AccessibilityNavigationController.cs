using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Navigation;

internal sealed class AccessibilityNavigationController
{
    internal static readonly TimeSpan KeyTapDelay =
        TimeSpan.FromMilliseconds(500);

    private readonly IRobloxAutomation _automation;
    private readonly Action<RobloxWindow> _validateWindow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;

    public AccessibilityNavigationController(
        IRobloxAutomation automation,
        Action<RobloxWindow> validateWindow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _validateWindow = validateWindow;
        _delay = delay;
    }

    public async Task RunEnabledAsync(
        RobloxWindow window,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        bool enabled = false;
        try
        {
            await TapAsync(
                window,
                RobloxKeyboardKey.Backslash,
                cancellationToken).ConfigureAwait(false);
            enabled = true;
            await action(cancellationToken).ConfigureAwait(false);
            await TapAsync(
                window,
                RobloxKeyboardKey.Backslash,
                cancellationToken).ConfigureAwait(false);
            enabled = false;
        }
        finally
        {
            if (enabled)
            {
                try
                {
                    await _automation.TapKeyboardKeyAsync(
                        window,
                        RobloxKeyboardKey.Backslash,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the owned workflow error. Individual key taps
                    // release their physical key in their own finally path.
                }
            }
        }
    }

    public async Task TapAsync(
        RobloxWindow window,
        RobloxKeyboardKey key,
        CancellationToken cancellationToken)
    {
        _validateWindow(window);
        await _automation.TapKeyboardKeyAsync(
            window,
            key,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            KeyTapDelay,
            cancellationToken).ConfigureAwait(false);
    }
}
