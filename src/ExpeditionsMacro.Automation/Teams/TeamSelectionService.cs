using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Automation.Teams;

public sealed class TeamSelectionService
{
    private const int LoadClickAttempts = 2;
    private const int StableLayoutDetections = 2;
    private const int TopAlignmentDragAttempts = 3;
    private const int TeamAlignmentDragAttempts = 5;
    private static readonly TimeSpan TopAlignmentTimeout =
        TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TeamAlignmentTimeout =
        TimeSpan.FromSeconds(15);
    private readonly IRobloxAutomation _automation;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public TeamSelectionService(IRobloxAutomation automation)
        : this(
            automation,
            static () => DateTimeOffset.UtcNow,
            static (duration, token) =>
                Task.Delay(duration, token))
    {
    }

    internal TeamSelectionService(
        IRobloxAutomation automation,
        Func<DateTimeOffset> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _utcNow = utcNow;
        _delay = delay;
    }

    public async Task SelectAsync(
        RobloxWindow window,
        int teamSlot,
        char unitMenuKey,
        IProgress<Core.Runtime.MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (teamSlot == 0) return;
        if (teamSlot is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(teamSlot));
        if (!char.IsAsciiLetter(unitMenuKey)) throw new ArgumentOutOfRangeException(nameof(unitMenuKey));
        EnsureFocus(window);

        progress?.Report(new Core.Runtime.MacroProgress("Team", 4, $"Opening Units to load Team {teamSlot}."));
        await new TeamListOpeningTransaction(
            _automation,
            _utcNow,
            _delay).OpenAsync(
                window,
                unitMenuKey,
                cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new Core.Runtime.MacroProgress("Team", 6, $"Loading Team {teamSlot}."));
        TeamScreenMatch loadConfirm = await OpenLoadConfirmationAsync(
            window,
            teamSlot,
            cancellationToken).ConfigureAwait(false);
        (int confirmX, int confirmY) = ResolveAction(loadConfirm, TeamScreenDetector.LoadConfirmAction);
        EnsureFocus(window);
        await _automation.ClickClientAsync(window, confirmX, confirmY, cancellationToken).ConfigureAwait(false);

        TeamScreenMatch afterLoad = await WaitForAsync(
            window,
            state => state is TeamScreenState.EquipmentConfirm or TeamScreenState.Teams,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        if (afterLoad.State == TeamScreenState.EquipmentConfirm)
        {
            (int includeX, int includeY) = ResolveAction(afterLoad, TeamScreenDetector.IncludeEquipmentAction);
            EnsureFocus(window);
            await _automation.ClickClientAsync(window, includeX, includeY, cancellationToken).ConfigureAwait(false);
            await WaitForAsync(window, state => state == TeamScreenState.Teams, TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
        }

        await CloseUnitInterfaceAsync(window, unitMenuKey, cancellationToken).ConfigureAwait(false);
        progress?.Report(new Core.Runtime.MacroProgress("Team", 8, $"Team {teamSlot} loaded."));
    }

    private async Task<TeamScreenMatch> OpenLoadConfirmationAsync(
        RobloxWindow window,
        int teamSlot,
        CancellationToken cancellationToken)
    {
        TeamScrollbarThumb topThumb =
            await NormalizeTeamListTopAsync(
                window,
                cancellationToken)
                .ConfigureAwait(false);

        TimeoutException? lastTimeout = null;
        for (int attempt = 0; attempt < LoadClickAttempts; attempt++)
        {
            (int loadX, int loadY) = await AlignLoadTeamActionAsync(
                window,
                teamSlot,
                topThumb.CenterY,
                cancellationToken).ConfigureAwait(false);
            EnsureFocus(window);
            await _automation.ClickClientAsync(window, loadX, loadY, cancellationToken).ConfigureAwait(false);
            try
            {
                return await WaitForAsync(
                    window,
                    state => state == TeamScreenState.LoadConfirm,
                    TimeSpan.FromSeconds(5),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException error)
            {
                lastTimeout = error;
                if (attempt + 1 >= LoadClickAttempts ||
                    TeamScreenDetector.Detect(CaptureClient(window)).State != TeamScreenState.Teams)
                {
                    throw;
                }
            }
        }

        throw lastTimeout ?? new TimeoutException($"Team {teamSlot} did not open its Load Team confirmation.");
    }

    private async Task<TeamScrollbarThumb>
        NormalizeTeamListTopAsync(
            RobloxWindow window,
            CancellationToken cancellationToken)
    {
        TeamScrollbarThumb thumb =
            await WaitForStableTeamListAsync(
                window,
                TimeSpan.FromSeconds(4),
                cancellationToken).ConfigureAwait(false);
        ObservationWaitBudget budget = new(
            TopAlignmentTimeout,
            TopAlignmentDragAttempts,
            _utcNow);
        int dragAttempts = 0;
        while (!TeamScreenDetector.IsScrollbarAtTop(thumb) &&
               dragAttempts < TopAlignmentDragAttempts &&
               budget.ShouldObserve())
        {
            EnsureFocus(window);
            await _automation.DragClientAsync(
                window,
                thumb.X,
                thumb.CenterY,
                thumb.X,
                TeamScreenDetector
                    .TopScrollbarDragLimitY,
                cancellationToken).ConfigureAwait(false);
            dragAttempts++;
            thumb = await WaitForStableTeamListAsync(
                window,
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);
            budget.MarkObserved();
        }

        if (!TeamScreenDetector.IsScrollbarAtTop(thumb))
        {
            throw new RobloxUiUnavailableException(
                $"The Unit Team scrollbar could not be normalized to its stable top position after {dragAttempts} bounded drag attempts. Last center: {thumb.CenterY}.");
        }
        return thumb;
    }

    private async Task<TeamScrollbarThumb> WaitForStableTeamListAsync(
        RobloxWindow window,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        TeamScrollbarThumb? candidate = null;
        TeamScrollbarThumb? last = null;
        int consecutive = 0;
        ObservationWaitBudget budget = new(
            timeout,
            StableLayoutDetections,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending:
                       consecutive > 0 &&
                       consecutive <
                           StableLayoutDetections))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame image = CaptureClient(window);
            TeamScreenMatch state = TeamScreenDetector.Detect(image);
            TeamScrollbarThumb? thumb = state.State == TeamScreenState.Teams
                ? TeamScreenDetector.FindScrollbarThumb(image)
                : null;
            last = thumb ?? last;
            if (thumb is not null &&
                candidate is TeamScrollbarThumb prior &&
                SameThumbGeometry(prior, thumb.Value))
            {
                consecutive++;
            }
            else if (thumb is not null)
            {
                candidate = thumb;
                consecutive = 1;
            }
            else
            {
                candidate = null;
                consecutive = 0;
            }

            if (consecutive >= StableLayoutDetections)
            {
                return thumb!.Value;
            }
            budget.MarkObserved();
            await _delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken).ConfigureAwait(false);
        }

        throw new RobloxUiUnavailableException(
            last is null
                ? "The Unit Team scrollbar could not be located after the interface finished opening."
                : $"The Unit Team scrollbar did not settle before team selection. Last center: {last.Value.CenterY}.");
    }

