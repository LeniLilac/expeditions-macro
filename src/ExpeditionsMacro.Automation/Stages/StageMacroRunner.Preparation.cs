using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Automation.Teams;
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
        TeamOperationSession? teamSession,
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
            teamSession?.BeginSelection(teamSlot);
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
            teamSession?.MarkLoaded(
                window,
                teamSlot);
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
        bool prepared =
            await _fastNoAlign.EnsurePreparedAsync(
            window,
            zoomTicks,
            pitchDragPixels,
            progress,
            cancellationToken).ConfigureAwait(false);
        preparation.MarkCameraAligned();
        log(
            prepared
                ? "Fast no align prepared zoom and pitch without changing yaw."
                : "Fast no align reused the camera pose preserved from the previous match.",
            MacroEventLevel.Success,
            prepared
                ? "fast_no_align"
                : "fast_no_align_reused",
            null);
    }
}
