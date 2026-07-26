using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

internal sealed class UiScaleNormalizer
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan SettledHoldDelay =
        TimeSpan.FromSeconds(1);
    private const int MaximumFeedbackAttempts = 5;
    private const double StableMeasurementTolerance = 0.005;

    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly AccessibilityNavigationController _navigation;

    public UiScaleNormalizer(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
        _navigation = new AccessibilityNavigationController(
            automation,
            ValidateWindow,
            delay);
    }

    public async Task<bool> NormalizeAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        bool changed = false;
        await _navigation.RunEnabledAsync(
            window,
            async token =>
            {
                await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.RightArrow,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.Enter,
                    token).ConfigureAwait(false);
                GameSettingsPanelMatch initial =
                    await WaitForSettledPanelAsync(
                    window,
                    token).ConfigureAwait(false);
                changed =
                    Math.Abs(
                        initial.UiScale -
                        UiScaleFeedbackPolicy
                            .TargetRenderedScale) >
                    GameSettingsScreenDetector
                        .CanonicalScaleTolerance;
                if (!changed) return;

                await _delay(
                    SettledHoldDelay,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.LeftArrow,
                    token).ConfigureAwait(false);

                for (int index = 0; index < 7; index++)
                {
                    await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.DownArrow,
                        token).ConfigureAwait(false);
                }
                await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.Enter,
                    token).ConfigureAwait(false);
                await WaitForSettledPanelAsync(
                    window,
                        token).ConfigureAwait(false);
                await _delay(
                    SettledHoldDelay,
                        token).ConfigureAwait(false);
                await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.RightArrow,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.DownArrow,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.DownArrow,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.LeftArrow,
                    token).ConfigureAwait(false);
                double candidate =
                    UiScaleFeedbackPolicy.TargetRenderedScale;
                GameSettingsPanelMatch observed = default;
                for (int attempt = 1;
                     attempt <= MaximumFeedbackAttempts;
                     attempt++)
                {
                    await EnterScaleValueAsync(
                        window,
                        candidate,
                            token).ConfigureAwait(false);
                    observed = await WaitForSettledScaleAsync(
                        window,
                            token).ConfigureAwait(false);
                    if (Math.Abs(
                            observed.UiScale -
                            UiScaleFeedbackPolicy
                                .TargetRenderedScale) <=
                        GameSettingsScreenDetector
                            .CanonicalScaleTolerance)
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
            },
            cancellationToken).ConfigureAwait(false);

        await _navigation.RunEnabledAsync(
            window,
            async token =>
            {
                await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.RightArrow,
                    token).ConfigureAwait(false);
                await _navigation.TapAsync(
                    window,
                    RobloxKeyboardKey.Enter,
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return changed;
    }

    private async Task<GameSettingsPanelMatch>
        WaitForSettledPanelAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            _utcNow() + TimeSpan.FromSeconds(7);
        int stable = 0;
        while (_utcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateWindow(window);
            GameSettingsPanelMatch panel =
                GameSettingsScreenDetector.DetectPanel(
                    _automation.CaptureClient(window));
            stable =
                panel.Visible && panel.Settled
                    ? stable + 1
                    : 0;
            if (stable >= 2) return panel;
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "Anime Expeditions Settings did not settle after selecting Misc.");
    }

    private async Task EnterScaleValueAsync(
        RobloxWindow window,
        double value,
        CancellationToken cancellationToken)
    {
        await _navigation.TapAsync(
            window,
            RobloxKeyboardKey.Enter,
            cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < 8; index++)
        {
            await _navigation.TapAsync(
                window,
                RobloxKeyboardKey.Backspace,
                cancellationToken).ConfigureAwait(false);
        }
        foreach (char character in
                 UiScaleFeedbackPolicy.Format(value))
        {
            await _navigation.TapAsync(
                window,
                ScaleKey(character),
                cancellationToken).ConfigureAwait(false);
        }
        await _navigation.TapAsync(
            window,
            RobloxKeyboardKey.Enter,
            cancellationToken).ConfigureAwait(false);
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