    private async Task<(int X, int Y)> AlignLoadTeamActionAsync(
        RobloxWindow window,
        int teamSlot,
        int topThumbCenterY,
        CancellationToken cancellationToken)
    {
        ObservationWaitBudget budget = new(
            TeamAlignmentTimeout,
            TeamAlignmentDragAttempts,
            _utcNow);
        int dragAttempts = 0;
        bool postDragObservationPending = false;
        while (budget.ShouldObserve(
                   confirmationPending:
                       postDragObservationPending))
        {
            postDragObservationPending = false;
            TeamScrollbarThumb thumb =
                await WaitForStableTeamListAsync(
                    window,
                    TimeSpan.FromSeconds(3),
                    cancellationToken).ConfigureAwait(false);
            ImageFrame image = CaptureClient(window);
            TeamScreenMatch state = TeamScreenDetector.Detect(image);
            if (state.State != TeamScreenState.Teams)
            {
                throw new RobloxUiUnavailableException(
                    $"The Unit Team list was lost while aligning Team {teamSlot}. Last state: {state.State} ({state.Confidence:P0}).");
            }

            (int X, int Y)? action = TeamScreenDetector.VisibleLoadTeamAction(
                image,
                teamSlot,
                topThumbCenterY);
            if (action is not null)
            {
                action = await WaitForVisibleLoadActionAsync(
                    window,
                    teamSlot,
                    topThumbCenterY,
                    TimeSpan.FromSeconds(3),
                    cancellationToken).ConfigureAwait(false);
                if (action is not null) return action.Value;
                budget.MarkObserved();
                continue;
            }

            if (dragAttempts >= TeamAlignmentDragAttempts)
            {
                break;
            }

            int targetCenterY =
                TeamScreenDetector.ScrollThumbTargetCenterY(
                    teamSlot,
                    topThumbCenterY);
            int dragEndY = teamSlot switch
            {
                1 => TeamScreenDetector.TopScrollbarDragLimitY,
                >= 7 => TeamScreenDetector.BottomScrollbarDragLimitY,
                _ => targetCenterY,
            };
            EnsureFocus(window);
            await _automation.DragClientAsync(
                window,
                thumb.X,
                thumb.CenterY,
                thumb.X,
                dragEndY,
                cancellationToken).ConfigureAwait(false);
            dragAttempts++;
            postDragObservationPending = true;
            budget.MarkObserved();
        }

        throw new RobloxUiUnavailableException(
            $"Team {teamSlot} could not be aligned to a fully visible Load Team button after {dragAttempts} bounded drag attempts.");
    }

