using ExpeditionsMacro.Automation.Expeditions;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class ExpeditionPlacementRetryTests
{
    [Fact]
    public void RetrySelection_ExcludesEveryNonPlacementAction()
    {
        PlacementStep firstPlacement =
            Step(MatchStepKind.Placement, 1);
        PlacementStep secondPlacement =
            Step(MatchStepKind.Placement, 2);
        PlacementStep[] timeline =
        [
            Step(MatchStepKind.ReconfigureUnit, 1),
            firstPlacement,
            Step(MatchStepKind.Delay, 0),
            Step(MatchStepKind.UpgradeUnit, 1),
            Step(MatchStepKind.StartGame, 0),
            secondPlacement,
        ];

        PlacementStep[] selected =
            ExpeditionMacroRunner
                .SelectRetryablePlacementSteps(
                    timeline);

        Assert.Equal(
            [firstPlacement, secondPlacement],
            selected);
    }

    [Fact]
    public void RetrySelection_PreservesOnlyRemainingPlacementRows()
    {
        PlacementStep remaining =
            Step(MatchStepKind.Placement, 2);
        PlacementStep[] timeline =
        [
            Step(MatchStepKind.Placement, 1),
            Step(MatchStepKind.ReconfigureUnit, 2),
            remaining,
            Step(MatchStepKind.UpgradeUnit, 2),
        ];

        PlacementStep[] selected =
            ExpeditionMacroRunner
                .SelectRetryablePlacementSteps(
                    timeline,
                    new HashSet<int> { 2 });

        Assert.Equal([remaining], selected);
    }

    private static PlacementStep Step(
        MatchStepKind kind,
        int unitKey) =>
        new()
        {
            Kind = kind,
            UnitKey = unitKey,
            X = 100,
            Y = 200,
            DelayAfterMilliseconds = 0,
        };
}
