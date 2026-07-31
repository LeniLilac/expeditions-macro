using ExpeditionsMacro.Core.Models;

namespace ExpeditionsMacro.Tests;

public sealed class BountyPlannerTests
{
    [Fact]
    public void DailyChallengeLimit_WithZeroParking_StopsAfterAllFourViableBounties()
    {
        HashSet<int> observed =
            [2, 4, 5, 6];

        Assert.True(
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                unavailableToday: new HashSet<int>(),
                parkedNonViable: 0,
                parkedLimit: 0,
                BountyChallengeAvailability.DailyLimit));
    }

    [Theory]
    [InlineData(BountyChallengeAvailability.Available)]
    [InlineData(BountyChallengeAvailability.Cooldown)]
    public void ChallengeEligiblePolicies_StillScanForConditionalBounties(
        BountyChallengeAvailability availability)
    {
        HashSet<int> observed =
            [2, 4, 5, 6];

        Assert.False(
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                unavailableToday: new HashSet<int>(),
                parkedNonViable: 0,
                parkedLimit: 0,
                availability));
    }

    [Fact]
    public void ParkingBudget_MustBeFilledBeforeSlotScanningStops()
    {
        HashSet<int> observed =
            [2, 4, 5, 6];

        Assert.False(
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                unavailableToday: new HashSet<int>(),
                parkedNonViable: 0,
                parkedLimit: 1,
                BountyChallengeAvailability.DailyLimit));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(3, 4, false)]
    [InlineData(4, 4, true)]
    public void ParkingLimit_TradesRerollGoldForParkedNonViableBounties(
        int alreadyParked,
        int limit,
        bool shouldReroll)
    {
        Assert.Equal(
            shouldReroll,
            BountyPlanner.ShouldReroll(
                bountyNumber: 1,
                unavailableToday: new HashSet<int>(),
                alreadyParked,
                limit,
                BountyChallengeAvailability.Available));
    }

    [Fact]
    public void ConditionalBounty_WaitsForOrdinaryCooldown_ButRerollsAtDailyLimit()
    {
        foreach (int number in new[] { 7, 9 })
        {
            Assert.False(
                BountyPlanner.ShouldReroll(
                    number,
                    unavailableToday: new HashSet<int>(),
                    parkedNonViable: 4,
                    parkedLimit: 0,
                    BountyChallengeAvailability.Cooldown));
            Assert.True(
                BountyPlanner.ShouldReroll(
                    number,
                    unavailableToday: new HashSet<int>(),
                    parkedNonViable: 0,
                    parkedLimit: 4,
                    BountyChallengeAvailability.DailyLimit));
        }
    }

    [Fact]
    public void ViableBounties_NeverConsumeTheParkingBudget()
    {
        foreach (int number in new[] { 2, 4, 5, 6 })
        {
            Assert.False(
                BountyPlanner.ShouldReroll(
                    number,
                    unavailableToday: new HashSet<int>(),
                    parkedNonViable: 4,
                    parkedLimit: 0,
                    BountyChallengeAvailability.Available));
        }
    }

    [Fact]
    public void DimmedClaimedBounty_IsExcludedUntilTheNextDailyReset()
    {
        HashSet<int> unavailableToday = [5];
        HashSet<int> observed =
            [2, 4, 6, 7, 9];

        Assert.True(
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                unavailableToday,
                parkedNonViable: 0,
                parkedLimit: 0,
                BountyChallengeAvailability.Available));
        Assert.True(
            BountyPlanner.ShouldReroll(
                bountyNumber: 5,
                unavailableToday,
                parkedNonViable: 0,
                parkedLimit: 0,
                BountyChallengeAvailability.Available));
    }

    [Fact]
    public void ClaimedNonViableBounty_ReducesTheReachableParkingTarget()
    {
        Assert.True(
            BountyPlanner.HasEveryRetainableBounty(
                observed:
                    new HashSet<int>
                    {
                        2,
                        4,
                        5,
                        6,
                        7,
                        9,
                    },
                unavailableToday:
                    new HashSet<int> { 1 },
                parkedNonViable: 3,
                parkedLimit: 4,
                BountyChallengeAvailability.Available));
    }

    [Fact]
    public void RoutePlanner_MergesOverlappingRoseKingdomObjectives()
    {
        BountyActiveProgress[] active =
        [
            Active(5),
            Active(9),
        ];

        IReadOnlyList<BountyWorkRoute> routes =
            BountyPlanner.BuildRoutes(
                active,
                BountyChallengeAvailability.Available);

        BountyWorkRoute infinite = Assert.Single(
            routes,
            route =>
                route.Kind ==
                BountyObjectiveKind.InfiniteWave);
        Assert.Equal(ChallengeMapId.RoseKingdom, infinite.Map);
        Assert.Equal(45, infinite.TargetWave);
        Assert.Equal(new[] { 5, 9 }, infinite.CoveredBounties);
    }

    [Fact]
    public void RoutePlanner_MergesOnlyObjectivesOnTheSameMap()
    {
        BountyActiveProgress[] active =
        [
            Active(2),
            Active(4),
        ];

        IReadOnlyList<BountyWorkRoute> routes =
            BountyPlanner.BuildRoutes(
                active,
                BountyChallengeAvailability.Available);

        Assert.Contains(
            routes,
            route =>
                route.Kind ==
                    BountyObjectiveKind.RaidActOne &&
                route.CoveredBounties.SequenceEqual(
                    new[] { 2, 4 }));
        Assert.Contains(
            routes,
            route =>
                route.Kind ==
                    BountyObjectiveKind.InfiniteWave &&
                route.Map ==
                    ChallengeMapId.SchoolGrounds &&
                route.TargetWave == 30 &&
                route.CoveredBounties.SequenceEqual(
                    new[] { 2, 4 }));
        Assert.Contains(
            routes,
            route =>
                route.Kind ==
                    BountyObjectiveKind.InfiniteWave &&
                route.Map ==
                    ChallengeMapId.FairyKingForest &&
                route.TargetWave == 15 &&
                route.CoveredBounties.SequenceEqual(
                    new[] { 4 }));
        Assert.Contains(
            routes,
            route =>
                route.Kind ==
                    BountyObjectiveKind.InfiniteWave &&
                route.Map ==
                    ChallengeMapId.FlowerForest &&
                route.TargetWave == 30 &&
                route.CoveredBounties.SequenceEqual(
                    new[] { 2 }));
    }

    [Fact]
    public void CompletedRoute_UpdatesOnlyCoveredObjectives()
    {
        BountyActiveProgress[] active =
        [
            Active(5),
            Active(9),
        ];
        BountyWorkRoute route = new()
        {
            Kind = BountyObjectiveKind.InfiniteWave,
            Map = ChallengeMapId.RoseKingdom,
            TargetWave = 45,
            CoveredBounties = [5, 9],
        };

        IReadOnlyList<BountyActiveProgress> updated =
            BountyPlanner.ApplyCompletedRoute(
                active,
                route);

        Assert.True(
            BountyPlanner.IsComplete(
                updated.Single(value =>
                    value.Number == 5)));
        BountyActiveProgress conditional =
            updated.Single(value =>
                value.Number == 9);
        Assert.Equal(
            1,
            conditional.ProgressFor("rk-45"));
        Assert.Equal(
            0,
            conditional.ProgressFor("challenge-1"));
        Assert.False(
            BountyPlanner.IsComplete(conditional));
    }

    [Fact]
    public void ClaimableBounty_DoesNotInterruptRemainingActiveWork()
    {
        BountyActiveProgress[] active =
        [
            Progress(
                2,
                ("ff-30", 1),
                ("sg-30", 1)),
            Progress(
                4,
                ("fkf-15", 1),
                ("sg-30", 1)),
            Progress(
                6,
                ("fkf-60", 1),
                ("fkf-30", 1)),
            Active(7),
            Active(9),
        ];

        Assert.False(
            BountyPlanner.HasClaimableBounty(active));

        BountyWorkRoute raid = new()
        {
            Kind = BountyObjectiveKind.RaidActOne,
            CoveredBounties = [2, 4],
        };
        IReadOnlyList<BountyActiveProgress> completed =
            BountyPlanner.ApplyCompletedRoute(
                active,
                raid);

        Assert.True(
            BountyPlanner.HasClaimableBounty(
                completed));
        Assert.True(
            BountyPlanner.HasExecutableWork(
                completed,
                BountyChallengeAvailability.Available));
    }

    [Fact]
    public void BoardReconciliationCanResumeAfterEveryExecutableRouteCompletes()
    {
        BountyActiveProgress[] active =
        [
            Progress(
                2,
                ("ff-30", 1),
                ("sg-30", 1),
                ("raid-spirit-city-act-1", 1)),
            Progress(
                4,
                ("fkf-15", 1),
                ("sg-30", 1),
                ("raid-spirit-city-act-1", 1)),
        ];

        Assert.True(
            BountyPlanner.HasClaimableBounty(
                active));
        Assert.False(
            BountyPlanner.HasExecutableWork(
                active,
                BountyChallengeAvailability.Available));
    }

    [Fact]
    public void UtcDailyReset_ClearsClaimIdentityButKeepsActiveProgress()
    {
        DateTimeOffset previous =
            new(2026, 7, 30, 23, 59, 0, TimeSpan.Zero);
        BountyProgressState state = new()
        {
            DailyEpochUtc =
                BountyProgressState.UtcDay(previous),
            ClaimedToday = 8,
            UnavailableNumbersToday = [1, 5],
            Active = [Active(6)],
        };

        BountyProgressState next = state.AdvanceDay(
            previous.AddMinutes(2));

        Assert.Equal(0, next.ClaimedToday);
        Assert.Empty(next.UnavailableNumbersToday);
        Assert.Single(next.Active);
        Assert.Equal(6, next.Active[0].Number);
    }

    [Fact]
    public void RequiredRoutes_AreRaidActOneAndEveryStoryInfiniteMap()
    {
        Assert.Collection(
            BountyCatalog.RequiredPlacementTargets,
            raid =>
            {
                Assert.Equal(PlacementTargetMode.Raid, raid.Mode);
                Assert.Equal(1, raid.MapNumber);
                Assert.Equal(1, raid.ActNumber);
            },
            story => AssertInfinite(story, 1),
            story => AssertInfinite(story, 2),
            story => AssertInfinite(story, 3),
            story => AssertInfinite(story, 4),
            story => AssertInfinite(story, 5));
    }

    [Fact]
    public void StoryInfiniteDependencies_CoverEveryChallengeMap()
    {
        ChallengeMapId[] challengeMaps =
            Enum.GetValues<ChallengeMapId>();
        PlacementTarget[] storyMaps =
            BountyCatalog.RequiredPlacementTargets
                .Where(target =>
                    target.Mode ==
                    PlacementTargetMode.Story)
                .ToArray();

        Assert.Equal(
            challengeMaps.Select(map => (int)map),
            storyMaps.Select(target =>
                target.MapNumber));
        Assert.All(
            storyMaps,
            target =>
                Assert.Equal(
                    StoryRunKind.Infinite,
                    target.StoryRunKind));
    }

    private static BountyActiveProgress Active(
        int number) =>
        new()
        {
            Number = number,
        };

    private static BountyActiveProgress Progress(
        int number,
        params (string Key, int Value)[] values) =>
        new()
        {
            Number = number,
            ObjectiveProgress = values.ToDictionary(
                value => value.Key,
                value => value.Value),
        };

    private static void AssertInfinite(
        PlacementTarget target,
        int map)
    {
        Assert.Equal(PlacementTargetMode.Story, target.Mode);
        Assert.Equal(map, target.MapNumber);
        Assert.Equal(StoryRunKind.Infinite, target.StoryRunKind);
    }
}