    private async Task<(int X, int Y)?> WaitForVisibleLoadActionAsync(
        RobloxWindow window,
        int teamSlot,
        int topThumbCenterY,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        (int X, int Y)? candidate = null;
        int consecutive = 0;
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending: consecutive == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImageFrame image = CaptureClient(window);
            TeamScreenMatch state = TeamScreenDetector.Detect(image);
            (int X, int Y)? action = state.State == TeamScreenState.Teams
                ? TeamScreenDetector.VisibleLoadTeamAction(
                    image,
                    teamSlot,
                    topThumbCenterY)
                : null;
            if (action is not null)
            {
                if (candidate == action) consecutive++;
                else
                {
                    candidate = action;
                    consecutive = 1;
                }
                if (consecutive >= 2) return action;
            }
            else
            {
                candidate = null;
                consecutive = 0;
            }
            budget.MarkObserved();
            await _delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<TeamScreenMatch> WaitForAsync(
        RobloxWindow window,
        Func<TeamScreenState, bool> expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        TeamScreenMatch last = new(TeamScreenState.None, 0);
        TeamScreenState? candidate = null;
        int consecutive = 0;
        ObservationWaitBudget budget = new(
            timeout,
            minimumObservations: 2,
            _utcNow);
        while (budget.ShouldObserve(
                   confirmationPending: consecutive == 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = TeamScreenDetector.Detect(CaptureClient(window));
            if (expected(last.State))
            {
                if (candidate == last.State) consecutive++;
                else
                {
                    candidate = last.State;
                    consecutive = 1;
                }
                if (consecutive >= 2) return last;
            }
            else
            {
                candidate = null;
                consecutive = 0;
            }
            budget.MarkObserved();
            await _delay(
                TimeSpan.FromMilliseconds(150),
                cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException($"Timed out waiting for the Unit Team interface. Last state: {last.State} ({last.Confidence:P0}).");
    }

    private async Task CloseUnitInterfaceAsync(RobloxWindow window, char unitMenuKey, CancellationToken cancellationToken)
    {
        EnsureFocus(window);
        await _automation.ParkCursorAsync(window, cancellationToken).ConfigureAwait(false);
        TeamScreenMatch last = new(TeamScreenState.None, 0);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            EnsureFocus(window);
            await _automation.TapLetterKeyAsync(window, char.ToUpperInvariant(unitMenuKey), cancellationToken).ConfigureAwait(false);
            last = await WaitForAsync(
                window,
                state => state is TeamScreenState.None or TeamScreenState.Units or TeamScreenState.Teams,
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);
            if (last.State == TeamScreenState.None) return;
        }

        throw new RobloxUiUnavailableException($"The Unit Team window did not close after loading the selected team. Last state: {last.State}.");
    }

    private void EnsureFocus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new RobloxSessionUnavailableException("Windows could not focus Roblox while changing teams.");
    }

    private static (int X, int Y) ResolveAction(TeamScreenMatch match, (int X, int Y) fallback) =>
        match.ActionX is int x && match.ActionY is int y ? (x, y) : fallback;

    private static bool SameThumbGeometry(
        TeamScrollbarThumb left,
        TeamScrollbarThumb right) =>
        Math.Abs(left.X - right.X) <= 2 &&
        Math.Abs(left.CenterY - right.CenterY) <= 2 &&
        Math.Abs(left.Height - right.Height) <= 3;

    private ImageFrame CaptureClient(RobloxWindow window)
    {
        EnsureFocus(window);
        var bounds = _automation.GetClientBounds(window);
        if (bounds.Width != TeamScreenDetector.ClientWidth || bounds.Height != TeamScreenDetector.ClientHeight)
        {
            throw new RobloxSessionUnavailableException(
                $"Roblox no longer matches the required {TeamScreenDetector.ClientWidth} by {TeamScreenDetector.ClientHeight} client size while changing teams.");
        }

        return _automation.CaptureClient(window);
    }
}
