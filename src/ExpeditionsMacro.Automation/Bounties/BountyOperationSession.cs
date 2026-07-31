using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Bounties;

public sealed class BountyOperationSession
{
    private int _initialBoardReconciled;

    public bool InitialBoardReconciliationRequired =>
        Volatile.Read(
            ref _initialBoardReconciled) == 0;

    internal void MarkInitialBoardReconciled() =>
        Volatile.Write(
            ref _initialBoardReconciled,
            1);
}

internal static class BountyBoardReconciliationPolicy
{
    public static bool RequiresBoardProcessing(
        BountyOperationSession operationSession,
        IReadOnlyList<BountyActiveProgress> active,
        BountyChallengeAvailability
            challengeAvailability)
    {
        ArgumentNullException.ThrowIfNull(
            operationSession);
        ArgumentNullException.ThrowIfNull(active);
        return operationSession
                .InitialBoardReconciliationRequired ||
            !BountyPlanner.HasExecutableWork(
                active,
                challengeAvailability);
    }
}
