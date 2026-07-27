using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Placement;

public sealed class PlacementRoutePositioningService
{
    private readonly IRobloxAutomation _automation;

    public PlacementRoutePositioningService(
        IRobloxAutomation automation)
    {
        _automation = automation;
    }

    public static bool IsAvailable(
        PlacementTarget target) =>
        StepsFor(target).Count > 0;

    public async Task PositionAsync(
        RobloxWindow window,
        PlacementTarget target,
        IProgress<MacroProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(char Key, int Milliseconds)> steps =
            StepsFor(target);
        if (steps.Count == 0)
        {
            throw new InvalidOperationException(
                "This route has no fixed spawn-position movement.");
        }

        progress?.Report(
            new MacroProgress(
                "Route position",
                45,
                "Moving the player to the recording start position."));
        foreach ((char key, int duration) in steps)
        {
            if (!_automation.Focus(window))
            {
                throw new RobloxSessionUnavailableException(
                    "Windows could not focus Roblox before route positioning.");
            }
            await _automation.HoldLetterKeyAsync(
                    window,
                    key,
                    duration,
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(
                    120,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        progress?.Report(
            new MacroProgress(
                "Route position",
                100,
                "Roblox is at the route's recording start position."));
    }

    internal static IReadOnlyList<(
        char Key,
        int Milliseconds)> StepsFor(
        PlacementTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        if (target.Mode != PlacementTargetMode.Event)
        {
            return [];
        }

        return (
            (EventAct)target.ActNumber,
            target.SpawnRoute) switch
        {
            (EventAct.Act1, EventSpawnRoute.Angle1) =>
            [
                ('W', 750),
                ('D', 750),
                ('W', 750),
            ],
            (EventAct.Act1, EventSpawnRoute.Angle2) =>
            [
                ('W', 750),
                ('D', 750),
                ('W', 2100),
            ],
            (EventAct.Act2, EventSpawnRoute.Angle1) =>
            [
                ('A', 75),
                ('W', 2000),
            ],
            (EventAct.Act3, EventSpawnRoute.Angle1) or
            (EventAct.Act4, EventSpawnRoute.Angle1) =>
                [],
            _ => throw new InvalidDataException(
                "The Event act does not support that spawn route."),
        };
    }
}
