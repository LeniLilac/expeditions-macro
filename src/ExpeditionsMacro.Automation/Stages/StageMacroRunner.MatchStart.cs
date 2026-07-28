using System.Diagnostics;
using ExpeditionsMacro.Automation.Navigation;
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
        StoryPreset? story,
        RaidPreset? raid,
        IDetectorPack detector,
        int stableDetections,
        ManualInputRecording? manualRecording,
        IProgress<MacroProgress>? progress,
        Action<string, int, string, string?, double?> report,
        char cancelPlacementKey,
        CancellationToken cancellationToken)
    {
        PlacementMatchExecutionPlan execution =
            PlacementExecutionPlan.ForMatch(
                models.Placement);
        if (execution.BeforeStart.Count > 0 &&
            models.Placement is not null)
        {
            report(
                "Placement",
                45,
                "Placing before-start units.",
                null,
                null);
            await PlayPlacementAsync(
                    window,
                    models.Placement,
                    execution.BeforeStart,
                    story,
                    raid,
                    cancelPlacementKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        StableScreenAction<StageScreenMatch>? liveStart =
            await StableScreenActionWaiter.WaitAsync(
                    StageScreenState.Prestart,
                    stableDetections,
                    () => StageScreenDetector.Detect(
                        CaptureClient(window, detector)),
                    static match => match.State,
                    static match =>
                        match.ActionX is int x &&
                        match.ActionY is int y
                            ? (x, y)
                            : null,
                    TimeSpan.FromSeconds(12),
                    TimeSpan.FromMilliseconds(
                        Math.Max(
                            200,
                            story?.PollMilliseconds ??
                                raid!.PollMilliseconds)),
                    cancellationToken)
                .ConfigureAwait(false);
        if (liveStart is null)
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
                    liveStart.Value.X,
                    liveStart.Value.Y,
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
