using ExpeditionsMacro.Automation.Challenges;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class ChallengePlacementPartitionTests
{
    [Fact]
    public void HiddenPlacement_DefersTheCompleteOrderedSuffix()
    {
        ScreenRegion dialog =
            new(314, 94, 180, 104);
        PlacementStep[] steps =
        [
            Step(300, 137),
            Step(354, 129),
            Step(384, 184),
            Step(363, 246),
            Step(485, 182),
            Step(578, 190),
        ];

        ChallengePlacementPartition partition =
            ChallengeRunPolicy
                .PartitionPrestartPlacements(
                    steps,
                    dialog);

        Assert.Equal(
            [(300, 137)],
            partition.BeforeStart.Select(
                step => (step.X, step.Y)));
        Assert.Equal(
            [
                (354, 129),
                (384, 184),
                (363, 246),
                (485, 182),
                (578, 190),
            ],
            partition.AfterStart.Select(
                step => (step.X, step.Y)));
    }

    [Fact]
    public void DependencySuffix_StaysBehindCoveredPlacement()
    {
        ScreenRegion dialog =
            new(314, 94, 180, 104);
        PlacementStep visible =
            Step(300, 137);
        PlacementStep covered =
            Step(354, 129);
        PlacementStep delay = new()
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 1,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            DelayDurationMilliseconds = 250,
        };
        PlacementStep reconfigure = new()
        {
            Kind = MatchStepKind.ReconfigureUnit,
            UnitKey = 1,
            TargetPlacementId =
                covered.PlacementId,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Strongest,
        };

        ChallengePlacementPartition partition =
            ChallengeRunPolicy
                .PartitionPrestartPlacements(
                    [
                        visible,
                        covered,
                        delay,
                        reconfigure,
                    ],
                    dialog);

        Assert.Equal(
            [visible],
            partition.BeforeStart);
        Assert.Equal(
            [covered, delay, reconfigure],
            partition.AfterStart);
    }

    [Fact]
    public void CoveredReferencedUnit_DefersItsAction()
    {
        ScreenRegion dialog =
            new(314, 94, 180, 104);
        PlacementStep covered =
            Step(354, 129);
        PlacementStep reconfigure = new()
        {
            Kind = MatchStepKind.ReconfigureUnit,
            UnitKey = 1,
            TargetPlacementId =
                covered.PlacementId,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            ChangeTargetingPriority = true,
            TargetingPriority =
                UnitTargetingPriority.Strongest,
        };

        ChallengePlacementPartition partition =
            ChallengeRunPolicy
                .PartitionPrestartPlacements(
                    [covered, reconfigure],
                    dialog);

        Assert.Empty(partition.BeforeStart);
        Assert.Equal(
            [covered, reconfigure],
            partition.AfterStart);
    }

    private static PlacementStep Step(
        int x,
        int y) =>
        new()
        {
            Kind = MatchStepKind.Placement,
            PlacementId =
                $"placement-{x}-{y}",
            UnitKey = 1,
            X = x,
            Y = y,
            DelayAfterMilliseconds = 900,
        };
}
