using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

public static class ManualPlaybackStartPolicy
{
    public static bool RequiresPrestart(
        PlacementModel? placement) =>
        placement is null ||
        !ManualInputRouteService.IsConfigured(placement) ||
        !placement.AdvancedSettings.Enabled ||
        placement.AdvancedSettings
            .VerifyPrestartBeforeManualPlayback;

    public static bool RequiresPrestart(
        PlacementModel? placement,
        bool arrivedFromRepeatStage) =>
        !arrivedFromRepeatStage ||
        RequiresPrestart(placement);

    public static async Task WaitBeforePlaybackAsync(
        PlacementModel placement,
        Action<string>? status,
        CancellationToken cancellationToken) =>
        await WaitBeforePlaybackAsync(
                placement,
                status,
                static (delay, token) =>
                    Task.Delay(delay, token),
                cancellationToken)
            .ConfigureAwait(false);

    internal static async Task WaitBeforePlaybackAsync(
        PlacementModel placement,
        Action<string>? status,
        Func<int, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(delayAsync);
        if (RequiresPrestart(placement))
        {
            return;
        }
        int delay =
            placement.AdvancedSettings
                .ManualPlaybackStartDelayMilliseconds;
        status?.Invoke(
            delay == 0
                ? "Advanced Recording Mode is starting playback without Start-screen verification."
                : $"Advanced Recording Mode is waiting {delay} ms after route entry before playback.");
        if (delay > 0)
        {
            await delayAsync(delay, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
