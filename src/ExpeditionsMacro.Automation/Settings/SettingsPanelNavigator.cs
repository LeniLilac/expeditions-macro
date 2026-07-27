using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

public sealed class RobloxSettingsButtonUnavailableException(
    string message) : InvalidOperationException(message);

internal sealed class SettingsPanelNavigator
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan NavigationTimeout =
        TimeSpan.FromSeconds(7);

    private readonly IRobloxAutomation _automation;
    private readonly Action<RobloxWindow> _validateWindow;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;

    public SettingsPanelNavigator(
        IRobloxAutomation automation,
        Action<RobloxWindow> validateWindow,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _validateWindow = validateWindow;
        _utcNow = utcNow;
        _delay = delay;
    }

    public async Task<GameSettingsPanelMatch> OpenAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        GameSettingsPanelMatch existing =
            GameSettingsScreenDetector.DetectPanel(
                Capture(window));
        if (existing.Visible && existing.Settled)
        {
            return existing;
        }

        RobloxSettingsButtonMatch button =
            await WaitForStableButtonAsync(
                window,
                RobloxSettingsButtonState.Closed,
                cancellationToken).ConfigureAwait(false);
        await _automation.ClickClientAsync(
            window,
            button.ActionX,
            button.ActionY,
            cancellationToken).ConfigureAwait(false);
        return await WaitForOpenAsync(
            window,
            button.ActionX,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        RobloxSettingsButtonMatch button =
            await WaitForStableButtonAsync(
                window,
                RobloxSettingsButtonState.Selected,
                cancellationToken).ConfigureAwait(false);
        await _automation.ClickClientAsync(
            window,
            button.ActionX,
            button.ActionY,
            cancellationToken).ConfigureAwait(false);
        await WaitForClosedAsync(
            window,
            button.ActionX,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RobloxSettingsButtonMatch>
        WaitForStableButtonAsync(
        RobloxWindow window,
        RobloxSettingsButtonState expectedState,
        CancellationToken cancellationToken)
    {
        StableNavigationActionTracker<
            RobloxSettingsButtonState> tracker = new();
        ObservationWaitBudget budget = new(
            NavigationTimeout,
            minimumObservations: 2,
            _utcNow);
        RobloxSettingsButtonMatch last = default;
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = RobloxSettingsButtonDetector.Detect(
                Capture(window));
            (int X, int Y)? stable = tracker.Update(
                last.State == expectedState
                    ? expectedState
                    : RobloxSettingsButtonState.None,
                last.State == expectedState
                    ? (last.ActionX, last.ActionY)
                    : null);
            if (stable is { } action)
            {
                return last with
                {
                    ActionX = action.X,
                    ActionY = action.Y,
                };
            }

            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxSettingsButtonUnavailableException(
            expectedState ==
                RobloxSettingsButtonState.Closed
                ? "The Roblox Settings gear could not be recognized in the closed top bar. Close any Roblox menu or overlay and try again. Both top bars with and without voice chat are supported."
                : "The selected Roblox Settings gear could not be recognized, so Settings was left open. Close any Roblox menu or overlay and try again.");
    }

    private async Task<GameSettingsPanelMatch>
        WaitForOpenAsync(
        RobloxWindow window,
        int actionX,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            NavigationTimeout,
            minimumObservations: 2,
            _utcNow);
        int stable = 0;
        GameSettingsPanelMatch lastPanel = default;
        while (budget.ShouldObserve(
                   confirmationPending: stable == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = Capture(window);
            lastPanel =
                GameSettingsScreenDetector.DetectPanel(frame);
            RobloxSettingsButtonMatch button =
                RobloxSettingsButtonDetector.Detect(frame);
            bool open =
                lastPanel.Visible &&
                lastPanel.Settled &&
                button.State ==
                    RobloxSettingsButtonState.Selected &&
                button.ActionX == actionX;
            stable = open ? stable + 1 : 0;
            if (stable >= 2)
            {
                return lastPanel;
            }

            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "The detected Roblox Settings gear was clicked, but Anime Expeditions Settings did not finish opening.");
    }

    private async Task WaitForClosedAsync(
        RobloxWindow window,
        int actionX,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            NavigationTimeout,
            minimumObservations: 2,
            _utcNow);
        int stable = 0;
        while (budget.ShouldObserve(
                   confirmationPending: stable == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = Capture(window);
            GameSettingsPanelMatch panel =
                GameSettingsScreenDetector.DetectPanel(frame);
            RobloxSettingsButtonMatch button =
                RobloxSettingsButtonDetector.Detect(frame);
            bool closed =
                !panel.Visible &&
                panel.CloseX == 0 &&
                button.State ==
                    RobloxSettingsButtonState.Closed &&
                button.ActionX == actionX;
            stable = closed ? stable + 1 : 0;
            if (stable >= 2)
            {
                return;
            }

            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "The selected Roblox Settings gear was clicked, but Anime Expeditions Settings did not finish closing.");
    }

    private ImageFrame Capture(
        RobloxWindow window)
    {
        _validateWindow(window);
        return _automation.CaptureClient(window);
    }
}
