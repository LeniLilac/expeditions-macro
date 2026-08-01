using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal sealed record BountyBoardProcessingResult(
    BountyProgressState State,
    bool GoldUnavailable,
    bool RerollLimitReached);

internal sealed class BountyBoardProcessor
{
    private const int MaximumSlotCycles = 400;
    private const int MaximumTotalCycles =
        MaximumSlotCycles * 5;

    private readonly IBountyBoardProcessorNavigator
        _navigator;
    private readonly BountyBoardSlotProcessor
        _slotProcessor;

    public BountyBoardProcessor(
        IBountyBoardProcessorNavigator navigator)
    {
        _navigator = navigator;
        _slotProcessor =
            new BountyBoardSlotProcessor(navigator);
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
        Func<
            BountyProgressState,
            CancellationToken,
            Task> persistState,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            persistState);
        await _navigator.ScrollAsync(
            window,
            detector,
            right: true,
            cancellationToken).ConfigureAwait(false);
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
            await ScrollLeftAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            return new(
                state,
                GoldUnavailable: false,
                RerollLimitReached: false);
        }
        if (BountyRerollLimitPolicy.IsReached(
                state))
        {
            LogRerollLimit(log);
            await ScrollLeftAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            return new(
                state,
                GoldUnavailable: false,
                RerollLimitReached: true);
        }
        if (!rerollEnabled)
        {
            await ScrollLeftAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
            return new(
                state,
                GoldUnavailable: true,
                RerollLimitReached: false);
        }

        (_, IReadOnlyList<BountyLiveSlot> liveSlots) =
            await _navigator.WaitForLiveSlotsAsync(
                    window,
                    detector,
                    rightView: true,
                    cancellationToken)
                .ConfigureAwait(false);
        HashSet<int> observed = [];
        HashSet<int> unavailableToday =
            state.UnavailableNumbersToday.ToHashSet();
        Queue<BountySlotWorkItem> pending = new(
            liveSlots
                .Where(value =>
                    value.Action.Kind ==
                    BountyCardActionKind.Reroll)
                .Select(value =>
                    new BountySlotWorkItem(
                        value.Slot)));
        int parked = 0;
        int totalCycles = 0;
        bool noGold = false;
        bool rerollLimitReached = false;
        bool retentionTargetReached = false;
        while (pending.Count > 0)
        {
            retentionTargetReached =
                BountyPlanner.HasEveryRetainableBounty(
                    observed,
                    unavailableToday,
                    parked,
                    parkedLimit,
                    challengeAvailability);
            if (retentionTargetReached)
            {
                break;
            }
            if (++totalCycles >
                MaximumTotalCycles)
            {
                throw new RobloxUiUnavailableException(
                    $"Bounty board exceeded its bounded {MaximumTotalCycles}-cycle reroll budget.");
            }

            BountySlotWorkItem work =
                pending.Dequeue();
            work.BeginCycle(MaximumSlotCycles);
            BountySlotCycleResult cycle =
                await _slotProcessor.ProcessCycleAsync(
                        window,
                        detector,
                        state,
                        work,
                        rightView: true,
                        parked,
                        parkedLimit,
                        challengeAvailability,
                        unavailableToday,
                        observed,
                        persistState,
                        log,
                        cancellationToken)
                    .ConfigureAwait(false);
            state = cycle.State;
            parked = cycle.Parked;
            switch (cycle.Disposition)
            {
                case BountySlotCycleDisposition.Retry:
                    pending.Enqueue(work);
                    break;
                case BountySlotCycleDisposition
                    .GoldUnavailable:
                    noGold = true;
                    break;
                case BountySlotCycleDisposition
                    .RerollLimitReached:
                    rerollLimitReached = true;
                    break;
                case BountySlotCycleDisposition.Retained:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (noGold || rerollLimitReached)
            {
                break;
            }
        }

        retentionTargetReached =
            retentionTargetReached ||
            BountyPlanner.HasEveryRetainableBounty(
                observed,
                unavailableToday,
                parked,
                parkedLimit,
                challengeAvailability);
        if (retentionTargetReached)
        {
            log?.Invoke(
                "Bounty slot scanning stopped because every Mythic retained by the current parking and Challenge policy is already active.");
        }
        if (rerollLimitReached)
        {
            LogRerollLimit(log);
        }
        await ScrollLeftAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<BountyActiveProgress> reconciled =
            state.Active
                .Where(active =>
                    observed.Contains(active.Number) ||
                    noGold ||
                    rerollLimitReached)
                .ToArray();
        return new(
            state with
            {
                Active = reconciled,
                UpdatedAtUtc =
                    DateTimeOffset.UtcNow,
            },
            noGold,
            rerollLimitReached);
    }

    private async Task<BountyProgressState>
        ClaimReadyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyProgressState state,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        while (state.ClaimedToday <
               BountyCatalog.DailyClaimLimit)
        {
            (_, IReadOnlyList<BountyLiveSlot> liveSlots) =
                await _navigator.WaitForLiveSlotsAsync(
                        window,
                        detector,
                        rightView: true,
                        cancellationToken)
                    .ConfigureAwait(false);
            BountyLiveSlot? claim = liveSlots
                .Where(value =>
                    value.Action.Kind ==
                    BountyCardActionKind.Claim)
                .Cast<BountyLiveSlot?>()
                .FirstOrDefault();
            if (claim is null)
            {
                return state;
            }
            state = await ClaimSlotAsync(
                    window,
                    detector,
                    state,
                    claim.Value.Slot,
                    rightView: true,
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
        BountyClaimResult? result =
            await _navigator.ClaimAsync(
                window,
                detector,
                slot,
                rightView,
                cancellationToken)
            .ConfigureAwait(false);
        if (result is not BountyClaimResult claim)
        {
            return state;
        }
        log?.Invoke(
            claim.Settlement ==
                BountyClaimSettlement.Dimmed
                ? $"Claimed Mythic Bounty #{claim.Number}; its dimmed slot removes that number from today's reroll pool."
                : $"Claimed Mythic Bounty #{claim.Number}; its live Reroll action keeps that number in today's reroll pool.");
        return state.RecordClaim(
            claim.Number,
            excludeNumberUntilReset:
                claim.Settlement ==
                BountyClaimSettlement.Dimmed,
            DateTimeOffset.UtcNow);
    }

    private Task ScrollLeftAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken) =>
        _navigator.ScrollAsync(
            window,
            detector,
            right: false,
            cancellationToken);

    private static void LogRerollLimit(
        Action<string>? log) =>
        log?.Invoke(
            $"Bounty rerolling stopped at the {BountyCatalog.DailyRerollLimit}-reroll UTC-day safety limit. Active Bounty work may continue; new rerolls resume after midnight UTC.");
}
