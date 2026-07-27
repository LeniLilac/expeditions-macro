using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

internal static class ExpeditionRecoveryTransitionLoop
{
    internal const int MaximumAttemptsPerState = 3;
    internal const int MaximumTotalAttempts = 18;

    internal static async Task<string> RunAsync(
        string initialState,
        Func<string, bool> isComplete,
        Func<string, CancellationToken, Task<string?>> transition,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialState);
        ArgumentNullException.ThrowIfNull(isComplete);
        ArgumentNullException.ThrowIfNull(transition);

        string state = initialState;
        string budgetState = initialState;
        int stateAttempts = 0;
        int totalAttempts = 0;

        while (!isComplete(state))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!state.Equals(
                    budgetState,
                    StringComparison.OrdinalIgnoreCase))
            {
                budgetState = state;
                stateAttempts = 0;
            }

            if (stateAttempts >= MaximumAttemptsPerState)
            {
                throw Exhausted(
                    state,
                    $"{MaximumAttemptsPerState} consecutive attempts");
            }
            if (totalAttempts >= MaximumTotalAttempts)
            {
                throw Exhausted(
                    state,
                    $"{MaximumTotalAttempts} total transition attempts");
            }

            stateAttempts++;
            totalAttempts++;
            string? nextState = await transition(
                state,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(nextState))
            {
                state = nextState;
            }
        }

        return state;
    }

    private static RobloxUiUnavailableException Exhausted(
        string state,
        string limit) =>
        new(
            $"Expedition recovery remained on '{state}' after {limit}. " +
            "Roblox did not acknowledge the recovery action or finish loading the next screen. " +
            "Wait for the client to become responsive, then retry the macro.");
}
