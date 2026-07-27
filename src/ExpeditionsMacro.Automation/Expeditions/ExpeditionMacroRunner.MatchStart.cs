using System.Diagnostics;
using ExpeditionsMacro.Automation.Placement;
using ExpeditionsMacro.Core.Abstractions;
using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Runtime;

namespace ExpeditionsMacro.Automation.Expeditions;

public sealed partial class ExpeditionMacroRunner
{
    private async Task<ExpeditionMatchStart?>
        BeginConfiguredMatchAsync(
        RobloxWindow window,
        ExpeditionPreset preset,
        PlacementModel placement,
        ManualInputRecording? manualRecording,
        IDetectorPack detector,
        IProgress<MacroProgress>? progress,
        DateTimeOffset? stopAfterCurrentRunUtc,
        Action<string, int, string, string?, double?> report,
        Action<string, MacroEventLevel, string?, double?> log,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                placement);
        if (execution.BeforeStart.Count > 0)
        {
            report(
                "Placement",
                0,
                "Placing the recorded prestart units.",
                null,
                null);
            await PlaceStepsAsync(
                    window,
                    placement,
                    execution.BeforeStart,
                    preset,
                    log,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
            log(
                $"Preplace pass sent {execution.BeforeStart.Count} placement(s).",
                MacroEventLevel.Information,
                null,
                null);
        }

        await ThrowIfRecoveryAsync(
                window,
                detector,
                preset,
                cancellationToken)
            .ConfigureAwait(false);
        if (ExpeditionRunPolicy.StopDeadlineReached(
                DateTimeOffset.UtcNow,
                stopAfterCurrentRunUtc))
        {
            log(
                "Challenge reset reached during Expedition preparation. Returning before starting the node.",
                MacroEventLevel.Success,
                null,
                null);
            return null;
        }

        report(
            "Starting node",
            0,
            "Starting the Expedition node.",
            null,
            null);
        if (execution.ManualPlayback)
        {
            _ = await WaitForStableActionAsync(
                    "start",
                    initialFrame: null,
                    () => CaptureClient(
                        window,
                        detector),
                    (action, frame) =>
                        ActionIsOwned(
                            detector,
                            action,
                            frame),
                    detector.ActionFor,
                    static () =>
                        DateTimeOffset.UtcNow,
                    static (duration, token) =>
                        Task.Delay(
                            duration,
                            token),
                    cancellationToken)
                .ConfigureAwait(false);
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
            await ClickActionAsync(
                    window,
                    detector,
                    "start",
                    cancellationToken)
                .ConfigureAwait(false);
            await Task.Delay(
                    2600,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new ExpeditionMatchStart(
            runtime,
            execution.BeforeStart,
            execution.AfterStart);
    }

    private sealed record ExpeditionMatchStart(
        Stopwatch Runtime,
        IReadOnlyList<PlacementStep> BeforeStart,
        IReadOnlyList<PlacementStep> AfterStart);
}
