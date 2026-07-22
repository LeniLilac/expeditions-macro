using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Vision.Teams;

namespace ExpeditionsMacro.Automation.Teams;

public sealed class TeamSelectionService
{
    private readonly IRobloxAutomation _automation;

    public TeamSelectionService(IRobloxAutomation automation) => _automation = automation;

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
        EnsureFocus(window);
        await _automation.TapLetterKeyAsync(window, char.ToUpperInvariant(unitMenuKey), cancellationToken).ConfigureAwait(false);
        TeamScreenMatch opened = await WaitForAsync(
            window,
            state => state is TeamScreenState.Units or TeamScreenState.Teams,
            TimeSpan.FromSeconds(6),
            cancellationToken).ConfigureAwait(false);
        if (opened.State == TeamScreenState.Units)
        {
            (int x, int y) = TeamScreenDetector.TeamsTabAction;
            EnsureFocus(window);
            await _automation.ClickClientAsync(window, x, y, cancellationToken).ConfigureAwait(false);
            await WaitForAsync(window, state => state == TeamScreenState.Teams, TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new Core.Runtime.MacroProgress("Team", 6, $"Loading Team {teamSlot}."));
        EnsureFocus(window);
        await _automation.ScrollClientAsync(window, TeamScreenDetector.ScrollNotchesForTeam(teamSlot), cancellationToken).ConfigureAwait(false);
        await Task.Delay(350, cancellationToken).ConfigureAwait(false);
        (int loadX, int loadY) = TeamScreenDetector.LoadTeamAction(teamSlot);
        EnsureFocus(window);
        await _automation.ClickClientAsync(window, loadX, loadY, cancellationToken).ConfigureAwait(false);
        await WaitForAsync(window, state => state == TeamScreenState.LoadConfirm, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        (int confirmX, int confirmY) = TeamScreenDetector.LoadConfirmAction;
        EnsureFocus(window);
        await _automation.ClickClientAsync(window, confirmX, confirmY, cancellationToken).ConfigureAwait(false);

        TeamScreenMatch afterLoad = await WaitForAsync(
            window,
            state => state is TeamScreenState.EquipmentConfirm or TeamScreenState.Teams,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
        if (afterLoad.State == TeamScreenState.EquipmentConfirm)
        {
            (int includeX, int includeY) = TeamScreenDetector.IncludeEquipmentAction;
            EnsureFocus(window);
            await _automation.ClickClientAsync(window, includeX, includeY, cancellationToken).ConfigureAwait(false);
            await WaitForAsync(window, state => state == TeamScreenState.Teams, TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
        }

        await CloseUnitInterfaceAsync(window, unitMenuKey, cancellationToken).ConfigureAwait(false);
        progress?.Report(new Core.Runtime.MacroProgress("Team", 8, $"Team {teamSlot} loaded."));
    }

    private async Task<TeamScreenMatch> WaitForAsync(
        RobloxWindow window,
        Func<TeamScreenState, bool> expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        TeamScreenMatch last = new(TeamScreenState.None, 0);
        TeamScreenState? candidate = null;
        int consecutive = 0;
        while (DateTimeOffset.UtcNow < deadline)
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
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
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

        throw new InvalidOperationException($"The Unit Team window did not close after loading the selected team. Last state: {last.State}.");
    }

    private void EnsureFocus(RobloxWindow window)
    {
        if (!_automation.Focus(window)) throw new InvalidOperationException("Windows could not focus Roblox while changing teams.");
    }

    private ImageFrame CaptureClient(RobloxWindow window)
    {
        EnsureFocus(window);
        var bounds = _automation.GetClientBounds(window);
        if (bounds.Width != TeamScreenDetector.ClientWidth || bounds.Height != TeamScreenDetector.ClientHeight)
        {
            throw new InvalidOperationException(
                $"Roblox no longer matches the required {TeamScreenDetector.ClientWidth} by {TeamScreenDetector.ClientHeight} client size while changing teams.");
        }

        return _automation.CaptureClient(window);
    }
}
