using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementExecutionPlanTests
{
    [Fact]
    public void DueAfterStartBatch_GroupsOnlyRowsDueAtCurrentElapsedTime()
    {
        PlacementStep[] ordered =
        [
            Step(1, 30_000),
            Step(2, 30_000),
            Step(3, 45_000),
        ];

        IReadOnlyList<PlacementStep> first =
            PlacementExecutionPlan
                .DueAfterStartBatch(
                    ordered,
                    nextIndex: 0,
                    TimeSpan.FromSeconds(30));
        IReadOnlyList<PlacementStep> second =
            PlacementExecutionPlan
                .DueAfterStartBatch(
                    ordered,
                    nextIndex: first.Count,
                    TimeSpan.FromSeconds(44));
        IReadOnlyList<PlacementStep> third =
            PlacementExecutionPlan
                .DueAfterStartBatch(
                    ordered,
                    nextIndex: first.Count,
                    TimeSpan.FromSeconds(45));
        IReadOnlyList<PlacementStep> overdue =
            PlacementExecutionPlan
                .DueAfterStartBatch(
                    ordered,
                    nextIndex: 0,
                    TimeSpan.FromMinutes(1));

        Assert.Equal([1, 2],
            first.Select(step => step.UnitKey));
        Assert.Empty(second);
        Assert.Equal(
            [3],
            third.Select(step => step.UnitKey));
        Assert.Equal(
            [1, 2],
            overdue.Select(step => step.UnitKey));
    }

    private static PlacementStep Step(
        int unit,
        int delay) =>
        new()
        {
            UnitKey = unit,
            X = 100 + unit * 10,
            Y = 200,
            DelayAfterMilliseconds = 0,
            Phase = PlacementPhase.AfterStart,
            DelayAfterStartMilliseconds = delay,
        };
}
