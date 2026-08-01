using ExpeditionsMacro.Automation.Bounties;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Tests;

public sealed class BountyBoardProcessorTests
{
    private static readonly int[] RightColumns =
        [263, 388, 513, 638, 763];

    [Fact]
    public async Task ProcessAsync_ObservesViableSlotFiveBeforeRetryingEarlierSlot()
    {
        RoundRobinNavigator navigator = new(
            new Dictionary<int, int?>
            {
                [1] = 4,
                [2] = 5,
                [3] = 6,
                [4] = null,
                [5] = 2,
            });
        BountyBoardProcessor processor = new(
            navigator);

        BountyBoardProcessingResult result =
            await processor.ProcessAsync(
                Window(),
                null!,
                State(),
                parkedLimit: 0,
                BountyChallengeAvailability.DailyLimit,
                rerollEnabled: true,
                static (_, _) => Task.CompletedTask,
                log: null,
                CancellationToken.None);

        Assert.Equal(
            [1, 2, 3, 4, 5],
            navigator.RerollClicks);
        Assert.Equal(
            [2, 4, 5, 6],
            result.State.Active
                .Select(value => value.Number)
                .Order());
        Assert.False(result.GoldUnavailable);
        Assert.False(result.RerollLimitReached);
    }

    [Fact]
    public async Task ProcessAsync_StopsAfterTheSevenHundredFiftiethPaidSettlement()
    {
        FinalPaidRerollNavigator navigator = new();
        BountyBoardProcessor processor = new(
            navigator);
        List<BountyProgressState> persisted = [];

        BountyBoardProcessingResult result =
            await processor.ProcessAsync(
                Window(),
                null!,
                State() with
                {
                    RerollsToday =
                        BountyCatalog.DailyRerollLimit - 1,
                },
                parkedLimit: 0,
                BountyChallengeAvailability.DailyLimit,
                rerollEnabled: true,
                (state, _) =>
                {
                    persisted.Add(state);
                    return Task.CompletedTask;
                },
                log: null,
                CancellationToken.None);

        Assert.Equal(2, navigator.RerollClicks);
        Assert.Equal(1, navigator.ConfirmClicks);
        Assert.Equal(
            BountyCatalog.DailyRerollLimit,
            result.State.RerollsToday);
        Assert.True(result.RerollLimitReached);
        Assert.Empty(result.State.Active);
        Assert.Contains(
            persisted,
            state => state.RerollsToday ==
                BountyCatalog.DailyRerollLimit);
    }

    private static BountyProgressState State() =>
        new()
        {
            DailyEpochUtc = BountyProgressState
                .UtcDay(DateTimeOffset.UtcNow),
        };

    private static RobloxWindow Window() =>
        new((nint)1, "Roblox");

    private static BountyLiveSlot LiveSlot(
        int slot) =>
        new(
            slot,
            new BountyCardAction(
                BountyCardActionKind.Reroll,
                RightColumns[slot - 1],
                420));

    private static BountyBoardMatch Board(
        IEnumerable<BountyLiveSlot> liveSlots,
        params BountyNumberMatch[] numbers) =>
        new(
            BountyBoardState.Board,
            Confidence: 0.95,
            Actions: liveSlots
                .Select(value => value.Action)
                .ToArray(),
            Numbers: numbers,
            NoGold: false);

    private static BountyBoardMatch Confirmation() =>
        new(
            BountyBoardState.RerollConfirmation,
            Confidence: 0.95,
            Actions: [],
            Numbers: [],
            NoGold: false);

    private sealed class RoundRobinNavigator(
        IReadOnlyDictionary<int, int?> numbers) :
        ProcessorNavigatorStub
    {
        private readonly BountyLiveSlot[] _liveSlots =
            Enumerable.Range(1, 5)
                .Select(LiveSlot)
                .ToArray();
        private int _currentSlot;

        public List<int> RerollClicks { get; } = [];

        public override Task<(
            BountyBoardMatch Board,
            IReadOnlyList<BountyLiveSlot> Slots)>
            WaitForLiveSlotsAsync(
            RobloxWindow window,
            IDetectorPack detector,
            bool rightView,
            CancellationToken cancellationToken) =>
            Task.FromResult((
                Board(_liveSlots),
                (IReadOnlyList<BountyLiveSlot>)_liveSlots));

        public override Task<BountyBoardMatch>
            ClickRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken)
        {
            _currentSlot = slot;
            RerollClicks.Add(slot);
            return Task.FromResult(
                numbers[slot] is null
                    ? Board(_liveSlots)
                    : Confirmation());
        }

        public override Task<BountyBoardMatch>
            WaitForBoardAsync(
            RobloxWindow window,
            IDetectorPack detector,
            CancellationToken cancellationToken)
        {
            int number = numbers[_currentSlot] ??
                throw new InvalidOperationException();
            return Task.FromResult(
                Board(
                    _liveSlots,
                    new BountyNumberMatch(
                        number,
                        Confidence: 0.99,
                        CenterX:
                            RightColumns[_currentSlot - 1] - 7,
                        CenterY: 330)));
        }
    }

    private sealed class FinalPaidRerollNavigator :
        ProcessorNavigatorStub
    {
        private readonly BountyLiveSlot[] _liveSlots =
            [LiveSlot(1)];

        public int RerollClicks { get; private set; }

        public int ConfirmClicks { get; private set; }

        public override Task<(
            BountyBoardMatch Board,
            IReadOnlyList<BountyLiveSlot> Slots)>
            WaitForLiveSlotsAsync(
            RobloxWindow window,
            IDetectorPack detector,
            bool rightView,
            CancellationToken cancellationToken) =>
            Task.FromResult((
                Board(_liveSlots),
                (IReadOnlyList<BountyLiveSlot>)_liveSlots));

        public override Task<BountyBoardMatch>
            ClickRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken)
        {
            RerollClicks++;
            return Task.FromResult(Confirmation());
        }

        public override Task<BountyBoardMatch>
            WaitForBoardAsync(
            RobloxWindow window,
            IDetectorPack detector,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Board(
                    _liveSlots,
                    new BountyNumberMatch(
                        Number: 1,
                        Confidence: 0.99,
                        CenterX: RightColumns[0] - 7,
                        CenterY: 330)));

        public override Task<BountyBoardMatch>
            ConfirmRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken)
        {
            ConfirmClicks++;
            return Task.FromResult(
                Board(
                    _liveSlots,
                    new BountyNumberMatch(
                        Number: 2,
                        Confidence: 0.99,
                        CenterX: RightColumns[0] - 7,
                        CenterY: 330)));
        }
    }

    private abstract class ProcessorNavigatorStub :
        IBountyBoardProcessorNavigator
    {
        public virtual Task ScrollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            bool right,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public abstract Task<(
            BountyBoardMatch Board,
            IReadOnlyList<BountyLiveSlot> Slots)>
            WaitForLiveSlotsAsync(
            RobloxWindow window,
            IDetectorPack detector,
            bool rightView,
            CancellationToken cancellationToken);

        public virtual Task<BountyClaimResult?> ClaimAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken) =>
            Task.FromResult<BountyClaimResult?>(null);

        public abstract Task<BountyBoardMatch>
            ClickRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken);

        public virtual Task CancelRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public abstract Task<BountyBoardMatch>
            WaitForBoardAsync(
            RobloxWindow window,
            IDetectorPack detector,
            CancellationToken cancellationToken);

        public virtual Task<BountyBoardMatch>
            ConfirmRerollAsync(
            RobloxWindow window,
            IDetectorPack detector,
            int slot,
            bool rightView,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
