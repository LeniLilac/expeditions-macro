using ExpeditionsMacro.Automation.Discord;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Stages;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Packs;

namespace ExpeditionsMacro.App.Pages;

public partial class MacroPage
{
    private async Task<ScheduledTaskResult>
        ExecuteEventAsync(
        MacroTaskDefinition task,
        Func<
            ScheduledTaskResult,
            CancellationToken,
            Task<ScheduledTaskContinuation>> recordResult,
        string webhook,
        string discordUserId,
        char playMenuKey,
        char? unitMenuKey,
        MacroRunTotals macroTotals,
        IProgress<MacroProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!task.UsesPlacementSetup)
        {
            throw new InvalidOperationException(
                "Event routes require Fast no align Placement Setup.");
        }
        (EventPreset preset, PlacementModel placement) =
            await BuildEventSetupAsync(
                task,
                cancellationToken).ConfigureAwait(false);
        IDetectorPack detector = await LoadDetectorAsync(
            AnimeExpeditionsDetectorSpec.PackId,
            cancellationToken).ConfigureAwait(false);
        StageRunResult result =
            await _services.Events.RunAsync(
                preset,
                placement,
                detector,
                webhook,
                playMenuKey,
                unitMenuKey,
                progress,
                entry => DispatchLog(entry),
                cancellationToken,
                continueScheduledRoute: async (
                    victories,
                    defeats,
                    runtime,
                    token) =>
                    await recordResult(
                        new ScheduledTaskResult(
                            victories,
                            defeats,
                            runtime),
                        token).ConfigureAwait(false)
                    == ScheduledTaskContinuation.RepeatStage,
                macroTotals: macroTotals)
                .ConfigureAwait(false);
        return ToScheduledResult(result);
    }
}
