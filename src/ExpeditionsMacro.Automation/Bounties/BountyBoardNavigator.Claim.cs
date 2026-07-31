using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal readonly record struct BountyClaimResult(
    int Number,
    BountyClaimSettlement Settlement);

internal sealed partial class BountyBoardNavigator
{
    public async Task<BountyClaimResult?> ClaimAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken)
    {
        (BountyBoardMatch board,
            IReadOnlyList<BountyLiveSlot> slots) =
            await WaitForLiveSlotsAsync(
                    window,
                    detector,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        BountyCardAction? action = slots
            .Where(value =>
                value.Slot == slot &&
                value.Action.Kind ==
                    BountyCardActionKind.Claim)
            .Select(value => value.Action)
            .Cast<BountyCardAction?>()
            .FirstOrDefault();
        if (action is null)
        {
            return null;
        }
        int number =
            BountyBoardLayout.NumberForSlot(
                board,
                slot,
                rightView) ??
            throw new RobloxUiUnavailableException(
                $"The completed Mythic number in Bounty slot {slot} could not be recognized.");
        await ClickAsync(
            window,
            action.Value.X,
            action.Value.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.RewardOverlay,
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);
        (int X, int Y) dismiss =
            BountyBoardDetector.RewardDismissAction;
        await ClickAsync(
            window,
            dismiss.X,
            dismiss.Y,
            cancellationToken).ConfigureAwait(false);
        BountyClaimSettlement settlement =
            await _liveActions
                .WaitForClaimSettlementAsync(
                    window,
                    detector,
                    slot,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        return new(number, settlement);
    }
}
