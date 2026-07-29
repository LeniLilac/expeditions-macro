using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;
using ExpeditionsMacro.Vision.Packs;
using ExpeditionsMacro.Vision.Refuel;

namespace ExpeditionsMacro.Automation.Refuel;

internal sealed class ResourceRefuelNavigator
{
    private static readonly TimeSpan HubTeleportDelay =
        TimeSpan.FromMilliseconds(5500);
    private static readonly TimeSpan RouteStepDelay =
        TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan InteractionSettle =
        TimeSpan.FromMilliseconds(250);

    private readonly IRobloxAutomation _automation;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ResourceRefuelScreenWaiter _screens;

    internal ResourceRefuelNavigator(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset> utcNow)
    {
        _automation = automation;
        _delay = delay;
        _utcNow = utcNow;
        _screens = new ResourceRefuelScreenWaiter(
            automation,
            delay,
            utcNow);
    }

    internal async Task PrepareLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await _screens.EnsureCanonicalClientAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        await _screens.WaitForLobbyAsync(
            window,
            detector,
            timeout,
            cancellationToken).ConfigureAwait(false);
        await EnsureChatClosedAsync(
            window,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task PrepareScheduledLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        await _screens.EnsureCanonicalClientAsync(
            window,
            cancellationToken).ConfigureAwait(false);
        if (await _screens.TryWaitForLobbyAsync(
                window,
                detector,
                TimeSpan.FromSeconds(2),
                cancellationToken).ConfigureAwait(false))
        {
            await EnsureChatClosedAsync(
                window,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        ChallengeScreenMatch surface =
            await _screens.WaitForPlaySurfaceAsync(
                window,
                state => state is
                    ChallengeScreenState.ChallengeList or
                    ChallengeScreenState
                        .ChallengeListUnavailable or
                    ChallengeScreenState.GameModeSelector or
                    ChallengeScreenState.PostMatchPreview,
                TimeSpan.FromSeconds(6),
                cancellationToken).ConfigureAwait(false);
        if (surface.State is
            ChallengeScreenState.ChallengeList or
            ChallengeScreenState.ChallengeListUnavailable)
        {
            StableScreenAction<ChallengeScreenMatch>?
                close =
                    await StableScreenActionWaiter.WaitAsync(
                            surface.State,
                            stableDetections: 2,
                            () => ChallengeScreenDetector
                                .Detect(
                                    _screens.Capture(window)),
                            static match => match.State,
                            static match =>
                                match.ActionX is int x &&
                                match.ActionY is int y
                                    ? (x, y)
                                    : null,
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromMilliseconds(200),
                            cancellationToken,
                            _utcNow,
                            _delay)
                        .ConfigureAwait(false);
            if (close is null)
            {
                throw new RobloxUiUnavailableException(
                    "The Challenge selector did not expose a stable close action before resource refuel.");
            }
            await _screens.ClickAsync(
                window,
                close.Value.X,
                close.Value.Y,
                cancellationToken).ConfigureAwait(false);
            await _screens.WaitForPlaySurfaceAsync(
                window,
                state => state ==
                    ChallengeScreenState.GameModeSelector,
                TimeSpan.FromSeconds(6),
                cancellationToken).ConfigureAwait(false);
        }

        await ClosePlayToLobbyAsync(
            window,
            detector,
            playMenuKey,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task ClosePlayToLobbyAsync(
        RobloxWindow window,
        IDetectorPack detector,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        _screens.RequireFocus(window);
        await _automation.TapLetterKeyAsync(
            window,
            playMenuKey,
            cancellationToken).ConfigureAwait(false);
        await _screens.WaitForLobbyAsync(
            window,
            detector,
            TimeSpan.FromSeconds(8),
            cancellationToken).ConfigureAwait(false);
        await EnsureChatClosedAsync(
            window,
            cancellationToken).ConfigureAwait(false);
    }

    private Task<bool> EnsureChatClosedAsync(
        RobloxWindow window,
        CancellationToken cancellationToken) =>
        new RobloxChatPanelNormalizer(_automation)
            .EnsureClosedAsync(
                window,
                cancellationToken);

    internal async Task RunStationWithRetriesAsync(
        RobloxWindow window,
        ResourceRefuelTarget target,
        ResourceRefuelRequest request,
        Action<string, string, MacroEventLevel> report,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        int maximumAttempts =
            request.Settings.RetryCount + 1;
        for (int attempt = 1;
             attempt <= maximumAttempts;
             attempt++)
        {
            try
            {
                report(
                    $"Refueling " +
                    $"{ResourceRefuelService.Label(target)} " +
                    $"(attempt {attempt}/{maximumAttempts}).",
                    "resource_refuel_station",
                    MacroEventLevel.Information);
                await TeleportToHubAsync(
                    window,
                    request.AreasMenuKey,
                    cancellationToken).ConfigureAwait(false);
                await WalkRouteAsync(
                    window,
                    request.Settings.RouteFor(target),
                    target,
                    report,
                    cancellationToken).ConfigureAwait(false);
                await OpenAndRefuelStationAsync(
                    window,
                    target,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                lastError = error;
                report(
                    $"{ResourceRefuelService.Label(target)} " +
                    $"attempt {attempt} failed: {error.Message}",
                    "resource_refuel_retry",
                    MacroEventLevel.Warning);
                if (attempt < maximumAttempts &&
                    !await RestoreRetryRootAsync(
                            window,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new RobloxUiUnavailableException(
                        "The resource station remained open, so the macro stopped before retrying its blind route.",
                        error);
                }
            }
        }

        throw new TimeoutException(
            $"{ResourceRefuelService.Label(target)} did not " +
            $"open and refuel after {maximumAttempts} attempt(s).",
            lastError);
    }

    internal async Task OpenPlayAsync(
        RobloxWindow window,
        char playMenuKey,
        CancellationToken cancellationToken)
    {
        await PlayMenuNavigator.OpenWithRetriesAsync(
            playMenuKey,
            () => _screens.Capture(window),
            (key, token) =>
                _automation.TapLetterKeyAsync(
                    window,
                    key,
                    token),
            (initialFrame, timeout, token) =>
                _screens.WaitForPlayAsync(
                    window,
                    initialFrame,
                    timeout,
                    token),
            attemptStarted: null,
            attemptMissed: null,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task TeleportToHubAsync(
        RobloxWindow window,
        char areasMenuKey,
        CancellationToken cancellationToken)
    {
        AreasScreenMatch areas =
            AreasScreenDetector.Detect(
                _screens.Capture(window));
        if (areas.State == AreasScreenState.None)
        {
            await _automation.TapLetterKeyAsync(
                window,
                areasMenuKey,
                cancellationToken).ConfigureAwait(false);
            areas = await _screens.WaitForAreasAsync(
                window,
                state => state != AreasScreenState.None,
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
        }

        if (areas.State != AreasScreenState.Expeditions)
        {
            await _screens.ClickAsync(
                window,
                areas,
                cancellationToken).ConfigureAwait(false);
            areas = await _screens.WaitForAreasAsync(
                window,
                state =>
                    state == AreasScreenState.Expeditions,
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
        }

        await _screens.ClickAsync(
            window,
            areas,
            cancellationToken).ConfigureAwait(false);
        await _delay(
            HubTeleportDelay,
            cancellationToken).ConfigureAwait(false);
        _screens.RequireFocus(window);
    }

    private async Task WalkRouteAsync(
        RobloxWindow window,
        IReadOnlyList<(
            char Key,
            int HoldMilliseconds)> route,
        ResourceRefuelTarget target,
        Action<string, string, MacroEventLevel> report,
        CancellationToken cancellationToken)
    {
        report(
            "Following the configured blind route to " +
            $"{ResourceRefuelService.Label(target)}.",
            "resource_refuel_route",
            MacroEventLevel.Information);
        foreach ((char key, int holdMilliseconds) in route)
        {
            _screens.RequireFocus(window);
            await _automation.HoldLetterKeyAsync(
                window,
                key,
                holdMilliseconds,
                cancellationToken).ConfigureAwait(false);
            await _delay(
                RouteStepDelay,
                cancellationToken).ConfigureAwait(false);
        }

        _screens.RequireFocus(window);
        await _automation.TapLetterKeyAsync(
            window,
            'E',
            cancellationToken).ConfigureAwait(false);
    }

    private async Task OpenAndRefuelStationAsync(
        RobloxWindow window,
        ResourceRefuelTarget target,
        CancellationToken cancellationToken)
    {
        ResourceStationScreenState expected = target switch
        {
            ResourceRefuelTarget.GoldMine =>
                ResourceStationScreenState.GoldMine,
            ResourceRefuelTarget.ResourceDrill =>
                ResourceStationScreenState.ResourceDrill,
            _ => throw new ArgumentOutOfRangeException(
                nameof(target)),
        };
        ResourceStationScreenMatch station =
            await _screens.WaitForStationAsync(
                window,
                state => state == expected,
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
        await _screens.ClickAsync(
            window,
            station,
            cancellationToken).ConfigureAwait(false);

        ResourceStationScreenMatch dialog =
            await _screens.WaitForStationAsync(
                window,
                state =>
                    state ==
                    ResourceStationScreenState.AddFuelDialog,
                TimeSpan.FromSeconds(4),
                cancellationToken).ConfigureAwait(false);
        await _screens.ClickAsync(
            window,
            dialog,
            cancellationToken).ConfigureAwait(false);
        ResourceStationScreenMatch confirm =
            await _screens.WaitForStationAsync(
                window,
                state =>
                    state ==
                    ResourceStationScreenState.AddFuelDialog,
                TimeSpan.FromSeconds(4),
                cancellationToken).ConfigureAwait(false);
        await _screens.ClickConfirmAsync(
            window,
            confirm,
            cancellationToken).ConfigureAwait(false);
        await _screens.WaitForStationAsync(
            window,
            state => state == expected,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> RestoreRetryRootAsync(
        RobloxWindow window,
        CancellationToken cancellationToken)
    {
        for (int action = 0; action < 2; action++)
        {
            ResourceStationScreenMatch current =
                ResourceStationScreenDetector.Detect(
                    _screens.Capture(window));
            if (current.State ==
                ResourceStationScreenState.None)
            {
                return true;
            }

            ResourceStationScreenMatch? stable =
                await _screens.TryWaitForStationAsync(
                    window,
                    current.State,
                    TimeSpan.FromSeconds(2),
                    cancellationToken).ConfigureAwait(false);
            if (stable is null)
            {
                return false;
            }
            await _screens.ClickDismissAsync(
                window,
                stable,
                cancellationToken).ConfigureAwait(false);
            await _delay(
                InteractionSettle,
                cancellationToken).ConfigureAwait(false);
        }

        return ResourceStationScreenDetector
            .Detect(_screens.Capture(window))
            .State == ResourceStationScreenState.None;
    }
}
