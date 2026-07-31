using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal sealed record BountyBoardProcessingResult(
    BountyProgressState State,
    bool GoldUnavailable);

internal sealed class BountyBoardProcessor
{
    private const int MaximumSlotCycles = 400;

    private readonly BountyBoardNavigator _navigator;

    public BountyBoardProcessor(
        BountyBoardNavigator navigator)
    {
        _navigator = navigator;
    }

    public async Task<BountyBoardProcessingResult>
        ProcessAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        int parkedLimit,
        BountyChallengeAvailability
            challengeAvailability,
        bool rerollEnabled,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        state = await ClaimReadyAsync(
                window,
                detector,
                state,
                log,
                cancellationToken)
            .ConfigureAwait(false);
        if (state.ClaimedToday >=
            BountyCatalog.DailyClaimLimit)
        {
            return new(
                state,
                GoldUnavailable: false);
        }
        if (!rerollEnabled)
        {
            return new(
                state,
                GoldUnavailable: true);
        }

        HashSet<int> observed = [];
        int parked = 0;
        bool noGold = false;
        for (int slot = 1;
             slot <= 4 && !noGold;
             slot++)
        {
            (state, parked, noGold) =
                await ProcessSlotAsync(
                        window,
                        detector,
                        state,
                        slot,
                        rightView: false,
                        parked,
                        parkedLimit,
                        challengeAvailability,
                        observed,
                        log,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        bool retentionTargetReached =
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                parked,
                parkedLimit,
                challengeAvailability);
        if (!noGold &&
            !retentionTargetReached)
        {
            await _navigator.ScrollAsync(
                window,
                detector,
                right: true,
                cancellationToken).ConfigureAwait(false);
            (state, parked, noGold) =
                await ProcessSlotAsync(
                        window,
                        detector,
                        state,
                        slot: 5,
                        rightView: true,
                        parked,
                        parkedLimit,
                        challengeAvailability,
                        observed,
                        log,
                        cancellationToken)
                    .ConfigureAwait(false);
            await _navigator.ScrollAsync(
                window,
                detector,
                right: false,
                cancellationToken).ConfigureAwait(false);
        }
        else if (retentionTargetReached)
        {
            log?.Invoke(
                "Bounty slot scanning stopped because every Mythic retained by the current parking and Challenge policy is already active.");
        }

        IReadOnlyList<BountyActiveProgress> reconciled =
            state.Active
                .Where(active =>
                    observed.Contains(active.Number) ||
                    noGold)
                .ToArray();
        return new(
            state with
            {
                Active = reconciled,
                UpdatedAtUtc =
                    DateTimeOffset.UtcNow,
            },
            noGold);
    }

    private async Task<BountyProgressState>
        ClaimReadyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        for (int slot = 1;
             slot <= 4 &&
             state.ClaimedToday <
                BountyCatalog.DailyClaimLimit;
             slot++)
        {
            state = await ClaimSlotAsync(
                    window,
                    detector,
                    state,
                    slot,
                    rightView: false,
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (state.ClaimedToday >=
            BountyCatalog.DailyClaimLimit)
        {
            return state;
        }
        await _navigator.ScrollAsync(
            window,
            detector,
            right: true,
            cancellationToken).ConfigureAwait(false);
        state = await ClaimSlotAsync(
                window,
                detector,
                state,
                slot: 5,
                rightView: true,
                log,
                cancellationToken)
            .ConfigureAwait(false);
        await _navigator.ScrollAsync(
            window,
            detector,
            right: false,
            cancellationToken).ConfigureAwait(false);
        return state;
    }

    private async Task<BountyProgressState>
        ClaimSlotAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        int slot,
        bool rightView,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        int? number = await _navigator.ClaimAsync(
                window,
                detector,
                slot,
                rightView,
                cancellationToken)
            .ConfigureAwait(false);
        if (number is not int claimed)
        {
            return state;
        }
        log?.Invoke(
            $"Claimed Mythic Bounty #{claimed}.");
        return state with
        {
            ClaimedToday = Math.Min(
                BountyCatalog.DailyClaimLimit,
                state.ClaimedToday + 1),
            Active = state.Active
                .Where(active =>
                    active.Number != claimed)
                .ToArray(),
            UpdatedAtUtc =
                DateTimeOffset.UtcNow,
        };
    }

    private async Task<(
        BountyProgressState State,
        int Parked,
        bool NoGold)> ProcessSlotAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        int slot,
        bool rightView,
        int parked,
        int parkedLimit,
        BountyChallengeAvailability
            challengeAvailability,
        ISet<int> observed,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        BountyRerollEvidenceTracker evidence = new();
        for (int cycle = 1;
             cycle <= MaximumSlotCycles;
             cycle++)
        {
            BountyBoardMatch result =
                await _navigator.ClickRerollAsync(
                        window,
                        detector,
                        slot,
                        rightView,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (result.NoGold)
            {
                log?.Invoke(
                    "Bounty rerolling stopped because the game reported that 1,000 Gold is unavailable.");
                return (state, parked, true);
            }
            if (result.State ==
                BountyBoardState.Board)
            {
                if (evidence.ObserveOrdinaryReroll())
                {
                    log?.Invoke(
                        $"Bounty slot {slot} showed no Mythic confirmation after {BountyRerollEvidenceTracker.OrdinaryRerollLimit} consecutive rerolls; Gold is treated as unavailable.");
                    return (state, parked, true);
                }
                continue;
            }
            if (result.State !=
                BountyBoardState
                    .RerollConfirmation)
            {
                throw new RobloxUiUnavailableException(
                    $"Bounty slot {slot} reached an unexpected reroll state.");
            }

            await _navigator.CancelRerollAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            BountyBoardMatch board =
                await _navigator.WaitForBoardAsync(
                    window,
                    detector,
                    cancellationToken).ConfigureAwait(false);
            int number =
                BountyBoardLayout.NumberForSlot(
                    board,
                    slot,
                    rightView) ??
                throw new RobloxUiUnavailableException(
                    $"The Mythic number in Bounty slot {slot} could not be recognized.");
            if (evidence.ObserveConfirmedMythic(
                    number))
            {
                log?.Invoke(
                    "Bounty rerolling stopped after the same confirmed Mythic remained unchanged four times.");
                return (state, parked, true);
            }

            bool reroll =
                BountyPlanner.ShouldReroll(
                    number,
                    parked,
                    parkedLimit,
                    challengeAvailability);
            if (!reroll)
            {
                BountyDefinition definition =
                    BountyCatalog.For(number);
                observed.Add(number);
                state = AddActive(
                    state,
                    number);
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
                return (state, parked, false);
            }

            evidence.MarkMythicRerolled(
                number);
            BountyBoardMatch confirmation =
                await _navigator.ClickRerollAsync(
                        window,
                        detector,
                        slot,
                        rightView,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (confirmation.NoGold)
            {
                return (state, parked, true);
            }
            if (confirmation.State !=
                BountyBoardState
                    .RerollConfirmation)
            {
                throw new RobloxUiUnavailableException(
                    "A verified Mythic reroll did not reopen its confirmation.");
            }
            try
            {
                await _navigator.ConfirmRerollAsync(
                    window,
                    detector,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (BountyGoldUnavailableException)
            {
                return (state, parked, true);
            }
            state = RemoveActive(
                state,
                number);
            log?.Invoke(
                $"Rerolled non-viable Mythic Bounty #{number}.");
        }

        throw new RobloxUiUnavailableException(
            $"Bounty slot {slot} exceeded its bounded {MaximumSlotCycles}-cycle reroll budget without reaching a retained card.");
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
