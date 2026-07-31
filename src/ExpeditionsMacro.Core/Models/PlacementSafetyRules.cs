namespace ExpeditionsMacro.Core.Models;

public static class PlacementSafetyRules
{
    private const int CanonicalClientRight = 807;
    private const int CanonicalClientBottom = 610;
    private const int CentralHotbarLeft = 235;
    private const int CentralHotbarTop = 525;
    private const int CentralHotbarRight = 585;
    private const int CentralHotbarBottom = 603;

    public static bool IsInsideFixedCentralHotbar(
        int x,
        int y) =>
        x >= CentralHotbarLeft &&
        x < CentralHotbarRight &&
        y >= CentralHotbarTop &&
        y < CentralHotbarBottom;

    public static bool IsOnCanonicalClientEdge(
        int x,
        int y) =>
        x <= 0 ||
        y <= 0 ||
        x >= CanonicalClientRight ||
        y >= CanonicalClientBottom;

    public static int? FindDuplicateUnitSlot(
        PlacementTargetMode mode,
        IEnumerable<int> unitSlots)
    {
        ArgumentNullException.ThrowIfNull(unitSlots);
        if (mode != PlacementTargetMode.Expedition)
        {
            return null;
        }

        HashSet<int> seen = [];
        foreach (int unitSlot in unitSlots)
        {
            if (!seen.Add(unitSlot))
            {
                return unitSlot;
            }
        }
        return null;
    }

    public static string? GetPlaybackSkipReason(
        PlacementModel model,
        PlacementStep step)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(step);
        if (step.HasCoordinate &&
            IsOnCanonicalClientEdge(step.X, step.Y))
        {
            return
                $"placement ({step.X}, {step.Y}) is on the canonical client edge rather than a verified map point. Remove it in Placement Setup and choose a point inside the battlefield.";
        }
        if (step.HasCoordinate &&
            IsInsideFixedCentralHotbar(step.X, step.Y))
        {
            return
                $"placement ({step.X}, {step.Y}) is inside the fixed center unit hotbar. Remove it in Placement Setup and choose a map point outside the bottom hotbar.";
        }

        if (model.Target?.Mode !=
                PlacementTargetMode.Expedition ||
            step.Kind != MatchStepKind.Placement)
        {
            return null;
        }

        int stepIndex = IndexOf(model.Steps, step);
        return stepIndex > 0 &&
            model.Steps.Take(stepIndex).Any(
                candidate =>
                    candidate.Kind ==
                        MatchStepKind.Placement &&
                    candidate.UnitKey == step.UnitKey)
            ? $"Expedition Unit {step.UnitKey} already has an earlier placement in this Match Steps timeline. Remove this duplicate in Placement Setup."
            : null;
    }

    private static int IndexOf(
        IReadOnlyList<PlacementStep> steps,
        PlacementStep target)
    {
        for (int index = 0; index < steps.Count; index++)
        {
            if (ReferenceEquals(steps[index], target))
            {
                return index;
            }
        }
        for (int index = 0; index < steps.Count; index++)
        {
            if (steps[index] == target)
            {
                return index;
            }
        }
        return -1;
    }
}
