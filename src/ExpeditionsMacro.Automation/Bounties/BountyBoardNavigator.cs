using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Bounties;

namespace ExpeditionsMacro.Automation.Bounties;

internal sealed partial class BountyBoardNavigator :
    IBountyBoardProcessorNavigator
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan RerollSettleDelay =
        TimeSpan.FromMilliseconds(200);
    private readonly IRobloxAutomation _automation;
    private readonly Func<
        TimeSpan,
        CancellationToken,
        Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly BountyLobbyHandoffNavigator
        _lobbyHandoff;
    private readonly BountyBoardLiveActionObserver
        _liveActions;

    public BountyBoardNavigator(
        IRobloxAutomation automation)
        : this(
            automation,
            static (duration, token) =>
                Task.Delay(duration, token),
            static () => DateTimeOffset.UtcNow)
    {
    }

    internal BountyBoardNavigator(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset> utcNow)
    {
        _automation = automation;
        _delay = delay;
        _utcNow = utcNow;
        _lobbyHandoff =
            new BountyLobbyHandoffNavigator(
                automation,
                delay,
                utcNow);
        _liveActions =
            new BountyBoardLiveActionObserver(
                Capture,
                delay,
                utcNow);
    }

    public async Task OpenAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        await _lobbyHandoff.EnsureAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
        (int X, int Y) events =
            BountyBoardDetector.LobbyEventAction;
        await ClickAsync(
            window,
            events.X,
            events.Y,
            cancellationToken).ConfigureAwait(false);
        BountyBoardMatch destination =
            await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.EventCatalog,
            TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);
        if (destination.State ==
            BountyBoardState.Board)
        {
            return;
        }
        (int X, int Y) board =
            destination.EventAction ??
            throw new RobloxUiUnavailableException(
                "The live Bounty Board event action was unavailable.");
        await ClickAsync(
            window,
            board.X,
            board.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.Board,
            TimeSpan.FromSeconds(15),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ReturnToLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.Board,
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);
        (int X, int Y) back =
            BountyBoardDetector.BoardBackAction;
        await ClickAsync(
            window,
            back.X,
            back.Y,
            cancellationToken).ConfigureAwait(false);

        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(20),
            minimumObservations: 3,
            _utcNow);
        StableStateTracker<string> tracker =
            new(required: 3);
        while (budget.ShouldObserve(
                   tracker.HasPendingCandidate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame frame =
                Capture(window, detector);
            string state =
                detector.RecoveryState(frame) ?? "";
            budget.MarkObserved();
            if (tracker.Update(
                    string.Equals(
                        state,
                        "lobby",
                        StringComparison.OrdinalIgnoreCase)
                        ? "lobby"
                        : "") == "lobby")
            {
                return;
            }
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            "Bounty Board Back did not return to a stable Lobby.");
    }

    public async Task<BountyBoardMatch> WaitForBoardAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken) =>
        await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.Board,
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);

    public async Task<(
        BountyBoardMatch Board,
        IReadOnlyList<BountyLiveSlot> Slots)>
        WaitForLiveSlotsAsync(
        RobloxWindow window,
        IDetectorPack detector,
        bool rightView,
        CancellationToken cancellationToken) =>
        await _liveActions.WaitForLiveSlotsAsync(
                window,
                detector,
                rightView,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task ScrollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        bool right,
        CancellationToken cancellationToken)
    {
        await WaitForBoardAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
        Focus(window);
        await _automation.ScrollClientAsync(
            window,
            right ? -20 : 20,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            TimeSpan.FromMilliseconds(350),
            cancellationToken).ConfigureAwait(false);
        await WaitForBoardAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BountyBoardMatch> ClickRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken)
    {
        (_, IReadOnlyList<BountyLiveSlot> slots) =
            await WaitForLiveSlotsAsync(
                    window,
                    detector,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        BountyCardAction action = slots
            .Where(value =>
                value.Slot == slot &&
                value.Action.Kind ==
                    BountyCardActionKind.Reroll)
            .Select(value => value.Action)
            .Cast<BountyCardAction?>()
            .FirstOrDefault() ??
            throw new RobloxUiUnavailableException(
                $"Bounty slot {slot} has no stable live Reroll action.");
        await ClickAsync(
            window,
            action.X,
            action.Y,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            RerollSettleDelay,
            cancellationToken).ConfigureAwait(false);

        ObservationWaitBudget budget = new(
            TimeSpan.FromSeconds(4),
            minimumObservations: 2,
            _utcNow);
        BountyBoardMatch last = default;
        int stable = 0;
        BountyBoardState candidate =
            BountyBoardState.None;
        while (budget.ShouldObserve(stable > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = BountyBoardDetector.Detect(
                Capture(window, detector));
            budget.MarkObserved();
            if (last.NoGold)
            {
                return last;
            }
            BountyBoardState observed =
                last.State is
                    BountyBoardState.Board or
                    BountyBoardState.RerollConfirmation
                    ? last.State
                    : BountyBoardState.None;
            if (observed == candidate &&
                observed != BountyBoardState.None)
            {
                stable++;
            }
            else
            {
                candidate = observed;
                stable = observed ==
                    BountyBoardState.None
                    ? 0
                    : 1;
            }
            if (stable >= 2)
            {
                return last;
            }
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            $"Bounty slot {slot} did not settle after reroll.");
    }

    public async Task CancelRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        CancellationToken cancellationToken)
    {
        await WaitForStateAsync(
            window,
            detector,
            BountyBoardState.RerollConfirmation,
            TimeSpan.FromSeconds(6),
            cancellationToken).ConfigureAwait(false);
        (int X, int Y) cancel =
            BountyBoardDetector.RerollCancelAction;
        await ClickAsync(
            window,
            cancel.X,
            cancel.Y,
            cancellationToken).ConfigureAwait(false);
        await WaitForBoardAsync(
            window,
            detector,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<BountyBoardMatch> ConfirmRerollAsync(
        RobloxWindow window,
        IDetectorPack detector,
        int slot,
        bool rightView,
        CancellationToken cancellationToken)
    {
        BountyBoardMatch confirmation =
            await WaitForStateAsync(
                window,
                detector,
                BountyBoardState.RerollConfirmation,
                TimeSpan.FromSeconds(6),
                cancellationToken).ConfigureAwait(false);
        ImageFrame frame =
            Capture(window, detector);
        (int X, int Y)? action =
            BountyBoardDetector
                .RerollConfirmAction(frame);
        if (action is null ||
            confirmation.NoGold)
        {
            throw new RobloxUiUnavailableException(
                "The live Bounty reroll confirmation action was unavailable.");
        }
        await ClickAsync(
            window,
            action.Value.X,
            action.Value.Y,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            TimeSpan.FromMilliseconds(200),
            cancellationToken).ConfigureAwait(false);
        BountyBoardMatch settled =
            await WaitForBoardAsync(
                window,
                detector,
                cancellationToken).ConfigureAwait(false);
        if (settled.NoGold)
        {
            throw new BountyGoldUnavailableException();
        }
        (BountyBoardMatch stableBoard,
            IReadOnlyList<BountyLiveSlot> stableSlots) =
            await WaitForLiveSlotsAsync(
                    window,
                    detector,
                    rightView,
                    cancellationToken)
                .ConfigureAwait(false);
        if (stableBoard.NoGold)
        {
            throw new BountyGoldUnavailableException();
        }
        if (!stableSlots.Any(value =>
                value.Slot == slot &&
                value.Action.Kind ==
                    BountyCardActionKind.Reroll))
        {
            throw new RobloxUiUnavailableException(
                $"Confirmed Bounty slot {slot} did not settle to a stable live Reroll action.");
        }
        return stableBoard;
    }

    private async Task<BountyBoardMatch>
        WaitForStateAsync(
        RobloxWindow window,
        IDetectorPack detector,
        BountyBoardState desired,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            _utcNow);
        int stable = 0;
        string? candidate = null;
        BountyBoardMatch last = default;
        while (budget.ShouldObserve(stable > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = BountyBoardDetector.Detect(
                Capture(window, detector));
            budget.MarkObserved();
            string? observation =
                last.State == desired
                    ? desired ==
                        BountyBoardState.EventCatalog
                        ? last.EventAction?.ToString()
                        : desired.ToString()
                    : desired ==
                            BountyBoardState.EventCatalog &&
                        last.State ==
                            BountyBoardState.Board
                        ? last.State.ToString()
                        : null;
            stable = observation is not null &&
                string.Equals(
                    observation,
                    candidate,
                    StringComparison.Ordinal)
                ? stable + 1
                : observation is null ? 0 : 1;
            candidate = observation;
            if (stable >= 2)
            {
                return last;
            }
            await _delay(
                PollInterval,
                cancellationToken).ConfigureAwait(false);
        }
        throw new RobloxUiUnavailableException(
            $"Timed out waiting for Bounty state {desired}. Last state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task ClickAsync(
        RobloxWindow window,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        Focus(window);
        await _automation.ClickClientAsync(
            window,
            x,
            y,
            cancellationToken).ConfigureAwait(false);
    }

    private ImageFrame Capture(
        RobloxWindow window,
        IDetectorPack detector)
    {
        Focus(window);
        ClientBounds bounds =
            _automation.GetClientBounds(window);
        if (bounds.Width !=
                detector.Manifest.ClientWidth ||
            bounds.Height !=
                detector.Manifest.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                "Roblox no longer matches the detector client size.");
        }
        return _automation.CaptureClient(window);
    }

    private void Focus(
        RobloxWindow window)
    {
        if (!_automation.Focus(window))
        {
            throw new RobloxSessionUnavailableException(
                "Windows could not focus Roblox.");
        }
    }
}
