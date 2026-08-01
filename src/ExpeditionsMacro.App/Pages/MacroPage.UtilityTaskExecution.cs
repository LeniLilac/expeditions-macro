using System.Diagnostics;
using ExpeditionsMacro.Automation.Refuel;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<ScheduledTaskResult>
        ExecuteUtilityAsync(
        MacroTaskDefinition task,
        Func<ScheduledTaskCheckpoint, Task>
            recordCheckpoint,
        RefuelTaskStateSession refuelStates,
        char playMenuKey,
        char? areasMenuKey,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        if (task.Kind != MacroTaskKind.Utility)
        {
            throw new ArgumentException(
                "The task is not a Utility task.",
                nameof(task));
        }
        if (areasMenuKey is not char areasKey)
        {
            throw new InvalidOperationException(
                "Scroll down to Controls on the Dashboard and set Toggle Areas Menu key before running a refuel Utility.");
        }

        IDetectorPack detector =
            await LoadDetectorAsync(
                    AnimeExpeditionsDetectorSpec.PackId,
                    cancellationToken)
                .ConfigureAwait(false);
        Stopwatch runtime = Stopwatch.StartNew();
        ResourceRefuelTarget pending =
            refuelStates.Pending(task);
        if (pending == ResourceRefuelTarget.None)
        {
            // A full target set is never persisted, but fail safe if a future
            // schema or interrupted write provides one.
            refuelStates.Complete(task.Id);
            pending = task.RefuelTarget;
        }
        if (pending != task.RefuelTarget)
        {
            DispatchLog(new MacroEvent(
                DateTimeOffset.Now,
                MacroEventLevel.Information,
                $"Resuming {task.Name}; {RefuelTargetLabel(pending)} remains.",
                "resource_refuel_resumed"));
        }

        ResourceRefuelResult result =
            await _services.ResourceRefuel.RunAsync(
                    new ResourceRefuelRequest
                    {
                        Start =
                            ResourceRefuelStart
                                .SharedNavigation,
                        Targets = pending,
                        Settings = _services.Settings
                            .ResourceRefuelDebug,
                        AreasMenuKey = areasKey,
                        PlayMenuKey = playMenuKey,
                        OpenPlayWhenComplete = false,
                        ReturnToLobbyWhenComplete = true,
                        StationCompleted = async target =>
                        {
                            if (refuelStates.TryRecordPartial(
                                    task,
                                    target,
                                    out ResourceRefuelTarget completed))
                            {
                                await recordCheckpoint(
                                        new ScheduledTaskCheckpoint(
                                            completed))
                                    .ConfigureAwait(false);
                            }
                        },
                    },
                    detector,
                    progress,
                    entry => DispatchLog(entry),
                    cancellationToken)
                .ConfigureAwait(false);
        runtime.Stop();
        refuelStates.Complete(task.Id);

        DateTimeOffset nextEligible =
            result.CompletedAtUtc.AddMinutes(
                task.RefuelIntervalMinutes);
        DispatchLog(new MacroEvent(
            DateTimeOffset.Now,
            MacroEventLevel.Information,
            $"{task.Name} is next due at " +
            $"{nextEligible.LocalDateTime:t}.",
            "resource_refuel_scheduled"));
        return new ScheduledTaskResult(
            0,
            0,
            runtime.Elapsed,
            nextEligible);
    }

    private static string RefuelTargetLabel(
        ResourceRefuelTarget target) =>
        target switch
        {
            ResourceRefuelTarget.GoldMine =>
                "Gold Mine",
            ResourceRefuelTarget.ResourceDrill =>
                "Resource Drill",
            _ => "the remaining resource station",
        };
}
