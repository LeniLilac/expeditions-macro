using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class EventPresetTests
{
    [Theory]
    [InlineData(EventAct.Act1)]
    [InlineData(EventAct.Act2)]
    [InlineData(EventAct.Act3)]
    public void ReviewedActs_AreValid(EventAct act)
    {
        EventPreset preset = Preset(act);

        preset.Validate();
    }

    [Fact]
    public void ActFour_RemainsUnavailableWithoutPlacementData()
    {
        InvalidDataException error =
            Assert.Throws<InvalidDataException>(
                () => Preset(EventAct.Act4).Validate());

        Assert.Contains(
            "Act 4",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FastTask_RequiresAnEventRoute()
    {
        MacroTaskDefinition task = new()
        {
            Id = "event-1",
            Kind = MacroTaskKind.Event,
            Name = "Villain Invasion",
            PlacementTarget = new PlacementTarget
            {
                Mode = PlacementTargetMode.Event,
                MapNumber =
                    (int)EventModeId.VillainInvasion,
                ActNumber = 2,
            },
        };

        task.Validate();
    }

    private static EventPreset Preset(EventAct act) =>
        new()
        {
            Id = "event-preset",
            Name = "Villain Invasion",
            Act = act,
            PlacementModelId = "event-placement",
        };
}
