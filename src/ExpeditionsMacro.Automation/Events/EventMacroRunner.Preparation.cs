using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Automation.Scheduling;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Events;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Events;

public sealed partial class EventMacroRunner
{
    private async Task PrepareMatchAsync(
        RobloxWindow window,
        EventPreset preset,
        PlacementModel placement,
        char? unitMenuKey,
        RepeatedRoutePreparationState preparation,
        bool arrivedFromRepeatStage,
        IDetectorPack detector,
        IProgress<MacroProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (preparation.ShouldLoadTeam)
        {
            EventScreenMatch current =
                EventScreenDetector.Detect(
                    CaptureClient(window, detector));
            if (current.State !=
                EventScreenState.Prestart)
            {
                throw new RobloxUiUnavailableException(
                    "Event team loading requires a confirmed prestart screen.");
            }
            await _teams.SelectAsync(
                window,
                preset.TeamSlot,
                unitMenuKey!.Value,
                progress,
                cancellationToken).ConfigureAwait(false);
            await WaitForStateAsync(
                window,
                EventScreenState.Prestart,
                NavigationTimeout,
                detector,
                cancellationToken).ConfigureAwait(false);
            preparation.MarkTeamLoaded();
        }

        if (preparation.ShouldAlignCamera(
                arrivedFromRepeatStage))
        {
            await _fastNoAlign.EnsurePreparedAsync(
                window,
                preset.ZoomTicks,
                preset.PitchDragPixels,
                progress,
                cancellationToken).ConfigureAwait(false);
            preparation.MarkCameraAligned();
            if (!arrivedFromRepeatStage)
            {
                await RunSpawnMovementAsync(
                    window,
                    preset,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        placement.ValidateCompatibility(
            CameraPreparationMode.FastNoAlign,
            PlacementTarget.ForEvent(preset));
    }

    private async Task RunSpawnMovementAsync(
        RobloxWindow window,
        EventPreset preset,
        CancellationToken cancellationToken)
    {
        foreach ((char key, int duration) in
                 SpawnMovementFor(preset))
        {
            Focus(window);
            await _automation.HoldLetterKeyAsync(
                window,
                key,
                duration,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(
                120,
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static IReadOnlyList<(
        char Key,
        int Milliseconds)> SpawnMovementFor(
        EventPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return PlacementRoutePositioningService.StepsFor(
            PlacementTarget.ForEvent(preset));
    }

    private Task PlayPlacementAsync(
        RobloxWindow window,
        EventPreset preset,
        PlacementModel placement,
        IReadOnlyList<PlacementStep> steps,
        char cancelPlacementKey,
        CancellationToken cancellationToken) =>
        _placements.PlayStepsAsync(
            window,
            placement,
            steps,
            useDefaultInterval: false,
            defaultIntervalMilliseconds: 0,
            preset.UnitKeyHoldMilliseconds,
            preset.UnitSelectDelayMilliseconds,
            cancelPlacementKey,
            stepSent: null,
            status: null,
            cancellationToken);
}
