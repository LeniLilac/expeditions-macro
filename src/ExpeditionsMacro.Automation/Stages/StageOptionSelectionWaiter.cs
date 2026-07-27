using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Stages;

internal readonly record struct StageOptionSelectionWaitResult<
    TObservation>(
    TObservation? Observation,
    int? ActionX,
    int? ActionY,
    string? Interruption)
    where TObservation : class
{
    public bool Succeeded =>
        Observation is not null &&
        Interruption is null &&
        (ActionX is null) ==
        (ActionY is null);
}

internal static class StageOptionSelectionWaiter
{
    public static async Task<
        StageOptionSelectionWaitResult<TObservation>>
        ClickOnceAndWaitAsync<TObservation>(
            Func<CancellationToken, Task> click,
            int stableDetections,
            Func<TObservation> observe,
            Func<TObservation, bool> matches,
            Func<TObservation, (int X, int Y)?>?
                actionFor,
            Func<TObservation, string?> interruptionFor,
            TimeSpan timeout,
            TimeSpan pollInterval,
            CancellationToken cancellationToken,
            Func<DateTimeOffset>? utcNow = null,
            Func<
                TimeSpan,
                CancellationToken,
                Task>? delay = null)
        where TObservation : class
    {
        ArgumentNullException.ThrowIfNull(click);
        ArgumentNullException.ThrowIfNull(observe);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(interruptionFor);
        if (stableDetections < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stableDetections));
        }
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout));
        }
        if (pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval));
        }

        await click(cancellationToken)
            .ConfigureAwait(false);

        int required = Math.Max(
            2,
            stableDetections);
        StableStateTracker<string> stateTracker =
            new(required);
        StableNavigationActionTracker<string>
            actionTracker =
                new(required);
        StableStateTracker<string> interruptionTracker =
            new(required);
        ObservationWaitBudget budget =
            new(timeout, required, utcNow);
        while (budget.ShouldObserve(
                   stateTracker.HasPendingCandidate ||
                   actionTracker.HasPendingCandidate ||
                   interruptionTracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TObservation observation = observe();
            bool selected = matches(observation);
            string? stableInterruption =
                interruptionTracker.Update(
                    interruptionFor(observation));
            budget.MarkObserved();
            if (stableInterruption is not null)
            {
                return new StageOptionSelectionWaitResult<
                    TObservation>(
                    observation,
                    null,
                    null,
                    stableInterruption);
            }

            if (actionFor is null)
            {
                if (stateTracker.Update(
                        selected
                            ? "selected"
                            : null) is not null)
                {
                    return new StageOptionSelectionWaitResult<
                        TObservation>(
                        observation,
                        null,
                        null,
                        null);
                }
            }
            else
            {
                (int X, int Y)? action =
                    selected
                        ? actionFor(observation)
                        : null;
                (int X, int Y)? stableAction =
                    actionTracker.Update(
                        selected
                            ? "selected"
                            : null,
                        action);
                if (stableAction is not null)
                {
                    return new StageOptionSelectionWaitResult<
                        TObservation>(
                        observation,
                        stableAction.Value.X,
                        stableAction.Value.Y,
                        null);
                }
            }

            await (delay is null
                    ? Task.Delay(
                        pollInterval,
                        cancellationToken)
                    : delay(
                        pollInterval,
                        cancellationToken))
                .ConfigureAwait(false);
        }

        return default;
    }
}
