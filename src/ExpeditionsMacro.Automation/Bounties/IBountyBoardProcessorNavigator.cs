using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal interface IBountyBoardProcessorNavigator
{
    Task ScrollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        bool right,
        CancellationToken cancellationToken);

    Task<(
        BountyBoardMatch Board,
        IReadOnlyList<BountyLiveSlot> Slots)>
        WaitForLiveSlotsAsync(
        RobloxWindow window,
        IDetectorPack detector,
        bool rightView,
        CancellationToken cancellationToken);

    Task<BountyClaimResult?> ClaimAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken);

    Task<BountyBoardMatch> ClickRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken);

    Task CancelRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken);

    Task<BountyBoardMatch> WaitForBoardAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken);

    Task<BountyBoardMatch> ConfirmRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken);
}
