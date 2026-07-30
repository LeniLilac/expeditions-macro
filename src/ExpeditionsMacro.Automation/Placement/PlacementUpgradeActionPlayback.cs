using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Placement;

namespace ExpeditionsMacro.Automation.Placement;

internal sealed class PlacementUpgradeActionPlayback(
    IRobloxAutomation automation,
    Func<DateTimeOffset>? utcNow = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    private const int PollMilliseconds = 250;
    private const int RequiredStableFrames = 2;
    private const int MissingPanelLimit = 2;
    private const int MaximumObservations = 722;
    private static readonly TimeSpan MaximumWait =
        TimeSpan.FromMinutes(3);
    private readonly Func<DateTimeOffset> _utcNow =
        utcNow ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay =
        delay ?? ((duration, token) =>
            Task.Delay(duration, token));

    public async Task ApplyAsync(
        RobloxWindow window,
        char upgradeKey,
        int pressCount,
        int actionIntervalMilliseconds,
        bool requireReadiness,
        int stepNumber,
        int stepCount,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        if (!requireReadiness)
        {
            status?.Invoke(
                $"Step {stepNumber}/{stepCount}: advanced mode is pressing Upgrade Unit {pressCount} time(s) without readiness checks.");
            await TapRepeatedAsync(
                    window,
                    upgradeKey,
                    pressCount,
                    actionIntervalMilliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        for (int press = 0; press < pressCount; press++)
        {
            UpgradeUnitReadinessState state =
                await WaitForActionableStateAsync(
                        window,
                        stepNumber,
                        stepCount,
                        press + 1,
                        pressCount,
                        status,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (state == UpgradeUnitReadinessState.Maxed)
            {
                status?.Invoke(
                    $"Step {stepNumber}/{stepCount}: the unit is Maxed; skipped the remaining {pressCount - press} Upgrade Unit press(es).");
                return;
            }

            EnsureFocus(window);
            status?.Invoke(
                $"Step {stepNumber}/{stepCount}: Upgrade Unit is affordable; sending press {press + 1}/{pressCount}.");
            await automation.TapLetterKeyAsync(
                    window,
                    upgradeKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (press + 1 < pressCount)
            {
                await _delay(
                        TimeSpan.FromMilliseconds(
                            Math.Max(
                                actionIntervalMilliseconds,
                                100)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<UpgradeUnitReadinessState>
        WaitForActionableStateAsync(
        RobloxWindow window,
        int stepNumber,
        int stepCount,
        int pressNumber,
        int pressCount,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline =
            _utcNow() + MaximumWait;
        UpgradeUnitReadinessState stableState =
            UpgradeUnitReadinessState.Unknown;
        int stableFrames = 0;
        int missingPanelFrames = 0;
        bool reportedUnaffordable = false;

        for (int observation = 0;
             observation < MaximumObservations;
             observation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureFocus(window);
            UpgradeUnitReadinessMatch match =
                UpgradeUnitReadinessDetector.Detect(
                    automation.CaptureClient(window));
            if (!match.PanelVisible)
            {
                missingPanelFrames++;
                stableFrames = 0;
                stableState =
                    UpgradeUnitReadinessState.Unknown;
                if (missingPanelFrames >=
                    MissingPanelLimit)
                {
                    throw new RobloxUiUnavailableException(
                        "The selected-unit panel disappeared while waiting for Upgrade Unit readiness.");
                }
            }
            else
            {
                missingPanelFrames = 0;
                switch (match.State)
                {
                    case UpgradeUnitReadinessState.Affordable:
                    case UpgradeUnitReadinessState.Maxed:
                        if (stableState == match.State)
                        {
                            stableFrames++;
                        }
                        else
                        {
                            stableState = match.State;
                            stableFrames = 1;
                        }
                        if (stableFrames >=
                            RequiredStableFrames)
                        {
                            return match.State;
                        }
                        break;
                    case UpgradeUnitReadinessState.Unaffordable:
                        stableFrames = 0;
                        stableState = match.State;
                        if (!reportedUnaffordable)
                        {
                            status?.Invoke(
                                $"Step {stepNumber}/{stepCount}: waiting for Upgrade Unit {pressNumber}/{pressCount} to become affordable.");
                            reportedUnaffordable = true;
                        }
                        break;
                    default:
                        stableFrames = 0;
                        stableState =
                            UpgradeUnitReadinessState.Unknown;
                        break;
                }
            }

            if (_utcNow() >= deadline)
            {
                break;
            }
            await _delay(
                    TimeSpan.FromMilliseconds(
                        PollMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            $"Upgrade Unit {pressNumber}/{pressCount} did not become affordable within three minutes.");
    }

    private async Task TapRepeatedAsync(
        RobloxWindow window,
        char upgradeKey,
        int pressCount,
        int actionIntervalMilliseconds,
        CancellationToken cancellationToken)
    {
        for (int press = 0; press < pressCount; press++)
        {
            EnsureFocus(window);
            await automation.TapLetterKeyAsync(
                    window,
                    upgradeKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (press + 1 < pressCount &&
                actionIntervalMilliseconds > 0)
            {
                await _delay(
                        TimeSpan.FromMilliseconds(
                            actionIntervalMilliseconds),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private void EnsureFocus(
        RobloxWindow window)
    {
        if (!automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
