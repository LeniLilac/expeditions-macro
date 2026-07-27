using System.Diagnostics;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Geometry;
using ExpeditionsMacro.Core.Imaging;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;
using ExpeditionsMacro.Vision.Challenges;

namespace ExpeditionsMacro.Automation.Challenges;

public sealed partial class ChallengeMacroRunner
{
    private async Task<(Stopwatch Runtime,
        IReadOnlyList<PlacementStep> AfterStart)>
        BeginConfiguredMatchAsync(
        RobloxWindow window,
        ChallengePreset preset,
        ChallengeMapRuntimeModels models,
        ManualInputRecording? manualRecording,
        ImageFrame prestart,
        IDetectorPack detector,
        Action attemptStarted,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                preset.CameraPreparationMode,
                models.PrestartPlacement,
                models.DelayedPlacement);
        ChallengePlacementPartition? partition =
            await PlaceVisiblePrestartAsync(
                    window,
                    preset,
                    models.PrestartPlacement,
                    execution.BeforeStart,
                    prestart,
                    report,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);

        (int X, int Y)? start =
            await LocateActionAfterParkingAsync(
                    token =>
                        _automation.ParkCursorAsync(
                            window,
                            token),
                    () => CaptureClient(
                        window,
                        detector),
                    frame =>
                        ChallengeScreenDetector.ActionFor(
                            ChallengeScreenState.Prestart,
                            frame),
                    retryMilliseconds: 100,
                    maximumAttempts: 3,
                    cancellationToken)
                .ConfigureAwait(false);
        if (start is null)
        {
            throw new RobloxUiUnavailableException(
                "The Challenge Start Game button disappeared before it could be clicked.");
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
                    new Progress<MacroProgress>(
                        value => report(
                            value.Phase,
                            value.Percent,
                            value.Message,
                            value.DetectedState,
                            value.Confidence)),
                    attemptStarted,
                    cancellationToken)
                .ConfigureAwait(false);
            return (
                runtime,
                execution.AfterStart);
        }

        runtime = Stopwatch.StartNew();
        attemptStarted();
        await ClickAsync(
                window,
                start.Value.X,
                start.Value.Y,
                cancellationToken)
            .ConfigureAwait(false);
        if (partition is
            { AfterStart.Count: > 0 } &&
            models.PrestartPlacement is not null)
        {
            await Task.Delay(
                    550,
                    cancellationToken)
                .ConfigureAwait(false);
            report(
                "Placement",
                50,
                $"Placing {partition.AfterStart.Count} unit(s) that were covered by the Start Game dialog.",
                null,
                null);
            await PlaceAsync(
                    window,
                    preset,
                    models.PrestartPlacement,
                    partition.AfterStart,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        await Task.Delay(
                2200,
                cancellationToken)
            .ConfigureAwait(false);
        return (
            runtime,
            execution.AfterStart);
    }

    private async Task<ChallengePlacementPartition?>
        PlaceVisiblePrestartAsync(
        RobloxWindow window,
        ChallengePreset preset,
        PlacementModel? placement,
        IReadOnlyList<PlacementStep> steps,
        ImageFrame prestart,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        if (placement is null ||
            steps.Count == 0)
        {
            return null;
        }

        ScreenRegion occlusion =
            ChallengeScreenDetector.PrestartOcclusion(
                prestart) ??
            throw new RobloxUiUnavailableException(
                "The Challenge Start Game dialog could not be measured before placement.");
        ChallengePlacementPartition partition =
            ChallengeRunPolicy.PartitionPrestartPlacements(
                steps,
                occlusion);
        report(
            "Placement",
            45,
            "Placing units outside the Start Game dialog.",
            null,
            null);
        if (partition.BeforeStart.Count > 0)
        {
            await PlaceAsync(
                    window,
                    preset,
                    placement,
                    partition.BeforeStart,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (partition.AfterStart.Count > 0)
        {
            log(
                $"Deferred {partition.AfterStart.Count} before-start placement(s) hidden by the Start Game dialog.",
                MacroEventLevel.Information,
                null,
                null);
        }
        return partition;
    }
}
