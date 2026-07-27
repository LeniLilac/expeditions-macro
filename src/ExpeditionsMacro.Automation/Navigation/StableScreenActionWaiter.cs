namespace ExpeditionsMacro.Automation.Navigation;

internal readonly record struct StableScreenAction<TObservation>(
    TObservation Observation,
    int X,
    int Y);

internal static class StableScreenActionWaiter
{
    public static async Task<StableScreenAction<TObservation>?> WaitAsync<
        TState,
        TObservation>(
        TState desiredState,
        int stableDetections,
        Func<TObservation> observe,
        Func<TObservation, TState> stateFor,
        Func<TObservation, (int X, int Y)?> actionFor,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(observe);
        ArgumentNullException.ThrowIfNull(stateFor);
        ArgumentNullException.ThrowIfNull(actionFor);
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

        int required = Math.Max(2, stableDetections);
        StableNavigationActionTracker<TState> tracker =
            new(required);
        ObservationWaitBudget budget =
            new(timeout, required, utcNow);
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TObservation observation = observe();
            TState observedState = stateFor(observation);
            (int X, int Y)? action =
                EqualityComparer<TState>.Default.Equals(
                    desiredState,
                    observedState)
                    ? actionFor(observation)
                    : null;
            (int X, int Y)? stableAction =
                tracker.Update(
                    desiredState,
                    action);
            budget.MarkObserved();
            if (stableAction is not null)
            {
                return new StableScreenAction<TObservation>(
                    observation,
                    stableAction.Value.X,
                    stableAction.Value.Y);
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

        return null;
    }
}
