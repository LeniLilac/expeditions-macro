using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Events;

namespace ExpeditionsMacro.Automation.Events;

internal enum EventPlayInterfaceLayer
{
    Closed,
    GameModeSelector,
    PostMatchParty,
}

internal static class EventPlayInterfaceCloser
{
    internal const int MaximumBackAttempts = 4;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);

    internal static EventPlayInterfaceLayer DetectLayer(
        ImageFrame frame)
    {
        if (EventScreenDetector.Detect(frame).State ==
            EventScreenState.GameModeSelector)
        {
            return EventPlayInterfaceLayer.GameModeSelector;
        }
        ChallengeScreenState state =
            ChallengeScreenDetector.Detect(frame).State;
        return state switch
        {
            ChallengeScreenState.GameModeSelector =>
                EventPlayInterfaceLayer.GameModeSelector,
            ChallengeScreenState.PostMatchPreview =>
                EventPlayInterfaceLayer.PostMatchParty,
            _ => EventPlayInterfaceLayer.Closed,
        };
    }

    internal static async Task CloseAsync(
        Func<EventPlayInterfaceLayer> observe,
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

        EventPlayInterfaceLayer layer =
            await ObserveStableLayerAsync(
                observe,
                delay,
                cancellationToken,
                utcNow).ConfigureAwait(false);
        if (layer == EventPlayInterfaceLayer.Closed)
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
            if (layer == EventPlayInterfaceLayer.Closed)
            {
                return;
            }
        }

        throw new RobloxUiUnavailableException(
            "The Play interface remained open after its verified Back actions, so Event lobby navigation could not begin.");
    }

    private static async Task<EventPlayInterfaceLayer>
        ObserveStableLayerAsync(
        Func<EventPlayInterfaceLayer> observe,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow)
    {
        EventPlayInterfaceLayer? last = null;
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
            EventPlayInterfaceLayer current = observe();
            budget.MarkObserved();
            stable = current == last ? stable + 1 : 1;
            last = current;
            required =
                current == EventPlayInterfaceLayer.Closed
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
            "The Play interface did not settle while preparing Event lobby navigation.");
    }
}
