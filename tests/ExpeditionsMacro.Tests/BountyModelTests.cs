using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class BountyModelTests
{
    [Fact]
    public void MythicTwo_UsesFlowerForestForItsWaveThirtyObjective()
    {
        BountyObjective objective =
            BountyCatalog.For(2)
                .Objectives.Single(value =>
                    value.Key == "ff-30");

        Assert.Equal(
            BountyObjectiveKind.InfiniteWave,
            objective.Kind);
        Assert.Equal(
            ChallengeMapId.FlowerForest,
            objective.Map);
        Assert.Equal(30, objective.TargetWave);
        Assert.DoesNotContain(
            BountyCatalog.For(2).Objectives,
            value =>
                value.Key == "fkf-30");
    }

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
                    BountyProgressState.UtcDay(
                        DateTimeOffset.UtcNow),
                ClaimedToday = 3,
                UnavailableNumbersToday = [2, 4, 5],
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
                new[] { 2, 4, 5 },
                actual.UnavailableNumbersToday);
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

    [Fact]
    public void RecordClaim_DimmedSlotExcludesTheNumber()
    {
        DateTimeOffset now =
            new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
        BountyProgressState state = new()
        {
            ClaimedToday = 4,
            UnavailableNumbersToday = [1, 2, 3, 4],
            Active =
            [
                new BountyActiveProgress
                {
                    Number = 5,
                },
            ],
        };

        BountyProgressState claimed =
            state.RecordClaim(
                5,
                excludeNumberUntilReset: true,
                now);

        Assert.Equal(5, claimed.ClaimedToday);
        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            claimed.UnavailableNumbersToday);
        Assert.Empty(claimed.Active);
    }

    [Fact]
    public void RecordClaim_RerollableSlotKeepsTheNumberAvailable()
    {
        DateTimeOffset now =
            new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
        BountyProgressState state = new()
        {
            ClaimedToday = 3,
            UnavailableNumbersToday = [1],
            Active =
            [
                new BountyActiveProgress
                {
                    Number = 5,
                },
            ],
        };

        BountyProgressState claimed =
            state.RecordClaim(
                5,
                excludeNumberUntilReset: false,
                now);

        Assert.Equal(4, claimed.ClaimedToday);
        Assert.Equal(
            new[] { 1 },
            claimed.UnavailableNumbersToday);
        Assert.Empty(claimed.Active);
    }

    [Fact]
    public void RecordClaim_RepeatedRerollableNumberCountsEveryClaim()
    {
        DateTimeOffset now =
            new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
        BountyProgressState state = new()
        {
            ClaimedToday = 3,
        };

        BountyProgressState first =
            state.RecordClaim(
                5,
                excludeNumberUntilReset: false,
                now);
        BountyProgressState second =
            first.RecordClaim(
                5,
                excludeNumberUntilReset: false,
                now.AddSeconds(1));

        Assert.Equal(5, second.ClaimedToday);
        Assert.Equal(
            Array.Empty<int>(),
            second.UnavailableNumbersToday);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void State_RejectsUnknownClaimedNumbers(
        int claimedNumber)
    {
        BountyProgressState state = new()
        {
            ClaimedToday = 1,
            UnavailableNumbersToday = [claimedNumber],
        };

        Assert.Throws<InvalidDataException>(
            state.Validate);
    }

    [Fact]
    public void State_RejectsDuplicateClaimedNumbers()
    {
        BountyProgressState state = new()
        {
            ClaimedToday = 2,
            UnavailableNumbersToday = [5, 5],
        };

        Assert.Throws<InvalidDataException>(
            state.Validate);
    }

    [Fact]
    public async Task Repository_ResetsTheRetiredBetaFortySevenState()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            await JsonFileStore.WriteAtomicAsync(
                paths.BountyStateFile,
                new BountyProgressState
                {
                    SchemaVersion = 1,
                    ClaimedToday = 4,
                    Active =
                    [
                        new BountyActiveProgress
                        {
                            Number = 2,
                            ObjectiveProgress =
                                new Dictionary<string, int>
                                {
                                    ["fkf-30"] = 1,
                                },
                        },
                    ],
                });
            BountyStateRepository repository =
                new(paths);

            BountyProgressState actual =
                await repository.LoadAsync();

            Assert.Equal(
                BountyProgressState.CurrentSchemaVersion,
                actual.SchemaVersion);
            Assert.Equal(0, actual.ClaimedToday);
            Assert.Empty(actual.Active);
            BountyProgressState? persisted =
                await JsonFileStore.ReadAsync<
                    BountyProgressState>(
                    paths.BountyStateFile);
            Assert.NotNull(persisted);
            Assert.Equal(
                BountyProgressState.CurrentSchemaVersion,
                persisted.SchemaVersion);
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
