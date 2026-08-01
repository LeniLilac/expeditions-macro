using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class BountyModelTests
{
    [Fact]
    public void TaskDefinition_DefaultsParkingToZero()
    {
        MacroTaskDefinition task = new()
        {
            Id = "bounty",
            Kind = MacroTaskKind.Bounty,
        };

        Assert.Equal(
            0,
            task.BountyParkedNonViableLimit);
    }

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

    [Fact]
    public void MythicTen_IsViableWithFiveChallengesStoryHardAndSchoolInfinite()
    {
        BountyDefinition bounty =
            BountyCatalog.For(10);

        Assert.False(bounty.AlwaysReroll);
        Assert.True(bounty.ChallengeConditional);
        Assert.Collection(
            bounty.Objectives,
            challenge =>
            {
                Assert.Equal(
                    BountyObjectiveKind.Challenge,
                    challenge.Kind);
                Assert.Equal(5, challenge.RequiredCount);
            },
            story =>
            {
                Assert.Equal(
                    BountyObjectiveKind.StoryActOneHard,
                    story.Kind);
                Assert.Equal(
                    ChallengeMapId.SchoolGrounds,
                    story.Map);
            },
            infinite =>
            {
                Assert.Equal(
                    BountyObjectiveKind.InfiniteWave,
                    infinite.Kind);
                Assert.Equal(
                    ChallengeMapId.SchoolGrounds,
                    infinite.Map);
                Assert.Equal(15, infinite.TargetWave);
            });
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
    public void ChallengePlacement_AdaptsStoryInfiniteWithoutMutatingSavedSetup()
    {
        PlacementTarget savedTarget = new()
        {
            Mode = PlacementTargetMode.Story,
            MapNumber = (int)ChallengeMapId.FlowerForest,
            StoryRunKind = StoryRunKind.Infinite,
            ActNumber = 1,
        };
        PlacementModel saved = new()
        {
            Id = "setup-story-map-2-infinite",
            Name = "Flower Forest Infinite",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = savedTarget,
            TeamSlot = 3,
            Steps =
            [
                new PlacementStep
                {
                    Kind = MatchStepKind.StartGame,
                    UnitKey = 0,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        PlacementModel runtime =
            BountyChallengePlacementPolicy
                .ForChallengeRuntime(
                    saved,
                    ChallengeMapId.FlowerForest);

        Assert.Same(savedTarget, saved.Target);
        Assert.Equal(PlacementTargetMode.Story, saved.Target.Mode);
        Assert.Equal(PlacementTargetMode.Challenge, runtime.Target!.Mode);
        Assert.Equal((int)ChallengeMapId.FlowerForest, runtime.Target.MapNumber);
        Assert.Equal(saved.Id, runtime.Id);
        Assert.Equal(saved.TeamSlot, runtime.TeamSlot);
        Assert.Same(saved.Steps, runtime.Steps);
        runtime.ValidateCompatibility(
            CameraPreparationMode.FastNoAlign,
            PlacementTarget.ForChallenge(
                ChallengeMapId.FlowerForest));
    }

    [Fact]
    public void ChallengePlacement_RejectsAnotherStoryMap()
    {
        PlacementModel saved = new()
        {
            Id = "setup-story-map-1-infinite",
            Name = "School Grounds Infinite",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = (int)ChallengeMapId.SchoolGrounds,
                StoryRunKind = StoryRunKind.Infinite,
                ActNumber = 1,
            },
            Steps =
            [
                new PlacementStep
                {
                    Kind = MatchStepKind.StartGame,
                    UnitKey = 0,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Throws<InvalidDataException>(() =>
            BountyChallengePlacementPolicy
                .ForChallengeRuntime(
                    saved,
                    ChallengeMapId.FlowerForest));
    }

    [Theory]
    [InlineData(ChallengeMapId.SchoolGrounds)]
    [InlineData(ChallengeMapId.FlowerForest)]
    [InlineData(ChallengeMapId.RoseKingdom)]
    [InlineData(ChallengeMapId.FairyKingForest)]
    [InlineData(ChallengeMapId.KingsTomb)]
    public void ChallengePlacement_AdaptsSharedStoryCategoryForEveryMap(
        ChallengeMapId map)
    {
        PlacementModel saved = new()
        {
            Id = $"setup-story-map-{(int)map}-shared",
            Name = $"Story map {(int)map} shared",
            ClientWidth = 808,
            ClientHeight = 611,
            CameraPreparationMode =
                CameraPreparationMode.FastNoAlign,
            Target = new PlacementTarget
            {
                Mode = PlacementTargetMode.Story,
                MapNumber = (int)map,
                StoryRunKind = StoryRunKind.Act,
                ActNumber =
                    PlacementSetupCatalog
                        .SharedStoryActNumber,
            },
            TeamSlot = 4,
            Steps =
            [
                new PlacementStep
                {
                    Kind = MatchStepKind.StartGame,
                    UnitKey = 0,
                    X = 0,
                    Y = 0,
                    DelayAfterMilliseconds = 0,
                },
            ],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        PlacementModel runtime =
            BountyChallengePlacementPolicy
                .ForChallengeRuntime(
                    saved,
                    map);

        Assert.Equal(
            PlacementTargetMode.Story,
            saved.Target!.Mode);
        Assert.Equal(
            PlacementSetupCatalog
                .SharedStoryActNumber,
            saved.Target.ActNumber);
        Assert.Equal(saved.Id, runtime.Id);
        Assert.Equal(saved.TeamSlot, runtime.TeamSlot);
        Assert.Same(saved.Steps, runtime.Steps);
        runtime.ValidateCompatibility(
            CameraPreparationMode.FastNoAlign,
            PlacementTarget.ForChallenge(map));
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
