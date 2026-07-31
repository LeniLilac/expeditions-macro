using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class BountyBoardLayoutTests
{
    [Fact]
    public void FindAction_MapsTheClaimButtonToItsOwningSlot()
    {
        BountyBoardMatch board = new(
            BountyBoardState.Board,
            Confidence: 0.91,
            Actions:
            [
                new(
                    BountyCardActionKind.Claim,
                    X: 648,
                    Y: 444),
            ],
            Numbers:
            [
                new(
                    Number: 4,
                    Confidence: 1,
                    CenterX: 681,
                    CenterY: 347),
            ],
            NoGold: false);

        BountyCardAction? action =
            BountyBoardLayout.FindAction(
                board,
                slot: 4,
                rightView: false,
                BountyCardActionKind.Claim);

        Assert.True(action.HasValue);
        Assert.Equal(648, action.Value.X);
        Assert.Equal(
            4,
            BountyBoardLayout.NumberForSlot(
                board,
                slot: 4,
                rightView: false));
    }
}
