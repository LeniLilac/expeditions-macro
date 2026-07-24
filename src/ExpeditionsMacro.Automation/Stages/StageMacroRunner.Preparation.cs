using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task PrepareMatchAsync(
        RobloxWindow window,
        StageMode mode,
        StoryPreset? story,
        RaidPreset? raid,
        StageRuntimeModels models,
        char? unitMenuKey,
        RepeatedRoutePreparationState preparation,
        bool arrivedFromRepeatStage,
        IProgress<MacroProgress>? progress,
        IDetectorPack detector,
        int stableDetections,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        CancellationToken cancellationToken)
    {
        int teamSlot = story?.TeamSlot ?? raid!.TeamSlot;
        if (preparation.ShouldLoadTeam)
        {
            report(
                "Team",
                14,
                $"Prestart recognized. Loading Team {teamSlot}.",
                null,
                null);
            StageNavigationPolicy.RequirePrestartForTeamLoad(
                StageScreenDetector.Detect(CaptureClient(window, detector)));
            await _teams.SelectAsync(
                window,
                teamSlot,
                unitMenuKey!.Value,
                progress,
                cancellationToken).ConfigureAwait(false);
            await WaitForStateAsync(
                window,
                StageScreenState.Prestart,
                NavigationTimeout,
                detector,
                stableDetections,
                cancellationToken).ConfigureAwait(false);
            preparation.MarkTeamLoaded();
            log(
                $"Team {teamSlot} loaded from the confirmed {Label(mode)} prestart screen.",
                MacroEventLevel.Success,
                null,
                null);
        }

        if (!preparation.ShouldAlignCamera(arrivedFromRepeatStage))
        {
            const string message =
                "Repeat Stage preserved the camera and team state; skipping repeated preparation.";
            report(
                "Camera",
                20,
                message,
                "repeat_preparation_reused",
                null);
            log(
                message,
                MacroEventLevel.Success,
                "repeat_preparation_reused",
                null);
            return;
        }

        int zoomTicks = story?.ZoomTicks ?? raid!.ZoomTicks;
        int pitchDragPixels = story?.PitchDragPixels ?? raid!.PitchDragPixels;
        report(
            "Camera",
            20,
            "Preparing and aligning the camera.",
            null,
            null);
        double confidence = await _camera.PrepareAndAlignAsync(
            models.Camera,
            window,
            zoomTicks,
            pitchDragPixels,
            progress,
            cancellationToken).ConfigureAwait(false);
        preparation.MarkCameraAligned();
        log(
            $"Camera alignment finished at {confidence:P0} confidence.",
            MacroEventLevel.Success,
            "camera",
            confidence);
    }
}
