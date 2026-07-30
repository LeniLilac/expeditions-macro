using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Automation.Navigation;

internal enum PlayInterfaceLayer
{
    Closed,
    GameModeSelector,
    PostMatchParty,
}

internal static class PlayInterfaceCloser
{
    internal const int MaximumBackAttempts = 4;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);

    internal static PlayInterfaceLayer DetectLayer(
        ImageFrame frame)
    {
        if (EventScreenDetector.Detect(frame).State ==
            EventScreenState.GameModeSelector)
        {
            return PlayInterfaceLayer.GameModeSelector;
        }
        ChallengeScreenState state =
            ChallengeScreenDetector.Detect(frame).State;
        return state switch
        {
            ChallengeScreenState.GameModeSelector =>
                PlayInterfaceLayer.GameModeSelector,
            ChallengeScreenState.PostMatchPreview =>
                PlayInterfaceLayer.PostMatchParty,
            _ => PlayInterfaceLayer.Closed,
        };
    }

    internal static async Task CloseAsync(
        Func<PlayInterfaceLayer> observe,
        Func<CancellationToken, Task> clickBack,
        CancellationToken cancellationToken,
        Func<
            TimeSpan,
            CancellationToken,
            Task>? delay = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(observe);
        ArgumentNullException.ThrowIfNull(clickBack);
        delay ??= static (duration, token) =>
            Task.Delay(duration, token);

        PlayInterfaceLayer layer =
            await ObserveStableLayerAsync(
                observe,
                delay,
                cancellationToken,
                utcNow).ConfigureAwait(false);
        if (layer == PlayInterfaceLayer.Closed)
        {
            return;
        }

        for (int attempt = 1;
             attempt <= MaximumBackAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await clickBack(cancellationToken)
                .ConfigureAwait(false);
            layer = await ObserveStableLayerAsync(
                observe,
                delay,
                cancellationToken,
                utcNow).ConfigureAwait(false);
            if (layer == PlayInterfaceLayer.Closed)
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "The Play interface remained open after its verified Back actions, so Lobby navigation could not begin.");
    }

    private static async Task<PlayInterfaceLayer>
        ObserveStableLayerAsync(
        Func<PlayInterfaceLayer> observe,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow)
    {
        PlayInterfaceLayer? last = null;
        int stable = 0;
        int required = 0;
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(8),
            minimumObservations: 3,
            utcNow);
        while (budget.ShouldObserve(
                   confirmationPending:
                       stable > 0 &&
                       stable < required))
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlayInterfaceLayer current = observe();
            budget.MarkObserved();
            stable = current == last ? stable + 1 : 1;
            last = current;
            required =
                current == PlayInterfaceLayer.Closed
                    ? 3
                    : 2;
            if (stable >= required)
            {
                return current;
            }
            await delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            "The Play interface did not settle while preparing Lobby navigation.");
    }
}
