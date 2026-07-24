using ExpeditionsMacro.Automation.Navigation;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
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
    private readonly ResourceRefuelScreenWaiter _screens;

    internal ResourceRefuelNavigator(
        IRobloxAutomation automation,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _automation = automation;
        _delay = delay;
        _screens = new ResourceRefuelScreenWaiter(
            automation,
            delay);
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
    }

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
            (timeout, token) =>
                _screens.WaitForPlayAsync(
                    window,
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
        await _delay(
            InteractionSettle,
            cancellationToken).ConfigureAwait(false);

        (int confirmX, int confirmY) =
            ResourceStationScreenDetector.ConfirmFuelAction();
        await _screens.ClickAsync(
            window,
            confirmX,
            confirmY,
            cancellationToken).ConfigureAwait(false);
        await _screens.WaitForStationAsync(
            window,
            state => state == expected,
            TimeSpan.FromSeconds(5),
            cancellationToken).ConfigureAwait(false);
    }
}
