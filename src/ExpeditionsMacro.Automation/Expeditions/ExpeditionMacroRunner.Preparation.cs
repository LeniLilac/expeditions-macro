using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task PrepareMatchAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        CameraModel model,
        char? unitMenuKey,
        RepeatedRoutePreparationState preparation,
        bool arrivedFromRepeatStage,
        IProgress<MacroProgress>? progress,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        if (preparation.ShouldLoadTeam)
        {
            await _teams.SelectAsync(
                window,
                preset.TeamSlot,
                unitMenuKey!.Value,
                progress,
                cancellationToken).ConfigureAwait(false);
            preparation.MarkTeamLoaded();
        }

        if (!preparation.ShouldAlignCamera(arrivedFromRepeatStage))
        {
            const string message =
                "Repeat Stage preserved the Expedition camera and team state; skipping repeated preparation.";
            progress?.Report(new MacroProgress(
                "Camera",
                20,
                message,
                "repeat_preparation_reused"));
            log(
                message,
                MacroEventLevel.Success,
                "repeat_preparation_reused",
                null);
            return;
        }

        log(
            "Prestart screen recognized. Preparing camera.",
            MacroEventLevel.Success,
            null,
            null);
        double score = await _camera.PrepareAndAlignAsync(
            model,
            window,
            preset.ZoomTicks,
            preset.PitchDragPixels,
            progress,
            cancellationToken).ConfigureAwait(false);
        preparation.MarkCameraAligned();
        log(
            $"Camera alignment finished at {score:P0} confidence.",
            MacroEventLevel.Information,
            null,
            score);
        await Task.Delay(350, cancellationToken).ConfigureAwait(false);
    }
}
