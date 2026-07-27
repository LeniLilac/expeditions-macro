using System.Diagnostics;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Stages;

namespace ExpeditionsMacro.Automation.Stages;

public sealed partial class StageMacroRunner
{
    private async Task<(Stopwatch Runtime, bool ManualPlayback)>
        BeginConfiguredMatchAsync(
        RobloxWindow window,
        StageMode mode,
        StageRuntimeModels models,
        CameraPreparationMode cameraMode,
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        ManualInputRecording? manualRecording,
        IProgress<MacroProgress>? progress,
        Action<string, int, string, string?, double?> report,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                cameraMode,
                models.PrestartPlacement,
                models.DelayedPlacement);
        if (execution.BeforeStart.Count > 0 &&
            models.PrestartPlacement is not null)
        {
            report(
                "Placement",
                45,
                "Placing before-start units.",
                null,
                null);
            await PlayPlacementAsync(
                    window,
                    models.PrestartPlacement,
                    execution.BeforeStart,
                    story,
                    raid,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        StageScreenMatch prestart =
            StageScreenDetector.Detect(
                CaptureClient(window, detector));
        if (prestart.State !=
                StageScreenState.Prestart ||
            prestart.ActionX is null ||
            prestart.ActionY is null)
        {
            throw new RobloxUiUnavailableException(
                $"The {Label(mode)} Start Game button disappeared before it could be clicked.");
        }

        Stopwatch runtime;
        if (execution.ManualPlayback)
        {
            if (_manualInputs is null ||
                manualRecording is null)
            {
                throw new InvalidOperationException(
                    "Manual input playback is unavailable.");
            }
            runtime =
                await ManualInputMatchPlayback.PlayAsync(
                    _manualInputs,
                    window,
                    manualRecording,
                    progress,
                    matchStarting: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            runtime = Stopwatch.StartNew();
            await ClickAsync(
                    window,
                    prestart.ActionX.Value,
                    prestart.ActionY.Value,
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(
                    1800,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return (
            runtime,
            execution.ManualPlayback);
    }
}
