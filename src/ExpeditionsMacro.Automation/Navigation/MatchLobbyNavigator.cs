using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Navigation;

namespace ExpeditionsMacro.Automation.Navigation;

public sealed class MatchLobbyNavigator
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);
    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly AccessibilityNavigationController
        _accessibility;

    public MatchLobbyNavigator(
        IRobloxAutomation automation)
        : this(
            automation,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token))
    {
    }

    internal MatchLobbyNavigator(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
        _accessibility =
            new AccessibilityNavigationController(
                automation,
                ValidateWindow,
                delay);
    }

    public async Task ReturnAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(detector);
        LobbyExitConfirmationMatch confirmation =
            await OpenConfirmationAsync(
                window,
                cancellationToken).ConfigureAwait(false);
        await ConfirmByClickAsync(
            window,
            confirmation,
            cancellationToken).ConfigureAwait(false);

        await RobloxLobbyReadinessGate.WaitAsync(
            token => CaptureAsync(window, token),
            detector.RecoveryState,
            TimeSpan.FromSeconds(75),
            PollInterval,
            cancellationToken,
            _utcNow,
            _delay).ConfigureAwait(false);
    }

    private async Task<LobbyExitConfirmationMatch>
        OpenConfirmationAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        LobbyExitConfirmationMatch last = default;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            await _accessibility.RunEnabledAsync(
                window,
                async token =>
                {
                    await _accessibility.TapAsync(
                        window,
                        RobloxKeyboardKey.RightArrow,
                        token).ConfigureAwait(false);
                    await _accessibility.TapAsync(
                        window,
                        RobloxKeyboardKey.RightArrow,
                        token).ConfigureAwait(false);
                    await _accessibility.TapAsync(
                        window,
                        RobloxKeyboardKey.Enter,
                        token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            last = await WaitForConfirmationAsync(
                window,
                TimeSpan.FromSeconds(4),
                cancellationToken).ConfigureAwait(false);
            if (last.Visible) return last;
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions did not open the verified Return to Lobby confirmation.");
    }

    private async Task ConfirmByClickAsync(
        RobloxWindow window,
        LobbyExitConfirmationMatch confirmation,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1;
             attempt <= 2;
             attempt++)
        {
            ValidateWindow(window);
            await _automation.ClickClientAsync(
                window,
                confirmation.ActionX,
                confirmation.ActionY,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForConfirmationClosedAsync(
                    window,
                    TimeSpan.FromSeconds(3),
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "The verified Return to Lobby confirmation did not accept its red action.");
    }

    private async Task<LobbyExitConfirmationMatch>
        WaitForConfirmationAsync(
        RobloxWindow window,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = _utcNow() + timeout;
        int stable = 0;
        LobbyExitConfirmationMatch last = default;
        while (_utcNow() < deadline)
        {
            last = LobbyExitConfirmationDetector.Detect(
                await CaptureAsync(
                    window,
                    cancellationToken).ConfigureAwait(false));
            stable = last.Visible ? stable + 1 : 0;
            if (stable >= 2) return last;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        return last;
    }

    private async Task<bool> WaitForConfirmationClosedAsync(
        RobloxWindow window,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = _utcNow() + timeout;
        int absent = 0;
        while (_utcNow() < deadline)
        {
            LobbyExitConfirmationMatch match =
                LobbyExitConfirmationDetector.Detect(
                    await CaptureAsync(
                        window,
                        cancellationToken).ConfigureAwait(false));
            absent = match.Visible ? 0 : absent + 1;
            if (absent >= 2) return true;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private Task<ImageFrame> CaptureAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateWindow(window);
        return Task.FromResult(
            _automation.CaptureClient(window));
    }

    private void ValidateWindow(
        RobloxWindow window)
    {
        RobloxWindow? current = _automation.FindWindow();
        if (current is null ||
            current.Value.Handle != window.Handle)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox closed or changed while returning to the lobby.");
        }
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while returning to the lobby.");
        }
        var bounds = _automation.GetClientBounds(window);
        if (bounds.Width != 808 ||
            bounds.Height != 611)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox changed size while returning to the lobby.");
        }
    }
}
