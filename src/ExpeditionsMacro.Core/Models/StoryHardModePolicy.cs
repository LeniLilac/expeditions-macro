namespace ExpeditionsMacro.Core.Models;

public static class StoryHardModePolicy
{
    public static bool SupportsHardMode(
        PlacementTarget? target) =>
        target is
        {
            Mode: PlacementTargetMode.Story,
            StoryRunKind: StoryRunKind.Act,
        };

    public static MacroTaskDefinition Normalize(
        MacroTaskDefinition task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.Kind == MacroTaskKind.Story &&
               task.HardMode &&
               task.PlacementTarget is not null &&
               !SupportsHardMode(task.PlacementTarget)
            ? task with { HardMode = false }
            : task;
    }

    public static MacroPlan Normalize(
        MacroPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        MacroTaskDefinition[] tasks =
            plan.Tasks
                .Select(Normalize)
                .ToArray();
        return plan with { Tasks = tasks };
    }

    public static StoryPreset Normalize(
        StoryPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return preset.RunKind != StoryRunKind.Act &&
               preset.HardMode
            ? preset with { HardMode = false }
            : preset;
    }

    public static FastNoAlignShareBundle Normalize(
        FastNoAlignShareBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle with
        {
            Plan = Normalize(bundle.Plan),
            StoryPresets = bundle.StoryPresets
                .Select(Normalize)
                .ToArray(),
        };
    }
}
