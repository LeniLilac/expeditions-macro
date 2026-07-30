using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal static class BountyBoardLayout
{
    private static readonly int[] LeftSlotColumns =
        [313, 438, 563, 688, 813];
    private static readonly int[] RightSlotColumns =
        [263, 388, 513, 638, 763];

    public static int? NumberForSlot(
        BountyBoardMatch board,
        int slot,
        bool rightView)
    {
        int column = SlotColumn(
            slot,
            rightView);
        BountyNumberMatch? match = board.Numbers
            .OrderBy(value =>
                Math.Abs(
                    value.CenterX -
                    (column - 7)))
            .Cast<BountyNumberMatch?>()
            .FirstOrDefault();
        return match is not null &&
            Math.Abs(
                match.Value.CenterX -
                (column - 7)) <= 12
            ? match.Value.Number
            : null;
    }

    public static BountyCardAction RequireAction(
        BountyBoardMatch board,
        int slot,
        bool rightView,
        BountyCardActionKind kind) =>
        FindAction(
            board,
            slot,
            rightView,
            kind) ??
        throw new RobloxUiUnavailableException(
            $"Bounty slot {slot} has no verified {kind} action.");

    public static BountyCardAction? FindAction(
        BountyBoardMatch board,
        int slot,
        bool rightView,
        BountyCardActionKind kind)
    {
        int column = SlotColumn(
            slot,
            rightView);
        BountyCardAction? action = board.Actions
            .Where(value => value.Kind == kind)
            .OrderBy(value =>
                Math.Abs(value.X - column))
            .Cast<BountyCardAction?>()
            .FirstOrDefault();
        return action is not null &&
            Math.Abs(
                action.Value.X -
                column) <= 18
            ? action
            : null;
    }

    private static int SlotColumn(
        int slot,
        bool rightView)
    {
        if (slot is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot));
        }
        return (rightView
                ? RightSlotColumns
                : LeftSlotColumns)[slot - 1];
    }
}
