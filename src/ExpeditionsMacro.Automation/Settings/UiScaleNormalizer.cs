using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

internal sealed class UiScaleNormalizer
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan NavigationTimeout =
        TimeSpan.FromSeconds(7);
    private static readonly TimeSpan InputFocusSettleDelay =
        TimeSpan.FromMilliseconds(2500);
    private const int MaximumFeedbackAttempts = 5;
    private const double StableMeasurementTolerance = 0.005;

    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly SettingsPanelNavigator _settingsPanel;

    public UiScaleNormalizer(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
        _settingsPanel = new SettingsPanelNavigator(
            automation,
            ValidateWindow,
            utcNow,
            delay);
    }

    public async Task<bool> NormalizeAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        GameSettingsPanelMatch initial =
            await _settingsPanel.OpenAsync(
                window,
                cancellationToken).ConfigureAwait(false);
        bool changed =
            !GameSettingsScreenDetector
                .IsCanonicalUiScale(
                    initial.UiScale);
        if (changed)
        {
            await SelectMiscellaneousAsync(
                window,
                cancellationToken).ConfigureAwait(false);
            double candidate =
                UiScaleFeedbackPolicy.TargetRenderedScale;
            GameSettingsPanelMatch observed = default;
            for (int attempt = 1;
                 attempt <= MaximumFeedbackAttempts;
                 attempt++)
            {
                await SelectUiScaleInputAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                await EnterScaleValueAsync(
                    window,
                    candidate,
                    cancellationToken).ConfigureAwait(false);
                observed = await WaitForSettledScaleAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
                if (GameSettingsScreenDetector
                        .IsCanonicalUiScale(
                            observed.UiScale))
                {
                    break;
                }

                double corrected =
                    UiScaleFeedbackPolicy.Correct(
                        candidate,
                        observed.UiScale);
                if (corrected == candidate)
                {
                    throw new InvalidOperationException(
                        $"Anime Expeditions cannot reach the canonical rendered UI size on this device. Numeric UI Scale {candidate:0.00} renders as {observed.UiScale:0.00}, and the supported range is {UiScaleFeedbackPolicy.MinimumValue:0.00} to {UiScaleFeedbackPolicy.MaximumValue:0.00}.");
                }
                candidate = corrected;
                if (attempt == MaximumFeedbackAttempts)
                {
                    throw new InvalidOperationException(
                        $"Anime Expeditions UI Scale did not converge after {MaximumFeedbackAttempts} feedback adjustments. The last numeric value {candidate:0.00} followed a rendered measurement of {observed.UiScale:0.00}.");
                }
            }
        }

        await _settingsPanel.CloseAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        return changed;
    }

    private async Task SelectMiscellaneousAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        if (GameSettingsNavigationDetector
                .DetectSelectedPage(Capture(window)).Page ==
            GameSettingsPage.Miscellaneous)
        {
            return;
        }

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            (int X, int Y) action =
                await WaitForStablePageActionAsync(
                    window,
                    cancellationToken).ConfigureAwait(false);
            await _automation.ClickClientAsync(
                window,
                action.X,
                action.Y,
                cancellationToken).ConfigureAwait(false);
            if (await WaitForSelectedPageAsync(
                    window,
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions did not open the Misc settings page. No UI Scale value was entered.");
    }

    private async Task<(int X, int Y)>
        WaitForStablePageActionAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        StableNavigationActionTracker<GameSettingsPage>
            tracker = new();
        ObservationWaitBudget budget = new(
            NavigationTimeout,
            minimumObservations: 2,
            _utcNow);
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameSettingsNavigationActionMatch match =
                GameSettingsNavigationDetector.DetectPageAction(
                    Capture(window),
                    GameSettingsPage.Miscellaneous);
            (int X, int Y)? stable = tracker.Update(
                match.Available
                    ? GameSettingsPage.Miscellaneous
                    : GameSettingsPage.None,
                match.Available
                    ? (match.ActionX, match.ActionY)
                    : null);
            if (stable is { } action)
            {
                return action;
            }
            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions did not expose a stable Misc settings action. No UI Scale value was entered.");
    }

    private async Task<bool> WaitForSelectedPageAsync(
        RobloxWindow window,
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
            bool selected =
                GameSettingsNavigationDetector
                    .DetectSelectedPage(
                        Capture(window)).Page ==
                GameSettingsPage.Miscellaneous;
            stable = selected ? stable + 1 : 0;
            if (stable >= 2)
            {
                return true;
            }
            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private async Task SelectUiScaleInputAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        (int X, int Y) action =
            await WaitForStableScaleInputAsync(
                window,
                cancellationToken).ConfigureAwait(false);
        await _automation.ClickClientAsync(
            window,
            action.X,
            action.Y,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            InputFocusSettleDelay,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(int X, int Y)>
        WaitForStableScaleInputAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        StableNavigationActionTracker<string> tracker =
            new();
        ObservationWaitBudget budget = new(
            NavigationTimeout,
            minimumObservations: 2,
            _utcNow);
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameSettingsUiScaleInputMatch match =
                GameSettingsNavigationDetector
                    .DetectUiScaleInput(Capture(window));
            (int X, int Y)? stable = tracker.Update(
                match.Available ? "ui_scale" : null,
                match.Available
                    ? (match.ActionX, match.ActionY)
                    : null);
            if (stable is { } action)
            {
                return action;
            }
            budget.MarkObserved();
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions did not expose a stable UI Scale input.");
    }

    private async Task EnterScaleValueAsync(
        RobloxWindow window,
        double value,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < 8; index++)
        {
            await TapAsync(
                window,
                RobloxKeyboardKey.Backspace,
                cancellationToken).ConfigureAwait(false);
        }
        foreach (char character in
                 UiScaleFeedbackPolicy.Format(value))
        {
            await TapAsync(
                window,
                ScaleKey(character),
                cancellationToken).ConfigureAwait(false);
        }
        await TapAsync(
            window,
            RobloxKeyboardKey.Enter,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task TapAsync(
        RobloxWindow window,
        RobloxKeyboardKey key,
        CancellationToken cancellationToken)
    {
        ValidateWindow(window);
        await _automation.TapKeyboardKeyAsync(
            window,
            key,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            TimeSpan.FromMilliseconds(500),
            cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame Capture(RobloxWindow window)
    {
        ValidateWindow(window);
        return _automation.CaptureClient(window);
    }

    private async Task<GameSettingsPanelMatch>
        WaitForSettledScaleAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            _utcNow() + TimeSpan.FromSeconds(7);
        int stable = 0;
        GameSettingsPanelMatch last = default;
        double previousScale = double.NaN;
        while (_utcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateWindow(window);
            last = GameSettingsScreenDetector.DetectPanel(
                _automation.CaptureClient(window));
            bool expected =
                last.Visible &&
                last.Settled;
            if (!expected)
            {
                stable = 0;
                previousScale = double.NaN;
            }
            else
            {
                stable =
                    double.IsFinite(previousScale) &&
                    Math.Abs(
                        last.UiScale -
                        previousScale) <=
                    StableMeasurementTolerance
                        ? stable + 1
                        : 1;
                previousScale = last.UiScale;
            }
            if (stable >= 2) return last;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            $"Anime Expeditions UI Scale did not settle for feedback measurement (last detected {last.UiScale:0.00}).");
    }

    private static RobloxKeyboardKey ScaleKey(
        char character) => character switch
        {
            '0' => RobloxKeyboardKey.Digit0,
            '1' => RobloxKeyboardKey.Digit1,
            '2' => RobloxKeyboardKey.Digit2,
            '3' => RobloxKeyboardKey.Digit3,
            '4' => RobloxKeyboardKey.Digit4,
            '5' => RobloxKeyboardKey.Digit5,
            '6' => RobloxKeyboardKey.Digit6,
            '7' => RobloxKeyboardKey.Digit7,
            '8' => RobloxKeyboardKey.Digit8,
            '9' => RobloxKeyboardKey.Digit9,
            '.' => RobloxKeyboardKey.Period,
            _ => throw new InvalidOperationException(
                $"Unsupported UI Scale character '{character}'."),
        };

    private void ValidateWindow(RobloxWindow window)
    {
        RobloxWindow? current = _automation.FindWindow();
        if (current is null ||
            current.Value.Handle != window.Handle)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox closed or changed while adjusting UI Scale.");
        }
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox while adjusting UI Scale.");
        }
        var bounds = _automation.GetClientBounds(window);
        if (bounds.Width !=
                GameSettingsScreenDetector.ClientWidth ||
            bounds.Height !=
                GameSettingsScreenDetector.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox changed size while adjusting UI Scale.");
        }
    }
}
