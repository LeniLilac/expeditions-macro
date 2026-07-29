using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementTimelinePolicyTests
{
    [Fact]
    public void LegacyPhases_MigrateToOneStartBoundaryAndExplicitWait()
    {
        PlacementStep before =
            Placement(1, PlacementPhase.BeforeStart);
        PlacementStep after =
            Placement(2, PlacementPhase.AfterStart) with
            {
                DelayAfterStartMilliseconds = 30_000,
            };

        IReadOnlyList<PlacementStep> timeline =
            PlacementTimelinePolicy.NormalizeSteps(
                [after, before]);

        Assert.Collection(
            timeline,
            step =>
            {
                Assert.Equal(before.UnitKey, step.UnitKey);
                Assert.Equal(before.X, step.X);
                Assert.Equal(before.Y, step.Y);
                Assert.Equal(
                    "placement-1",
                    step.PlacementId);
            },
            step => Assert.Equal(
                MatchStepKind.StartGame,
                step.Kind),
            step =>
            {
                Assert.Equal(
                    MatchStepKind.Delay,
                    step.Kind);
                Assert.Equal(
                    30_000,
                    step.DelayDurationMilliseconds);
            },
            step =>
            {
                Assert.Equal(2, step.UnitKey);
                Assert.Equal(
                    PlacementPhase.AfterStart,
                    step.Phase);
                Assert.Equal(
                    0,
                    step.DelayAfterStartMilliseconds);
            });
    }

    [Fact]
    public void ExistingTimeline_DerivesCompatibilityPhaseFromPosition()
    {
        IReadOnlyList<PlacementStep> timeline =
            PlacementTimelinePolicy.NormalizeSteps(
            [
                Placement(
                    1,
                    PlacementPhase.AfterStart),
                PlacementTimelinePolicy
                    .CreateStartGameStep(),
                Placement(
                    2,
                    PlacementPhase.BeforeStart),
            ]);

        Assert.Equal(
            PlacementPhase.BeforeStart,
            timeline[0].Phase);
        Assert.Equal(
            PlacementPhase.AfterStart,
            timeline[2].Phase);
    }

    [Fact]
    public void ExecutionPlan_UsesTimelineAndCompilesAfterStartWaits()
    {
        PlacementModel model = Model(
        [
            Placement(
                1,
                PlacementPhase.BeforeStart),
            PlacementTimelinePolicy
                .CreateStartGameStep(),
            Delay(1200),
            Placement(
                2,
                PlacementPhase.AfterStart),
            Placement(
                3,
                PlacementPhase.AfterStart),
            Delay(800),
            Placement(
                4,
                PlacementPhase.AfterStart),
        ]);

        PlacementMatchExecutionPlan plan =
            PlacementExecutionPlan.ForMatch(model);

        Assert.Equal(
            [1],
            plan.BeforeStart.Select(step =>
                step.UnitKey));
        Assert.Equal(
            [2, 3, 4],
            plan.AfterStart.Select(step =>
                step.UnitKey));
        Assert.Equal(
            [1200, 1200, 2000],
            plan.AfterStart.Select(step =>
                step.DelayAfterStartMilliseconds));
    }

    [Fact]
    public void MultipleStartGameSteps_AreRejected()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () =>
                    PlacementTimelinePolicy
                        .NormalizeSteps(
                        [
                            PlacementTimelinePolicy
                                .CreateStartGameStep(),
                            PlacementTimelinePolicy
                                .CreateStartGameStep(),
                        ]));

        Assert.Contains(
            "only one Start Game",
            error.Message,
            StringComparison.Ordinal);
    }

    private static PlacementStep Placement(
        int unit,
        PlacementPhase phase) =>
        new()
        {
            Kind = MatchStepKind.Placement,
            UnitKey = unit,
            X = 100 + unit * 20,
            Y = 200,
            DelayAfterMilliseconds = 900,
            Phase = phase,
        };

    private static PlacementStep Delay(
        int milliseconds) =>
        new()
        {
            Kind = MatchStepKind.Delay,
            UnitKey = 0,
            X = 0,
            Y = 0,
            DelayAfterMilliseconds = 0,
            Phase = PlacementPhase.AfterStart,
            DelayDurationMilliseconds = milliseconds,
        };

    private static PlacementModel Model(
        IReadOnlyList<PlacementStep> steps) =>
        new()
        {
            Id = "timeline-test",
            Name = "Timeline test",
            ClientWidth = 808,
            ClientHeight = 611,
            Steps = steps,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 0,
                ActNumber = 0,
            },
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
