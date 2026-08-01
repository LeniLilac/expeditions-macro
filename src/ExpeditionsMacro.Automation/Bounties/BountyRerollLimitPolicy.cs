using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal readonly record struct BountyRerollAccountingResult(
    BountyProgressState State,
    bool Counted);

internal static class BountyRerollLimitPolicy
{
    public static bool IsReached(
        BountyProgressState state) =>
        state.RerollsToday >=
        BountyCatalog.DailyRerollLimit;

    public static bool ShouldRetryForGoldOnNextStart(
        BountyProgressState state,
        bool goldUnavailable) =>
        goldUnavailable &&
        !IsReached(state);

    public static DateTimeOffset NextUtcReset(
        DateTimeOffset now) =>
        BountyProgressState.UtcDay(now)
            .AddDays(1);

    public static DateTimeOffset NextEligibility(
        DateTimeOffset? activeWorkReset,
        DateTimeOffset now)
    {
        DateTimeOffset utcReset =
            NextUtcReset(now);
        return activeWorkReset is
                DateTimeOffset active &&
            active < utcReset
                ? active
                : utcReset;
    }

    public static BountyRerollAccountingResult
        RecordOrdinarySettlement(
        BountyProgressState state,
        BountyBoardMatch settled,
        DateTimeOffset now) =>
        Record(
            state,
            settled.State ==
                BountyBoardState.Board &&
            !settled.NoGold,
            now);

    public static BountyRerollAccountingResult
        RecordConfirmedMythicSettlement(
        BountyProgressState state,
        BountyBoardMatch settled,
        int previousNumber,
        int slot,
        bool rightView,
        DateTimeOffset now)
    {
        BountyCardAction? liveReroll =
            BountyBoardLayout.FindAction(
                settled,
                slot,
                rightView,
                BountyCardActionKind.Reroll);
        int? currentNumber =
            BountyBoardLayout.NumberForSlot(
                settled,
                slot,
                rightView);
        bool changed =
            settled.State ==
                BountyBoardState.Board &&
            !settled.NoGold &&
            liveReroll is not null &&
            currentNumber != previousNumber;
        return Record(
            state,
            changed,
            now);
    }

    public static Task PersistCountedAsync(
        Func<
            BountyProgressState,
            CancellationToken,
            Task> persistState,
        BountyProgressState state)
    {
        ArgumentNullException.ThrowIfNull(
            persistState);
        return persistState(
            state,
            CancellationToken.None);
    }

    private static BountyRerollAccountingResult Record(
        BountyProgressState state,
        bool settledPaidReroll,
        DateTimeOffset now) =>
        settledPaidReroll
            ? new(
                state.RecordReroll(now),
                Counted: true)
            : new(
                state.AdvanceDay(now),
                Counted: false);
}
