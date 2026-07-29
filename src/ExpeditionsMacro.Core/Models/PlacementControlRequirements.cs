namespace ExpeditionsMacro.Core.Models;

public static class PlacementControlRequirements
{
    public static async Task<bool>
        PlanRequiresQuickPlacementKeyAsync(
        MacroPlan plan,
        Func<
            MacroTaskDefinition,
            CancellationToken,
            Task<IReadOnlyList<PlacementModel>>>
            resolvePlacements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(resolvePlacements);

        foreach (MacroTaskDefinition task in plan.Tasks)
        {
            IReadOnlyList<PlacementModel> placements =
                await resolvePlacements(
                        task,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (placements.Any(
                    RequiresQuickPlacementKey))
            {
                return true;
            }
        }

        return false;
    }

    public static void ValidateQuickPlacementForPlayback(
        PlacementModel model,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (RequiresQuickPlacementKey(model))
        {
            _ = AppSettings.ParseQuickPlacementKey(
                settings);
        }
    }

    public static char ValidateStepModeBindingsForPlayback(
        PlacementModel model,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(model);
        return RequiresQuickPlacementKey(model)
            ? ValidateStepModeBindings(settings)
            : default;
    }

    public static char ValidateStepModeBindings(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = AppSettings.ParseQuickPlacementKey(
            settings);
        return AppSettings.ParseCancelPlacementKey(
            settings.CancelPlacementKey,
            settings.MacroHotkeyVirtualKey,
            settings.PlayMenuKey,
            settings.UnitMenuKey,
            settings.AreasMenuKey,
            settings.ShiftLockVirtualKey);
    }

    public static bool RequiresQuickPlacementKey(
        PlacementModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Steps.Any(step =>
                step.Kind == MatchStepKind.Placement) &&
            string.IsNullOrWhiteSpace(
                model.ManualInputRecordingId);
    }
}
