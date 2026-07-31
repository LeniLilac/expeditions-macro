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

    [Fact]
    public void LiveSlots_SkipADimmedCardAtAnyPosition()
    {
        BountyBoardMatch board = new(
            BountyBoardState.Board,
            Confidence: 0.91,
            Actions:
            [
                new(
                    BountyCardActionKind.Reroll,
                    X: 263,
                    Y: 360),
                new(
                    BountyCardActionKind.Reroll,
                    X: 388,
                    Y: 412),
                new(
                    BountyCardActionKind.Claim,
                    X: 598,
                    Y: 444),
                new(
                    BountyCardActionKind.Reroll,
                    X: 763,
                    Y: 379),
            ],
            Numbers: [],
            NoGold: false);

        IReadOnlyList<BountyLiveSlot> slots =
            BountyBoardLayout.LiveSlots(
                board,
                rightView: true);

        Assert.Equal(
            [1, 2, 4, 5],
            slots.Select(value =>
                value.Slot));
        Assert.Equal(
            BountyCardActionKind.Claim,
            slots.Single(value =>
                    value.Slot == 4)
                .Action.Kind);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void StableLiveActions_AllowOnlyThreePixelGeometryDrift(
        int drift,
        bool expected)
    {
        BountyLiveSlot[] previous =
        [
            new(
                2,
                new(
                    BountyCardActionKind.Reroll,
                    388,
                    412)),
        ];
        BountyLiveSlot[] current =
        [
            new(
                2,
                new(
                    BountyCardActionKind.Reroll,
                    388 + drift,
                    412 - drift)),
        ];

        Assert.Equal(
            expected,
            BountyBoardLiveActionObserver
                .AreSameStableActions(
                    previous,
                    current));
    }

    [Fact]
    public void StableLiveActions_RejectAChangedActionKind()
    {
        BountyLiveSlot[] previous =
        [
            new(
                4,
                new(
                    BountyCardActionKind.Reroll,
                    638,
                    445)),
        ];
        BountyLiveSlot[] current =
        [
            new(
                4,
                new(
                    BountyCardActionKind.Claim,
                    598,
                    445)),
        ];

        Assert.False(
            BountyBoardLiveActionObserver
                .AreSameStableActions(
                    previous,
                    current));
    }

    [Fact]
    public void RequestedRightView_RejectsStableUnmappedLeftActions()
    {
        BountyBoardMatch ignoredScroll = new(
            BountyBoardState.Board,
            Confidence: 0.9,
            Actions:
            [
                new(
                    BountyCardActionKind.Reroll,
                    X: 313,
                    Y: 412),
            ],
            Numbers: [],
            NoGold: false);
        IReadOnlyList<BountyLiveSlot> mapped =
            BountyBoardLayout.LiveSlots(
                ignoredScroll,
                rightView: true);

        Assert.Empty(mapped);
        Assert.False(
            BountyBoardLiveActionObserver
                .RepresentsRequestedView(
                    ignoredScroll,
                    mapped));
    }

    [Fact]
    public void RequestedView_AcceptsAConfirmedAllDimmedBoard()
    {
        BountyBoardMatch allDimmed = new(
            BountyBoardState.Board,
            Confidence: 0.9,
            Actions: [],
            Numbers: [],
            NoGold: false);

        Assert.True(
            BountyBoardLiveActionObserver
                .RepresentsRequestedView(
                    allDimmed,
                    []));
    }

    [Fact]
    public void ClaimSettlement_RejectsAnIgnoredRightView()
    {
        BountyBoardMatch leftView = new(
            BountyBoardState.Board,
            Confidence: 0.9,
            Actions:
            [
                new(
                    BountyCardActionKind.Reroll,
                    X: 313,
                    Y: 412),
            ],
            Numbers: [],
            NoGold: false);

        Assert.Null(
            BountyBoardLiveActionObserver
                .ClaimSettlementInRequestedView(
                    leftView,
                    slot: 1,
                    rightView: true));
    }

    [Theory]
    [InlineData(BountyCardActionKind.Claim, null)]
    [InlineData(
        BountyCardActionKind.Reroll,
        (int)BountyClaimSettlement.RerollAvailable)]
    public void ClaimSettlement_DistinguishesClaimFromReroll(
        BountyCardActionKind kind,
        int? expected)
    {
        BountyBoardMatch board = new(
            BountyBoardState.Board,
            Confidence: 0.9,
            Actions:
            [
                new(
                    kind,
                    X: kind ==
                        BountyCardActionKind.Claim
                            ? 223
                            : 263,
                    Y: 412),
            ],
            Numbers: [],
            NoGold: false);

        BountyClaimSettlement? settlement =
            BountyBoardLiveActionObserver
                .ClaimSettlementInRequestedView(
                    board,
                    slot: 1,
                    rightView: true);

        Assert.Equal(
            expected,
            settlement is BountyClaimSettlement
                value
                ? (int)value
                : null);
    }

    [Fact]
    public void ClaimSettlement_RecognizesTheClaimedSlotTurningDim()
    {
        BountyBoardMatch board = new(
            BountyBoardState.Board,
            Confidence: 0.9,
            Actions:
            [
                new(
                    BountyCardActionKind.Reroll,
                    X: 388,
                    Y: 412),
            ],
            Numbers: [],
            NoGold: false);

        Assert.Equal(
            BountyClaimSettlement.Dimmed,
            BountyBoardLiveActionObserver
                .ClaimSettlementInRequestedView(
                    board,
                    slot: 1,
                    rightView: true));
    }
}
