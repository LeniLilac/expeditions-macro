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
        IReadOnlyList<PlacementStep> timeline =
            PlacementTimelinePolicy.NormalizeSteps(
                placement.Steps);
        int start =
            PlacementTimelinePolicy.StartGameIndex(
                timeline);
        return timeline.Take(start).ToArray();
    }

    public static IReadOnlyList<PlacementStep> AfterStart(
        PlacementModel? placement)
    {
        if (placement is null) return [];
        IReadOnlyList<PlacementStep> timeline =
            PlacementTimelinePolicy.NormalizeSteps(
                placement.Steps);
        int start =
            PlacementTimelinePolicy.StartGameIndex(
                timeline);
        int elapsedMilliseconds = 0;
        List<PlacementStep> scheduled = [];
        foreach (PlacementStep step in
                 timeline.Skip(start + 1))
        {
            if (step.Kind == MatchStepKind.Delay)
            {
                elapsedMilliseconds = checked(
                    elapsedMilliseconds +
                    step.DelayDurationMilliseconds);
                continue;
            }
            scheduled.Add(step with
            {
                Phase = PlacementPhase.AfterStart,
                DelayAfterStartMilliseconds =
                    elapsedMilliseconds,
            });
        }
        return scheduled;
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
