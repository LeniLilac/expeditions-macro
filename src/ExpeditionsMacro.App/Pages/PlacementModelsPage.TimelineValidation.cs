using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.App.Pages;

public partial class PlacementModelsPage
{
    private void ValidateTimeline(
        IReadOnlyList<PlacementStep> steps,
        IReadOnlyList<PlacementStep>? previous = null)
    {
        PlacementStep[] normalized =
            PlacementTimelinePolicy
                .NormalizeSteps(steps)
                .ToArray();
        foreach (PlacementStep step in normalized)
        {
            step.Validate(808, 611);
        }
        PlacementAuthoringRules.ValidateMinimumSpacing(
            normalized);
        PlacementAuthoringRules.ValidateBeforeStartSafety(
            normalized);
        PlacementAuthoringRules
            .ValidateMatchStepReferences(normalized);

        PlacementTargetMode mode =
            CurrentFastTarget().Mode;
        int? duplicate = previous is null
            ? PlacementSafetyRules.FindDuplicateUnitSlot(
                mode,
                PlacementUnitSlots(normalized))
            : PlacementSafetyRules
                .FindIntroducedDuplicateUnitSlot(
                    mode,
                    PlacementUnitSlots(previous),
                    PlacementUnitSlots(normalized));
        if (duplicate is int duplicateUnit)
        {
            throw new InvalidOperationException(
                $"Expedition setups allow one placement for Unit {duplicateUnit}.");
        }
    }

    private static IEnumerable<int> PlacementUnitSlots(
        IEnumerable<PlacementStep> steps) =>
        steps.Where(step =>
                step.Kind == MatchStepKind.Placement)
            .Select(step => step.UnitKey);
}
