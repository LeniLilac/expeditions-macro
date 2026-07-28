using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

public readonly record struct PlacementMatchExecutionPlan(
    bool ManualPlayback,
    IReadOnlyList<PlacementStep> BeforeStart,
    IReadOnlyList<PlacementStep> AfterStart);

public static class PlacementExecutionPlan
{
    public static PlacementMatchExecutionPlan ForMatch(
        PlacementModel? placement)
    {
        bool manualPlayback =
            placement is not null &&
            ManualInputRouteService.IsConfigured(placement);
        return manualPlayback
            ? new PlacementMatchExecutionPlan(
                true,
                [],
                [])
            : new PlacementMatchExecutionPlan(
                false,
                BeforeStart(placement),
                AfterStart(placement));
    }

    public static IReadOnlyList<PlacementStep> BeforeStart(
        PlacementModel? placement)
    {
        if (placement is null) return [];
        return placement.Steps
            .Where(step =>
                step.Phase == PlacementPhase.BeforeStart)
            .ToArray();
    }

    public static IReadOnlyList<PlacementStep> AfterStart(
        PlacementModel? placement)
    {
        return placement?.Steps
            .Select((step, index) =>
                new
                {
                    Step = step,
                    Index = index,
                })
            .Where(step =>
                step.Step.Phase ==
                PlacementPhase.AfterStart)
            .OrderBy(step =>
                step.Step.DelayAfterStartMilliseconds)
            .ThenBy(step => step.Index)
            .Select(step => step.Step)
            .ToArray() ?? [];
    }

    public static bool IsAfterStartDue(
        PlacementStep step,
        TimeSpan elapsed) =>
        step.Phase == PlacementPhase.AfterStart &&
        elapsed >= TimeSpan.FromMilliseconds(
            step.DelayAfterStartMilliseconds);

    public static IReadOnlyList<PlacementStep>
        DueAfterStartBatch(
        IReadOnlyList<PlacementStep> orderedSteps,
        int nextIndex,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(
            orderedSteps);
        if (nextIndex < 0 ||
            nextIndex > orderedSteps.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextIndex));
        }

        if (nextIndex == orderedSteps.Count ||
            !IsAfterStartDue(
                orderedSteps[nextIndex],
                elapsed))
        {
            return [];
        }

        int dueOffset =
            orderedSteps[nextIndex]
                .DelayAfterStartMilliseconds;
        List<PlacementStep> due = [];
        for (int index = nextIndex;
             index < orderedSteps.Count &&
             orderedSteps[index]
                 .DelayAfterStartMilliseconds ==
                 dueOffset;
             index++)
        {
            due.Add(orderedSteps[index]);
        }
        return due;
    }
}
