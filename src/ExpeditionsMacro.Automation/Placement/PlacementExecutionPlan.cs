using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Automation.Placement;

public readonly record struct PlacementMatchExecutionPlan(
    bool ManualPlayback,
    IReadOnlyList<PlacementStep> BeforeStart,
    IReadOnlyList<PlacementStep> AfterStart);

public static class PlacementExecutionPlan
{
    public static PlacementMatchExecutionPlan ForMatch(
        CameraPreparationMode mode,
        PlacementModel? primary,
        PlacementModel? delayed = null)
    {
        bool manualPlayback =
            primary is not null &&
            ManualInputRouteService.IsConfigured(primary);
        return manualPlayback
            ? new PlacementMatchExecutionPlan(
                true,
                [],
                [])
            : new PlacementMatchExecutionPlan(
                false,
                BeforeStart(mode, primary),
                AfterStart(mode, primary, delayed));
    }

    public static IReadOnlyList<PlacementStep> BeforeStart(
        CameraPreparationMode mode,
        PlacementModel? primary)
    {
        if (primary is null) return [];
        return mode == CameraPreparationMode.FastNoAlign
            ? primary.Steps
                .Where(step =>
                    step.Phase == PlacementPhase.BeforeStart)
                .ToArray()
            : primary.Steps;
    }

    public static IReadOnlyList<PlacementStep> AfterStart(
        CameraPreparationMode mode,
        PlacementModel? primary,
        PlacementModel? delayed = null)
    {
        if (mode == CameraPreparationMode.FastNoAlign)
        {
            return primary?.Steps
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
        return delayed?.Steps ?? [];
    }

    public static bool IsAfterStartDue(
        PlacementStep step,
        TimeSpan elapsed) =>
        step.Phase == PlacementPhase.AfterStart &&
        elapsed >= TimeSpan.FromMilliseconds(
            step.DelayAfterStartMilliseconds);
}
