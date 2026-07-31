using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal enum BountyClaimSettlement
{
    Dimmed,
    RerollAvailable,
}

internal sealed class BountyBoardLiveActionObserver
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);
    private readonly Func<
        RobloxWindow,
        IDetectorPack,
        ImageFrame> _capture;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;

    public BountyBoardLiveActionObserver(
        Func<
            RobloxWindow,
            IDetectorPack,
            ImageFrame> capture,
        Func<
            TimeSpan,
            CancellationToken,
            Task> delay,
        Func<DateTimeOffset> utcNow)
    {
        _capture = capture;
        _delay = delay;
        _utcNow = utcNow;
    }

    public async Task<(
        BountyBoardMatch Board,
        IReadOnlyList<BountyLiveSlot> Slots)>
        WaitForLiveSlotsAsync(
        RobloxWindow window,
        IDetectorPack detector,
        bool rightView,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(8),
            minimumObservations: 2,
            _utcNow);
        IReadOnlyList<BountyLiveSlot>? candidate =
            null;
        int stable = 0;
        BountyBoardMatch last = default;
        IReadOnlyList<BountyLiveSlot> slots = [];
        while (budget.ShouldObserve(stable > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = BountyBoardDetector.Detect(
                _capture(window, detector));
            budget.MarkObserved();
            if (last.State != BountyBoardState.Board)
            {
                candidate = null;
                stable = 0;
            }
            else
            {
                slots = BountyBoardLayout.LiveSlots(
                    last,
                    rightView);
                if (!RepresentsRequestedView(
                        last,
                        slots))
                {
                    candidate = null;
                    stable = 0;
                }
                else
                {
                    stable = AreSameStableActions(
                            candidate,
                            slots)
                        ? stable + 1
                        : 1;
                    candidate = slots.ToArray();
                    if (stable >= 2)
                    {
                        return (last, slots);
                    }
                }
            }
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            "The Bounty Board live-card actions did not become stable.");
    }

    public async Task<BountyClaimSettlement>
        WaitForClaimSettlementAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(8),
            minimumObservations: 2,
            _utcNow);
        BountyClaimSettlement? candidate =
            null;
        int stable = 0;
        while (budget.ShouldObserve(stable > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BountyBoardMatch board =
                BountyBoardDetector.Detect(
                    _capture(window, detector));
            budget.MarkObserved();
            BountyClaimSettlement? settlement =
                board.State ==
                    BountyBoardState.Board
                    ? ClaimSettlementInRequestedView(
                        board,
                        slot,
                        rightView)
                    : null;
            if (settlement is null)
            {
                candidate = null;
                stable = 0;
            }
            else
            {
                stable = candidate == settlement
                    ? stable + 1
                    : 1;
                candidate = settlement;
            }
            if (stable >= 2 &&
                candidate is BountyClaimSettlement
                    confirmed)
            {
                return confirmed;
            }
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            $"Claimed Bounty slot {slot} did not settle to a consumed card.");
    }

    internal static bool AreSameStableActions(
        IReadOnlyList<BountyLiveSlot>? left,
        IReadOnlyList<BountyLiveSlot> right)
    {
        if (left is null ||
            left.Count != right.Count)
        {
            return false;
        }
        for (int index = 0;
             index < left.Count;
             index++)
        {
            BountyLiveSlot previous = left[index];
            BountyLiveSlot current = right[index];
            if (previous.Slot != current.Slot ||
                previous.Action.Kind !=
                    current.Action.Kind ||
                Math.Abs(
                    previous.Action.X -
                    current.Action.X) > 3 ||
                Math.Abs(
                    previous.Action.Y -
                    current.Action.Y) > 3)
            {
                return false;
            }
        }
        return true;
    }

    internal static bool RepresentsRequestedView(
        BountyBoardMatch board,
        IReadOnlyList<BountyLiveSlot> slots) =>
        board.Actions.Count == 0 ||
        slots.Count > 0;

    internal static BountyClaimSettlement?
        ClaimSettlementInRequestedView(
        BountyBoardMatch board,
        int slot,
        bool rightView)
    {
        IReadOnlyList<BountyLiveSlot> slots =
            BountyBoardLayout.LiveSlots(
                board,
                rightView);
        if (!RepresentsRequestedView(
                board,
                slots))
        {
            return null;
        }
        BountyLiveSlot? target = slots
            .Where(value =>
                value.Slot == slot)
            .Cast<BountyLiveSlot?>()
            .FirstOrDefault();
        return target?.Action.Kind switch
        {
            BountyCardActionKind.Reroll =>
                BountyClaimSettlement
                    .RerollAvailable,
            BountyCardActionKind.Claim =>
                null,
            null =>
                BountyClaimSettlement.Dimmed,
            _ => null,
        };
    }
}
