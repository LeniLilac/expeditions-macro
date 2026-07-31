using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal readonly record struct BountyLiveSlot(
    int Slot,
    BountyCardAction Action);

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
                Math.Abs(
                    value.CardAnchorX -
                    column))
            .Cast<BountyCardAction?>()
            .FirstOrDefault();
        return action is not null &&
            Math.Abs(
                action.Value.CardAnchorX -
                column) <= 18
            ? action
            : null;
    }

    public static IReadOnlyList<BountyLiveSlot>
        LiveSlots(
        BountyBoardMatch board,
        bool rightView)
    {
        int[] columns = rightView
            ? RightSlotColumns
            : LeftSlotColumns;
        return board.Actions
            .Select(action =>
            {
                int slot = Enumerable.Range(
                        0,
                        columns.Length)
                    .OrderBy(index =>
                        Math.Abs(
                            columns[index] -
                            action.CardAnchorX))
                    .First();
                return new
                {
                    Slot = slot + 1,
                    Distance = Math.Abs(
                        columns[slot] -
                        action.CardAnchorX),
                    Action = action,
                };
            })
            .Where(value =>
                value.Distance <= 18)
            .GroupBy(value =>
                value.Slot)
            .Select(group =>
            {
                var selected = group
                    .OrderByDescending(value =>
                        value.Action.Kind ==
                        BountyCardActionKind.Claim)
                    .ThenBy(value =>
                        value.Distance)
                    .First();
                return new BountyLiveSlot(
                    selected.Slot,
                    selected.Action);
            })
            .OrderBy(value =>
                value.Slot)
            .ToArray();
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
