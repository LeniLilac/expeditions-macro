using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Settings;

namespace ExpeditionsMacro.Automation.Settings;

internal sealed class StableGameSettingsControlWaiter(
    Func<DateTimeOffset> utcNow,
    Func<TimeSpan, CancellationToken, Task> delay,
    TimeSpan pollInterval)
{
    public async Task<GameSettingToggleMatch?> WaitForToggleAsync(
        Func<ImageFrame> capture,
        RequiredGameSetting setting,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        GameSettingToggleMatch? candidate = null;
        int stableFrames = 0;
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            utcNow);
        while (budget.ShouldObserve(
                   confirmationPending: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameSettingToggleMatch observed =
                GameSettingsScreenDetector.DetectToggle(
                    capture(),
                    setting);
            if (observed.State ==
                GameSettingToggleState.Unknown)
            {
                candidate = null;
                stableFrames = 0;
            }
            else if (candidate is not null &&
                     SameControl(
                         candidate.Value,
                         observed))
            {
                stableFrames++;
            }
            else
            {
                candidate = observed;
                stableFrames = 1;
            }

            budget.MarkObserved();
            if (stableFrames >= 2)
            {
                return observed;
            }
            await delay(
                    pollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    public async Task<GameSettingsScrollbarThumb?>
        WaitForUnitsScrollbarAsync(
            Func<ImageFrame> capture,
            TimeSpan timeout,
            CancellationToken cancellationToken)
    {
        GameSettingsScrollbarThumb? candidate = null;
        int stableFrames = 0;
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            utcNow);
        while (budget.ShouldObserve(
                   confirmationPending: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameSettingsScrollbarThumb? observed =
                GameSettingsScreenDetector
                    .FindUnitsScrollbarThumb(capture());
            if (observed is null)
            {
                candidate = null;
                stableFrames = 0;
            }
            else if (candidate == observed)
            {
                stableFrames++;
            }
            else
            {
                candidate = observed;
                stableFrames = 1;
            }

            budget.MarkObserved();
            if (stableFrames >= 2)
            {
                return observed;
            }
            await delay(
                    pollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private static bool SameControl(
        GameSettingToggleMatch left,
        GameSettingToggleMatch right) =>
        left.Setting == right.Setting &&
        left.State == right.State &&
        left.ActionX == right.ActionX &&
        left.ActionY == right.ActionY;
}
