using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class PlacementNewActionOrderingTests
{
    [Fact]
    public void EmptyTimeline_AppendsNewActionBelowStartGame()
    {
        PlacementStep start = PlacementTimelinePolicy
            .CreateStartGameStep();

        int insertion = PlacementTimelinePolicy
            .NewActionInsertionIndex([start]);

        Assert.Equal(1, insertion);
    }

    [Fact]
    public void ExistingActions_DoNotChangeBottomInsertion()
    {
        PlacementStep before = Step(1);
        PlacementStep start = PlacementTimelinePolicy
            .CreateStartGameStep();
        PlacementStep after = Step(2);

        int insertion = PlacementTimelinePolicy
            .NewActionInsertionIndex(
                [before, start, after]);

        Assert.Equal(3, insertion);
    }

    [Fact]
    public void MissingStartGame_RejectsNewAction()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () => PlacementTimelinePolicy
                    .NewActionInsertionIndex(
                        [Step(1)]));

        Assert.Contains(
            "Start Game",
            error.Message,
            StringComparison.Ordinal);
    }

    private static PlacementStep Step(int unit) =>
        new()
        {
            UnitKey = unit,
            X = 200 + unit * 50,
            Y = 300,
            DelayAfterMilliseconds = 900,
        };
}
