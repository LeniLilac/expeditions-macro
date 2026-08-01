using ExpeditionsMacro.Automation.Recovery;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.Automation.Refuel;

public enum ResourceRefuelStart
{
    CurrentLobby,
    RestartPrivateServer,
    SharedNavigation,
}

public sealed record ResourceRefuelRequest
{
    public required ResourceRefuelStart Start { get; init; }

    public required ResourceRefuelTarget Targets { get; init; }

    public required ResourceRefuelDebugSettings Settings { get; init; }

    public required char AreasMenuKey { get; init; }

    public required char PlayMenuKey { get; init; }

    public RobloxPrivateServerLaunchTarget? RestartTarget { get; init; }

    public bool OpenPlayWhenComplete { get; init; } = true;

    public bool ReturnToLobbyWhenComplete { get; init; }

    public Func<ResourceRefuelTarget, Task>?
        StationCompleted
    { get; init; }
}

public sealed record ResourceRefuelResult(
    ResourceRefuelTarget CompletedTargets,
    DateTimeOffset CompletedAtUtc);

public sealed class ResourceRefuelService
{
    private readonly IRobloxAutomation _automation;
    private readonly IRobloxRuntimeRecoveryService _recovery;
    private readonly ResourceRefuelNavigator _navigator;

    public ResourceRefuelService(
        IRobloxAutomation automation,
        IRobloxRuntimeRecoveryService recovery)
        : this(
            automation,
            recovery,
            static (delay, token) => Task.Delay(delay, token),
            static () => DateTimeOffset.UtcNow)
    {
    }

    internal ResourceRefuelService(
        IRobloxAutomation automation,
        IRobloxRuntimeRecoveryService recovery,
        Func<TimeSpan, CancellationToken, Task> delay,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(delay);
        _automation = automation;
        _recovery = recovery;
        _navigator = new ResourceRefuelNavigator(
            automation,
            delay,
            utcNow ??
                (() => DateTimeOffset.UtcNow));
    }

    public async Task<ResourceRefuelResult> RunAsync(
        ResourceRefuelRequest request,
        IDetectorPack detector,
        IProgress<MacroProgress>? progress = null,
        Action<MacroEvent>? log = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(detector);
        request.Settings.Validate();
        ValidateRequest(request);

        void ReportNavigation(
            string message,
            string state,
            MacroEventLevel level) =>
            Report(
                progress,
                log,
                message,
                state,
                level);

        RobloxWindow window = await AcquireLobbyAsync(
            request,
            detector,
            ReportNavigation,
            cancellationToken).ConfigureAwait(false);
        ResourceRefuelTarget completed =
            ResourceRefuelTarget.None;

        foreach (ResourceRefuelTarget target in Targets(
                     request.Targets))
        {
            await _navigator.RunStationWithRetriesAsync(
                window,
                target,
                request,
                ReportNavigation,
                cancellationToken).ConfigureAwait(false);
            completed |= target;
            if (request.StationCompleted is not null)
            {
                await request.StationCompleted(target)
                    .ConfigureAwait(false);
            }
        }

        if (request.ReturnToLobbyWhenComplete)
        {
            ReportNavigation(
                "Resource refuel complete. Returning to Lobby through Areas.",
                "resource_refuel_lobby_return",
                MacroEventLevel.Information);
            await _navigator.ReturnToLobbyViaAreasAsync(
                window,
                detector,
                request.AreasMenuKey,
                cancellationToken,
                openAreasFromOwnedStation: true)
                .ConfigureAwait(false);
        }
        else if (request.OpenPlayWhenComplete)
        {
            ReportNavigation(
                "Resource refuel complete. Opening Play.",
                "resource_refuel_play",
                MacroEventLevel.Information);
            await _navigator.OpenPlayAsync(
                window,
                request.PlayMenuKey,
                cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        ReportNavigation(
            $"Resource refuel completed for {Label(completed)}.",
            "resource_refuel_complete",
            MacroEventLevel.Success);
        return new ResourceRefuelResult(completed, completedAt);
    }

    private async Task<RobloxWindow> AcquireLobbyAsync(
        ResourceRefuelRequest request,
        IDetectorPack detector,
        Action<string, string, MacroEventLevel> report,
        CancellationToken cancellationToken)
    {
        RobloxWindow window;
        if (request.Start ==
            ResourceRefuelStart.RestartPrivateServer)
        {
            report(
                "Restarting Roblox before the resource-refuel test.",
                "resource_refuel_restart",
                MacroEventLevel.Information);
            window = await _recovery.RestartAsync(
                request.RestartTarget!,
                progress: null,
                log: null,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            window = _automation.FindWindow() ??
                throw new RobloxSessionUnavailableException(
                    "No visible Roblox window was found.");
        }

        TimeSpan timeout =
            request.Start ==
            ResourceRefuelStart.RestartPrivateServer
                ? TimeSpan.FromMinutes(2)
                : TimeSpan.FromSeconds(5);
        if (request.Start ==
            ResourceRefuelStart.SharedNavigation)
        {
            await _navigator
                .PrepareScheduledLobbyAsync(
                    window,
                    detector,
                    request.AreasMenuKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _navigator.PrepareLobbyAsync(
                window,
                detector,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }
        report(
            "Lobby ready. Opening Areas.",
            "resource_refuel_lobby",
            MacroEventLevel.Information);
        return window;
    }

    private static void ValidateRequest(
        ResourceRefuelRequest request)
    {
        if (request.Targets == ResourceRefuelTarget.None ||
            (request.Targets & ~ResourceRefuelTarget.Both) != 0)
        {
            throw new InvalidDataException(
                "Choose Gold Mine, Resource Drill, or both.");
        }
        if (!char.IsAsciiLetter(request.AreasMenuKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard and set Toggle Areas Menu key to the same A-Z letter assigned in Anime Expeditions.");
        }
        if (!char.IsAsciiLetter(request.PlayMenuKey))
        {
            throw new InvalidDataException(
                "Scroll down to Controls on the Dashboard and set Toggle Play Menu key to the same A-Z letter assigned in Anime Expeditions.");
        }
        if (request.Start ==
                ResourceRefuelStart.RestartPrivateServer &&
            request.RestartTarget is null)
        {
            throw new InvalidDataException(
                "A configured private-server link is required for the restart start state.");
        }
    }

    private static IEnumerable<ResourceRefuelTarget> Targets(
        ResourceRefuelTarget targets)
    {
        if (targets.HasFlag(ResourceRefuelTarget.GoldMine))
        {
            yield return ResourceRefuelTarget.GoldMine;
        }
        if (targets.HasFlag(
                ResourceRefuelTarget.ResourceDrill))
        {
            yield return ResourceRefuelTarget.ResourceDrill;
        }
    }

    private static void Report(
        IProgress<MacroProgress>? progress,
        Action<MacroEvent>? log,
        string message,
        string state,
        MacroEventLevel level)
    {
        progress?.Report(new MacroProgress(
            "Refuel",
            0,
            message,
            state));
        log?.Invoke(new MacroEvent(
            DateTimeOffset.Now,
            level,
            message,
            state));
    }

    internal static string Label(
        ResourceRefuelTarget target) =>
        target switch
        {
            ResourceRefuelTarget.GoldMine => "Gold Mine",
            ResourceRefuelTarget.ResourceDrill =>
                "Resource Drill",
            ResourceRefuelTarget.Both =>
                "Gold Mine and Resource Drill",
            _ => "resource stations",
        };
}
