using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class BountyModelTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void TaskDefinition_RejectsParkingValuesOutsideZeroThroughFour(
        int value)
    {
        MacroTaskDefinition task = Task() with
        {
            BountyParkedNonViableLimit = value,
        };

        Assert.Throws<InvalidDataException>(
            task.Validate);
    }

    [Fact]
    public void TaskDefinition_IsRecurringAndDoesNotOwnOnePlacementTarget()
    {
        MacroTaskDefinition task = Task();

        task.Validate();

        Assert.True(task.IsRecurring);
        Assert.False(task.UsesPlacementSetup);
        Assert.Null(task.PlacementTarget);
    }

    [Fact]
    public void TaskDefinition_RejectsAPlacementTargetOrPreset()
    {
        Assert.Throws<InvalidDataException>(
            (Task() with
            {
                PlacementTarget = new PlacementTarget
                {
                    Mode = PlacementTargetMode.Story,
                    MapNumber = 1,
                    StoryRunKind = StoryRunKind.Infinite,
                },
            }).Validate);
        Assert.Throws<InvalidDataException>(
            (Task() with
            {
                PresetId = "legacy",
            }).Validate);
    }

    [Fact]
    public async Task Repository_RoundTripsOneLocalAccountState()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            BountyStateRepository repository =
                new(new AppPaths(root));
            BountyProgressState expected = new()
            {
                DailyEpochUtc =
                    new DateTimeOffset(
                        2026,
                        7,
                        30,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
                ClaimedToday = 3,
                Active =
                [
                    new BountyActiveProgress
                    {
                        Number = 5,
                        ObjectiveProgress =
                            new Dictionary<string, int>
                            {
                                ["rk-15"] = 1,
                            },
                    },
                ],
            };

            await repository.SaveAsync(expected);
            BountyProgressState actual =
                await repository.LoadAsync();

            Assert.Equal(
                expected.DailyEpochUtc,
                actual.DailyEpochUtc);
            Assert.Equal(3, actual.ClaimedToday);
            Assert.Equal(
                1,
                Assert.Single(actual.Active)
                    .ProgressFor("rk-15"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static MacroTaskDefinition Task() =>
        new()
        {
            Id = "bounty",
            Kind = MacroTaskKind.Bounty,
            Name = "Mythic Bounty Board",
            BountyParkedNonViableLimit = 2,
        };
}
