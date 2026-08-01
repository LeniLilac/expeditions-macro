using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class BountyRerollLimitPolicyTests
{
    private static readonly DateTimeOffset Day =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OrdinarySettledBoard_CountsOnePaidReroll()
    {
        BountyProgressState state = State(749);

        BountyRerollAccountingResult result =
            BountyRerollLimitPolicy
                .RecordOrdinarySettlement(
                    state,
                    Board(number: null),
                    Day.AddHours(1));

        Assert.True(result.Counted);
        Assert.Equal(750, result.State.RerollsToday);
        Assert.True(
            BountyRerollLimitPolicy.IsReached(
                result.State));
    }

    [Fact]
    public void OrdinaryNoGoldOrUnsettledState_DoesNotCount()
    {
        BountyProgressState state = State(12);

        BountyRerollAccountingResult noGold =
            BountyRerollLimitPolicy
                .RecordOrdinarySettlement(
                    state,
                    Board(number: null, noGold: true),
                    Day.AddHours(1));
        BountyRerollAccountingResult confirmation =
            BountyRerollLimitPolicy
                .RecordOrdinarySettlement(
                    state,
                    new BountyBoardMatch(
                        BountyBoardState
                            .RerollConfirmation,
                        0.9,
                        [],
                        [],
                        NoGold: false),
                    Day.AddHours(1));

        Assert.False(noGold.Counted);
        Assert.False(confirmation.Counted);
        Assert.Equal(12, noGold.State.RerollsToday);
        Assert.Equal(
            12,
            confirmation.State.RerollsToday);
    }

    [Fact]
    public void ConfirmedMythic_CountsOnlyAfterTheCardChanges()
    {
        BountyProgressState state = State(20);

        BountyRerollAccountingResult unchanged =
            BountyRerollLimitPolicy
                .RecordConfirmedMythicSettlement(
                    state,
                    Board(number: 5),
                    previousNumber: 5,
                    slot: 4,
                    rightView: true,
                    Day.AddHours(2));
        BountyRerollAccountingResult changedMythic =
            BountyRerollLimitPolicy
                .RecordConfirmedMythicSettlement(
                    state,
                    Board(number: 6),
                    previousNumber: 5,
                    slot: 4,
                    rightView: true,
                    Day.AddHours(2));
        BountyRerollAccountingResult changedOrdinary =
            BountyRerollLimitPolicy
                .RecordConfirmedMythicSettlement(
                    state,
                    Board(number: null),
                    previousNumber: 5,
                    slot: 4,
                    rightView: true,
                    Day.AddHours(2));

        Assert.False(unchanged.Counted);
        Assert.True(changedMythic.Counted);
        Assert.True(changedOrdinary.Counted);
        Assert.Equal(20, unchanged.State.RerollsToday);
        Assert.Equal(21, changedMythic.State.RerollsToday);
        Assert.Equal(21, changedOrdinary.State.RerollsToday);
    }

    [Fact]
    public void ConfirmedMythic_RequiresLiveSettledCardOwnership()
    {
        BountyProgressState state = State(20);
        BountyBoardMatch board = new(
            BountyBoardState.Board,
            0.9,
            [],
            [],
            NoGold: false);

        BountyRerollAccountingResult result =
            BountyRerollLimitPolicy
                .RecordConfirmedMythicSettlement(
                    state,
                    board,
                    previousNumber: 5,
                    slot: 4,
                    rightView: true,
                    Day.AddHours(2));

        Assert.False(result.Counted);
        Assert.Equal(20, result.State.RerollsToday);
    }

    [Fact]
    public void UtcMidnight_ResetsRerollsButPreservesActiveWork()
    {
        BountyProgressState state = State(750) with
        {
            ClaimedToday = 4,
            UnavailableNumbersToday = [1, 3],
            Active =
            [
                new BountyActiveProgress
                {
                    Number = 5,
                },
            ],
        };

        BountyProgressState next =
            state.RecordReroll(
                Day.AddDays(1).AddSeconds(1));

        Assert.Equal(1, next.RerollsToday);
        Assert.Equal(0, next.ClaimedToday);
        Assert.Empty(next.UnavailableNumbersToday);
        Assert.Equal(
            5,
            Assert.Single(next.Active).Number);
    }

    [Fact]
    public void ReachedLimit_WaitsForUtcResetInsteadOfMacroRestart()
    {
        DateTimeOffset now =
            Day.AddHours(23).AddMinutes(10);
        BountyProgressState state = State(750);

        Assert.False(
            BountyRerollLimitPolicy
                .ShouldRetryForGoldOnNextStart(
                    state,
                    goldUnavailable: true));
        Assert.Equal(
            Day.AddDays(1),
            BountyRerollLimitPolicy
                .NextUtcReset(now));
        Assert.Equal(
            now.AddMinutes(20),
            BountyRerollLimitPolicy
                .NextEligibility(
                    now.AddMinutes(20),
                    now));
        Assert.Equal(
            Day.AddDays(1),
            BountyRerollLimitPolicy
                .NextEligibility(
                    Day.AddDays(2),
                    now));
        Assert.True(
            BountyRerollLimitPolicy
                .ShouldRetryForGoldOnNextStart(
                    State(749),
                    goldUnavailable: true));
    }

    [Fact]
    public void State_RejectsRerollsPastTheHardLimit()
    {
        BountyProgressState invalid = State(751);

        Assert.Throws<InvalidDataException>(
            invalid.Validate);
        Assert.Throws<InvalidOperationException>(() =>
            State(750).RecordReroll(
                Day.AddHours(1)));
    }

    [Fact]
    public async Task CountedRerollPersistence_IgnoresNewCancellation()
    {
        CancellationToken observed =
            new(canceled: true);
        BountyProgressState state = State(1);

        await BountyRerollLimitPolicy
            .PersistCountedAsync(
                (value, token) =>
                {
                    observed = token;
                    Assert.Same(state, value);
                    return Task.CompletedTask;
                },
                state);

        Assert.False(observed.CanBeCanceled);
    }

    [Fact]
    public async Task Repository_LoadsExistingSchemaTwoWithoutLosingProgress()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            AppPaths paths = new(root);
            DateTimeOffset today =
                BountyProgressState.UtcDay(
                    DateTimeOffset.UtcNow);
            await JsonFileStore.WriteAtomicAsync(
                paths.BountyStateFile,
                new ExistingSchemaTwoState
                {
                    DailyEpochUtc = today,
                    ClaimedToday = 4,
                    UnavailableNumbersToday = [1, 3],
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
                });
            BountyStateRepository repository =
                new(paths);

            BountyProgressState loaded =
                await repository.LoadAsync();

            Assert.Equal(0, loaded.RerollsToday);
            Assert.Equal(4, loaded.ClaimedToday);
            Assert.Equal(
                new[] { 1, 3 },
                loaded.UnavailableNumbersToday);
            Assert.Equal(
                1,
                Assert.Single(loaded.Active)
                    .ProgressFor("rk-15"));
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task Repository_RoundTripsTheDailyRerollCount()
    {
        string root = TestPaths.NewTemporaryDirectory();
        try
        {
            BountyStateRepository repository =
                new(new AppPaths(root));
            BountyProgressState expected = State(749) with
            {
                DailyEpochUtc =
                    BountyProgressState.UtcDay(
                        DateTimeOffset.UtcNow),
            };

            await repository.SaveAsync(expected);
            BountyProgressState loaded =
                await repository.LoadAsync();

            Assert.Equal(749, loaded.RerollsToday);
        }
        finally
        {
            TestPaths.DeleteTemporaryDirectory(root);
        }
    }

    private static BountyProgressState State(
        int rerolls) =>
        new()
        {
            DailyEpochUtc = Day,
            RerollsToday = rerolls,
        };

    private static BountyBoardMatch Board(
        int? number,
        bool noGold = false)
    {
        const int actionX = 638;
        IReadOnlyList<BountyNumberMatch> numbers =
            number is int value
                ?
                [
                    new BountyNumberMatch(
                        value,
                        0.99,
                        actionX - 7,
                        335),
                ]
                : [];
        return new BountyBoardMatch(
            BountyBoardState.Board,
            0.95,
            [
                new BountyCardAction(
                    BountyCardActionKind.Reroll,
                    actionX,
                    440),
            ],
            numbers,
            noGold);
    }

    private sealed record ExistingSchemaTwoState
    {
        public int SchemaVersion { get; init; } = 2;
        public required DateTimeOffset DailyEpochUtc { get; init; }
        public int ClaimedToday { get; init; }
        public IReadOnlyList<int> UnavailableNumbersToday { get; init; } = [];
        public IReadOnlyList<BountyActiveProgress> Active { get; init; } = [];
        public DateTimeOffset UpdatedAtUtc { get; init; } =
            DateTimeOffset.UtcNow;
    }
}
