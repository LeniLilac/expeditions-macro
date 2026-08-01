using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal enum BountySlotCycleDisposition
{
    Retry,
    Retained,
    GoldUnavailable,
    RerollLimitReached,
}

internal sealed record BountySlotCycleResult(
    BountyProgressState State,
    int Parked,
    BountySlotCycleDisposition Disposition);

internal sealed class BountySlotWorkItem(int slot)
{
    public int Slot { get; } = slot;

    public int CycleCount { get; private set; }

    public BountyRerollEvidenceTracker Evidence { get; } =
        new();

    public void BeginCycle(int maximumCycles)
    {
        CycleCount++;
        if (CycleCount > maximumCycles)
        {
            throw new RobloxUiUnavailableException(
                $"Bounty slot {Slot} exceeded its bounded {maximumCycles}-cycle reroll budget without reaching a retained card.");
        }
    }
}

internal sealed class BountyBoardSlotProcessor
{
    private readonly IBountyBoardProcessorNavigator
        _navigator;

    public BountyBoardSlotProcessor(
        IBountyBoardProcessorNavigator navigator)
    {
        _navigator = navigator;
    }

    public async Task<BountySlotCycleResult>
        ProcessCycleAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        BountySlotWorkItem work,
        bool rightView,
        int parked,
        int parkedLimit,
        BountyChallengeAvailability
            challengeAvailability,
        IReadOnlySet<int> unavailableToday,
        ISet<int> observed,
        Func<
            BountyProgressState,
            CancellationToken,
            Task> persistState,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        state = state.AdvanceDay(
            DateTimeOffset.UtcNow);
        if (BountyRerollLimitPolicy.IsReached(
                state))
        {
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .RerollLimitReached);
        }

        BountyBoardMatch result =
            await _navigator.ClickRerollAsync(
                    window,
                    detector,
                    work.Slot,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        if (result.NoGold)
        {
            log?.Invoke(
                "Bounty rerolling stopped because the game reported that 1,000 Gold is unavailable.");
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .GoldUnavailable);
        }
        if (result.State ==
            BountyBoardState.Board)
        {
            return await ProcessOrdinaryAsync(
                    state,
                    work,
                    parked,
                    result,
                    persistState,
                    log)
                .ConfigureAwait(false);
        }
        if (result.State !=
            BountyBoardState.RerollConfirmation)
        {
            throw new RobloxUiUnavailableException(
                $"Bounty slot {work.Slot} reached an unexpected reroll state.");
        }

        return await ProcessMythicAsync(
                window,
                detector,
                state,
                work,
                rightView,
                parked,
                parkedLimit,
                challengeAvailability,
                unavailableToday,
                observed,
                persistState,
                log,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BountySlotCycleResult>
        ProcessOrdinaryAsync(
        BountyProgressState state,
        BountySlotWorkItem work,
        int parked,
        BountyBoardMatch result,
        Func<
            BountyProgressState,
            CancellationToken,
            Task> persistState,
        Action<string>? log)
    {
        BountyRerollAccountingResult accounting =
            BountyRerollLimitPolicy
                .RecordOrdinarySettlement(
                    state,
                    result,
                    DateTimeOffset.UtcNow);
        state = accounting.State;
        if (accounting.Counted)
        {
            await BountyRerollLimitPolicy
                .PersistCountedAsync(
                    persistState,
                    state)
                .ConfigureAwait(false);
        }
        if (work.Evidence.ObserveOrdinaryReroll())
        {
            log?.Invoke(
                $"Bounty slot {work.Slot} showed no Mythic confirmation after {BountyRerollEvidenceTracker.OrdinaryRerollLimit} observations; Gold is treated as unavailable.");
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .GoldUnavailable);
        }
        return new(
            state,
            parked,
            BountyRerollLimitPolicy.IsReached(state)
                ? BountySlotCycleDisposition
                    .RerollLimitReached
                : BountySlotCycleDisposition.Retry);
    }

    private async Task<BountySlotCycleResult>
        ProcessMythicAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        BountySlotWorkItem work,
        bool rightView,
        int parked,
        int parkedLimit,
        BountyChallengeAvailability
            challengeAvailability,
        IReadOnlySet<int> unavailableToday,
        ISet<int> observed,
        Func<
            BountyProgressState,
            CancellationToken,
            Task> persistState,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        await _navigator.CancelRerollAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
        BountyBoardMatch board =
            await _navigator.WaitForBoardAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
        int number = BountyBoardLayout.NumberForSlot(
                board,
                work.Slot,
                rightView) ??
            throw new RobloxUiUnavailableException(
                $"The Mythic number in Bounty slot {work.Slot} could not be recognized.");
        if (work.Evidence.ObserveConfirmedMythic(
                number))
        {
            log?.Invoke(
                "Bounty rerolling stopped after the same confirmed Mythic remained unchanged four times.");
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .GoldUnavailable);
        }

        if (!BountyPlanner.ShouldReroll(
                number,
                unavailableToday,
                parked,
                parkedLimit,
                challengeAvailability))
        {
            return Retain(
                state,
                number,
                parked,
                parkedLimit,
                challengeAvailability,
                observed,
                log);
        }

        work.Evidence.MarkMythicRerolled(number);
        BountyBoardMatch confirmation =
            await _navigator.ClickRerollAsync(
                    window,
                    detector,
                    work.Slot,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        if (confirmation.NoGold)
        {
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .GoldUnavailable);
        }
        if (confirmation.State !=
            BountyBoardState.RerollConfirmation)
        {
            throw new RobloxUiUnavailableException(
                "A verified Mythic reroll did not reopen its confirmation.");
        }

        BountyBoardMatch settled;
        try
        {
            settled = await _navigator
                .ConfirmRerollAsync(
                    window,
                    detector,
                    work.Slot,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (BountyGoldUnavailableException)
        {
            return new(
                state,
                parked,
                BountySlotCycleDisposition
                    .GoldUnavailable);
        }
        BountyRerollAccountingResult accounting =
            BountyRerollLimitPolicy
                .RecordConfirmedMythicSettlement(
                    state,
                    settled,
                    number,
                    work.Slot,
                    rightView,
                    DateTimeOffset.UtcNow);
        state = accounting.State;
        if (!accounting.Counted)
        {
            return new(
                state,
                parked,
                BountySlotCycleDisposition.Retry);
        }

        state = RemoveActive(state, number);
        await BountyRerollLimitPolicy
            .PersistCountedAsync(
                persistState,
                state)
            .ConfigureAwait(false);
        log?.Invoke(
            $"Rerolled non-viable Mythic Bounty #{number}.");
        return new(
            state,
            parked,
            BountyRerollLimitPolicy.IsReached(state)
                ? BountySlotCycleDisposition
                    .RerollLimitReached
                : BountySlotCycleDisposition.Retry);
    }

    private static BountySlotCycleResult Retain(
        BountyProgressState state,
        int number,
        int parked,
        int parkedLimit,
        BountyChallengeAvailability
            challengeAvailability,
        ISet<int> observed,
        Action<string>? log)
    {
        BountyDefinition definition =
            BountyCatalog.For(number);
        observed.Add(number);
        state = AddActive(state, number);
        if (definition.AlwaysReroll)
        {
            parked++;
            log?.Invoke(
                $"Parked Mythic Bounty #{number} ({parked}/{parkedLimit}) to save reroll Gold.");
        }
        else if (definition.ChallengeConditional &&
                 challengeAvailability ==
                 BountyChallengeAvailability.Cooldown)
        {
            log?.Invoke(
                $"Parked Mythic Bounty #{number} until the next regular Challenge reset.");
        }
        else
        {
            log?.Invoke(
                $"Kept viable Mythic Bounty #{number}.");
        }
        return new(
            state,
            parked,
            BountySlotCycleDisposition.Retained);
    }

    private static BountyProgressState AddActive(
        BountyProgressState state,
        int number)
    {
        if (state.Active.Any(active =>
                active.Number == number))
        {
            return state;
        }
        return state with
        {
            Active =
            [
                .. state.Active,
                new BountyActiveProgress
                {
                    Number = number,
                },
            ],
        };
    }

    private static BountyProgressState RemoveActive(
        BountyProgressState state,
        int number) =>
        state with
        {
            Active = state.Active
                .Where(active =>
                    active.Number != number)
                .ToArray(),
        };
}
