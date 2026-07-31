using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class BountyOperationSessionTests
{
    [Fact]
    public void NewMacroOperation_ForcesBoardBeforeSavedWork()
    {
        BountyOperationSession session = new();
        IReadOnlyList<BountyActiveProgress> active =
        [
            new()
            {
                Number = 5,
            },
        ];
        Assert.True(
            BountyPlanner.HasExecutableWork(
                active,
                BountyChallengeAvailability.Available));

        Assert.True(
            BountyBoardReconciliationPolicy
                .RequiresBoardProcessing(
                    session,
                    active,
                    BountyChallengeAvailability.Available));
    }

    [Fact]
    public void CompletedInitialReconciliation_ResumesSavedWork()
    {
        BountyOperationSession session = new();
        IReadOnlyList<BountyActiveProgress> active =
        [
            new()
            {
                Number = 5,
            },
        ];

        session.MarkInitialBoardReconciled();

        Assert.False(
            BountyBoardReconciliationPolicy
                .RequiresBoardProcessing(
                    session,
                    active,
                    BountyChallengeAvailability.Available));
    }

    [Fact]
    public void CompletedInitialReconciliation_StillOpensForNoWork()
    {
        BountyOperationSession session = new();
        session.MarkInitialBoardReconciled();

        Assert.True(
            BountyBoardReconciliationPolicy
                .RequiresBoardProcessing(
                    session,
                    Array.Empty<BountyActiveProgress>(),
                    BountyChallengeAvailability.Available));
    }
}
