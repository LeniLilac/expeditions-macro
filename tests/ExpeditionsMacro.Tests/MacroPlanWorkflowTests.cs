using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class MacroPlanWorkflowTests
{
    [Fact]
    public void PlansIdentifyFastAndLegacyWorkflowsWithoutConvertingEither()
    {
        MacroTaskDefinition fast = new()
        {
            Id = "fast-task",
            Kind = MacroTaskKind.Expedition,
            PlacementTarget = new PlacementTarget
            {
                Mode = PlacementTargetMode.Expedition,
                MapNumber = 1,
                ActNumber = 0,
            },
        };
        MacroTaskDefinition legacy = new()
        {
            Id = "legacy-task",
            Kind = MacroTaskKind.Expedition,
            PresetId = "legacy-preset",
        };

        Assert.True(Plan(fast).UsesPlacementSetupWorkflow);
        Assert.False(Plan(legacy).UsesPlacementSetupWorkflow);
        Assert.False(
            Plan(fast, legacy)
                .UsesPlacementSetupWorkflow);
    }

    private static MacroPlan Plan(
        params MacroTaskDefinition[] tasks) =>
        new()
        {
            Id = "plan",
            Name = "Plan",
            Tasks = tasks,
        };
}
